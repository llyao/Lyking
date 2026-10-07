using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Lyking.GameFramework.Client.Services.ContentUpdate
{
    /// <summary>提供显式恢复到前一份已激活内容快照的存储。</summary>
    public interface IRecoverableContentVersionStore : IContentVersionStore
    {
        ValueTask<ContentVersionState> RestorePreviousAsync(CancellationToken cancellationToken);
    }

    /// <summary>把每次更新写入独立快照，并通过校验过的版本指针原子切换当前快照。</summary>
    public sealed class FileContentVersionStore : IRecoverableContentVersionStore
    {
        private static readonly byte[] PointerMagic = Encoding.ASCII.GetBytes("LYCV");
        private static readonly ConcurrentDictionary<string, SemaphoreSlim> ActivationGates =
            new(StringComparer.OrdinalIgnoreCase);
        private const int ActivationLockTimeoutMilliseconds = 10000;
        private const int ActivationLockRetryDelayMilliseconds = 10;
        private readonly string _root;
        private readonly string _snapshots;
        private readonly string _pointer;
        private readonly string _activationLockPath;
        private readonly SemaphoreSlim _activationGate;

        private sealed class StoredContentPointer
        {
            public StoredContentPointer(ContentVersionState state, string? previousVersion,
                string? previousSnapshotId, long previousSequence, string? previousManifestHash)
            {
                State = state;
                PreviousVersion = previousVersion;
                PreviousSnapshotId = previousSnapshotId;
                PreviousSequence = previousSequence;
                PreviousManifestHash = previousManifestHash;
            }

            public ContentVersionState State { get; }
            public string? PreviousVersion { get; }
            public string? PreviousSnapshotId { get; }
            public long PreviousSequence { get; }
            public string? PreviousManifestHash { get; }
        }

        public FileContentVersionStore(string rootDirectory)
        {
            if (string.IsNullOrWhiteSpace(rootDirectory)) throw new ArgumentException("内容根目录不能为空。", nameof(rootDirectory));
            _root = Path.GetFullPath(rootDirectory);
            _snapshots = Path.Combine(_root, "snapshots");
            _pointer = Path.Combine(_root, "current.ptr");
            _activationLockPath = Path.Combine(_root, "activation.lock");
            _activationGate = ActivationGates.GetOrAdd(_root, _ => new SemaphoreSlim(1, 1));
            Directory.CreateDirectory(_snapshots);
        }

        public async ValueTask<ContentVersionState> ReadStateAsync(CancellationToken cancellationToken)
        {
            if (!File.Exists(_pointer)) return new ContentVersionState(null, null, 0, 0);
            byte[] pointer = await File.ReadAllBytesAsync(_pointer, cancellationToken).ConfigureAwait(false);
            return DecodePointer(pointer).State;
        }

        public ValueTask BeginStagingAsync(string stagingId, string version, long sequence, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ValidateStagingId(stagingId);
            if (string.IsNullOrWhiteSpace(version) || sequence <= 0) throw new ArgumentException("暂存版本元数据无效。");
            string path = SnapshotPath(stagingId);
            if (Directory.Exists(path)) throw new IOException("内容暂存标识已存在。");
            Directory.CreateDirectory(Path.Combine(path, "files"));
            return default;
        }

        public async ValueTask WriteStagedFileAsync(string stagingId, string path, byte[] content, CancellationToken cancellationToken)
        {
            if (content == null) throw new ArgumentNullException(nameof(content));
            string fullPath = ResolveFilePath(stagingId, path);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            using (var stream = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, useAsync: true))
            {
                await stream.WriteAsync(content, 0, content.Length, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                stream.Flush(true);
            }
        }

        public async ValueTask<byte[]?> ReadStagedFileAsync(string stagingId, string path, CancellationToken cancellationToken)
        {
            string fullPath = ResolveFilePath(stagingId, path);
            if (!File.Exists(fullPath)) return null;
            return await File.ReadAllBytesAsync(fullPath, cancellationToken).ConfigureAwait(false);
        }

        public bool ActivateStaged(string stagingId, string version, long sequence, string manifestHash)
        {
            ValidateStagingId(stagingId);
            if (string.IsNullOrWhiteSpace(version) || sequence <= 0) throw new ArgumentException("激活版本元数据无效。");
            if (!IsValidManifestHash(manifestHash)) throw new ArgumentException("清单哈希必须是小写 SHA-256 十六进制值。", nameof(manifestHash));
            if (!Directory.Exists(Path.Combine(SnapshotPath(stagingId), "files"))) throw new DirectoryNotFoundException("内容暂存快照不存在。");
            _activationGate.Wait();
            try
            {
                using (AcquireActivationFileLock())
                {
                    StoredContentPointer current = ReadStoredPointer();
                    if (sequence <= current.State.HighestSequence)
                    {
                        if (sequence == current.State.ActiveSequence
                            && string.Equals(version, current.State.ActiveVersion, StringComparison.Ordinal)
                            && string.Equals(manifestHash, current.State.ActiveManifestHash, StringComparison.Ordinal))
                        {
                            return false;
                        }

                        throw new ContentManifestRejectedException("内容清单序号已被其他更新激活或超过当前高水位。");
                    }

                    var next = new StoredContentPointer(
                        new ContentVersionState(version, stagingId, sequence, sequence, manifestHash),
                        current.State.ActiveVersion, current.State.ActiveSnapshotId, current.State.ActiveSequence,
                        current.State.ActiveManifestHash);
                    WritePointerAtomically(EncodePointer(next));
                    return true;
                }
            }
            finally { _activationGate.Release(); }
        }

        public async ValueTask<ContentVersionState> RestorePreviousAsync(CancellationToken cancellationToken)
        {
            await _activationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                using (AcquireActivationFileLock(cancellationToken))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    StoredContentPointer current = ReadStoredPointer();
                    if (current.PreviousVersion == null || current.PreviousSnapshotId == null || current.PreviousSequence <= 0)
                        throw new ContentUpdateException("没有可恢复的上一内容快照。");
                    if (!Directory.Exists(Path.Combine(SnapshotPath(current.PreviousSnapshotId), "files")))
                        throw new ContentUpdateException("上一内容快照不存在，无法恢复。");

                    var restored = new ContentVersionState(current.PreviousVersion, current.PreviousSnapshotId,
                        current.PreviousSequence, current.State.HighestSequence, current.PreviousManifestHash);
                    WritePointerAtomically(EncodePointer(new StoredContentPointer(restored, null, null, 0, null)));
                    return restored;
                }
            }
            finally { _activationGate.Release(); }
        }

        private FileStream AcquireActivationFileLock(CancellationToken cancellationToken = default)
        {
            Stopwatch wait = Stopwatch.StartNew();
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try { return new FileStream(_activationLockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
                catch (IOException exception)
                {
                    if (wait.ElapsedMilliseconds >= ActivationLockTimeoutMilliseconds)
                        throw new IOException("等待内容版本跨进程激活锁超时。", exception);
                    Thread.Sleep(ActivationLockRetryDelayMilliseconds);
                }
            }
        }

        private StoredContentPointer ReadStoredPointer()
        {
            if (!File.Exists(_pointer))
                return new StoredContentPointer(new ContentVersionState(null, null, 0, 0), null, null, 0, null);
            return DecodePointer(File.ReadAllBytes(_pointer));
        }

        private void WritePointerAtomically(byte[] pointer)
        {
            string temp = Path.Combine(_root, "current." + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, useAsync: false))
                {
                    stream.Write(pointer, 0, pointer.Length);
                    stream.Flush(true);
                }
                if (File.Exists(_pointer)) File.Replace(temp, _pointer, null);
                else File.Move(temp, _pointer);
            }
            finally
            {
                if (File.Exists(temp)) File.Delete(temp);
            }
        }

        public ValueTask DiscardStagedAsync(string stagingId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ValidateStagingId(stagingId);
            string path = SnapshotPath(stagingId);
            if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
            return default;
        }

        public async ValueTask<byte[]?> ReadActiveFileAsync(string path, CancellationToken cancellationToken)
        {
            ContentVersionState state = await ReadStateAsync(cancellationToken).ConfigureAwait(false);
            if (state.ActiveSnapshotId == null) return null;
            string fullPath = ResolveFilePath(state.ActiveSnapshotId, path);
            if (!File.Exists(fullPath)) return null;
            return await File.ReadAllBytesAsync(fullPath, cancellationToken).ConfigureAwait(false);
        }

        private byte[] EncodePointer(StoredContentPointer pointer)
        {
            ContentVersionState state = pointer.State;
            byte[] snapshot = Encoding.ASCII.GetBytes(state.ActiveSnapshotId ?? string.Empty);
            byte[] versionBytes = Encoding.UTF8.GetBytes(state.ActiveVersion ?? string.Empty);
            byte[] previousSnapshot = Encoding.ASCII.GetBytes(pointer.PreviousSnapshotId ?? string.Empty);
            byte[] previousVersion = Encoding.UTF8.GetBytes(pointer.PreviousVersion ?? string.Empty);
            byte[] activeManifestHash = Encoding.ASCII.GetBytes(state.ActiveManifestHash ?? string.Empty);
            byte[] previousManifestHash = Encoding.ASCII.GetBytes(pointer.PreviousManifestHash ?? string.Empty);
            if (snapshot.Length > ushort.MaxValue || versionBytes.Length > ushort.MaxValue ||
                previousSnapshot.Length > ushort.MaxValue || previousVersion.Length > ushort.MaxValue ||
                activeManifestHash.Length > ushort.MaxValue || previousManifestHash.Length > ushort.MaxValue)
                throw new ArgumentException("内容版本标识过长。", nameof(pointer));
            if ((state.ActiveManifestHash != null && !IsValidManifestHash(state.ActiveManifestHash)) ||
                (pointer.PreviousManifestHash != null && !IsValidManifestHash(pointer.PreviousManifestHash)))
                throw new ArgumentException("清单哈希必须是小写 SHA-256 十六进制值。", nameof(pointer));
            var bytes = new byte[41 + snapshot.Length + versionBytes.Length + previousSnapshot.Length +
                previousVersion.Length + activeManifestHash.Length + previousManifestHash.Length + 32];
            Buffer.BlockCopy(PointerMagic, 0, bytes, 0, PointerMagic.Length);
            bytes[4] = 3;
            WriteUInt16(bytes, 5, snapshot.Length);
            WriteUInt16(bytes, 7, versionBytes.Length);
            WriteInt64(bytes, 9, state.ActiveSequence);
            WriteInt64(bytes, 17, state.HighestSequence);
            WriteUInt16(bytes, 25, previousSnapshot.Length);
            WriteUInt16(bytes, 27, previousVersion.Length);
            WriteInt64(bytes, 29, pointer.PreviousSequence);
            WriteUInt16(bytes, 37, activeManifestHash.Length);
            WriteUInt16(bytes, 39, previousManifestHash.Length);
            int offset = 41;
            Buffer.BlockCopy(snapshot, 0, bytes, offset, snapshot.Length);
            offset += snapshot.Length;
            Buffer.BlockCopy(versionBytes, 0, bytes, offset, versionBytes.Length);
            offset += versionBytes.Length;
            Buffer.BlockCopy(previousSnapshot, 0, bytes, offset, previousSnapshot.Length);
            offset += previousSnapshot.Length;
            Buffer.BlockCopy(previousVersion, 0, bytes, offset, previousVersion.Length);
            offset += previousVersion.Length;
            Buffer.BlockCopy(activeManifestHash, 0, bytes, offset, activeManifestHash.Length);
            offset += activeManifestHash.Length;
            Buffer.BlockCopy(previousManifestHash, 0, bytes, offset, previousManifestHash.Length);
            byte[] hash;
            using (SHA256 sha = SHA256.Create()) hash = sha.ComputeHash(bytes, 0, bytes.Length - 32);
            Buffer.BlockCopy(hash, 0, bytes, bytes.Length - 32, hash.Length);
            return bytes;
        }

        private static StoredContentPointer DecodePointer(byte[] bytes)
        {
            if (bytes.Length < 25 + 32 || bytes[0] != PointerMagic[0] || bytes[1] != PointerMagic[1] ||
                bytes[2] != PointerMagic[2] || bytes[3] != PointerMagic[3] || (bytes[4] < 1 || bytes[4] > 3))
                throw new ContentUpdateException("内容版本指针格式损坏。");
            int snapshotLength = ReadUInt16(bytes, 5);
            int versionLength = ReadUInt16(bytes, 7);
            int previousSnapshotLength = bytes[4] >= 2 ? ReadUInt16(bytes, 25) : 0;
            int previousVersionLength = bytes[4] >= 2 ? ReadUInt16(bytes, 27) : 0;
            int activeManifestHashLength = bytes[4] == 3 ? ReadUInt16(bytes, 37) : 0;
            int previousManifestHashLength = bytes[4] == 3 ? ReadUInt16(bytes, 39) : 0;
            int headerLength = bytes[4] == 3 ? 41 : bytes[4] == 2 ? 37 : 25;
            if (bytes.Length < headerLength + 32 ||
                bytes.Length != headerLength + snapshotLength + versionLength + previousSnapshotLength + previousVersionLength +
                    activeManifestHashLength + previousManifestHashLength + 32)
                throw new ContentUpdateException("内容版本指针长度无效。");
            byte[] hash;
            using (SHA256 sha = SHA256.Create()) hash = sha.ComputeHash(bytes, 0, bytes.Length - 32);
            int difference = 0;
            for (int index = 0; index < hash.Length; index++) difference |= hash[index] ^ bytes[bytes.Length - 32 + index];
            if (difference != 0) throw new ContentUpdateException("内容版本指针完整性校验失败。");
            long activeSequence = ReadInt64(bytes, 9);
            long highWaterMark = ReadInt64(bytes, 17);
            long previousSequence = bytes[4] >= 2 ? ReadInt64(bytes, 29) : 0;
            int offset = headerLength;
            string snapshot = Encoding.ASCII.GetString(bytes, offset, snapshotLength);
            offset += snapshotLength;
            string version = Encoding.UTF8.GetString(bytes, offset, versionLength);
            offset += versionLength;
            string? previousSnapshot = previousSnapshotLength == 0 ? null : Encoding.ASCII.GetString(bytes, offset, previousSnapshotLength);
            offset += previousSnapshotLength;
            string? previousVersion = previousVersionLength == 0 ? null : Encoding.UTF8.GetString(bytes, offset, previousVersionLength);
            offset += previousVersionLength;
            string? activeManifestHash = activeManifestHashLength == 0 ? null : Encoding.ASCII.GetString(bytes, offset, activeManifestHashLength);
            offset += activeManifestHashLength;
            string? previousManifestHash = previousManifestHashLength == 0 ? null : Encoding.ASCII.GetString(bytes, offset, previousManifestHashLength);
            if (!Guid.TryParseExact(snapshot, "N", out _) || string.IsNullOrWhiteSpace(version) || activeSequence <= 0 || highWaterMark < activeSequence)
                throw new ContentUpdateException("内容版本指针字段无效。");
            if (activeManifestHash != null && !IsValidManifestHash(activeManifestHash))
                throw new ContentUpdateException("内容版本指针中的活动清单哈希无效。");
            bool hasPrevious = previousSnapshot != null && previousVersion != null && previousSequence > 0 &&
                previousSequence < activeSequence && previousSequence <= highWaterMark && Guid.TryParseExact(previousSnapshot, "N", out _) &&
                !string.IsNullOrWhiteSpace(previousVersion) &&
                (previousManifestHash == null || IsValidManifestHash(previousManifestHash));
            bool hasNoPrevious = previousSnapshotLength == 0 && previousVersionLength == 0 && previousSequence == 0 &&
                previousManifestHashLength == 0;
            if (!hasPrevious && !hasNoPrevious) throw new ContentUpdateException("内容版本指针中的上一快照字段无效。");
            var state = new ContentVersionState(version, snapshot, activeSequence, highWaterMark, activeManifestHash);
            return new StoredContentPointer(state, hasPrevious ? previousVersion : null,
                hasPrevious ? previousSnapshot : null, hasPrevious ? previousSequence : 0,
                hasPrevious ? previousManifestHash : null);
        }

        private static bool IsValidManifestHash(string manifestHash)
        {
            if (manifestHash == null || manifestHash.Length != 64) return false;
            foreach (char character in manifestHash)
            {
                if (!(character >= '0' && character <= '9') && !(character >= 'a' && character <= 'f')) return false;
            }
            return true;
        }

        private string ResolveFilePath(string stagingId, string path)
        {
            ValidateStagingId(stagingId);
            if (string.IsNullOrEmpty(path) || path[0] == '/' || path.Contains("\\") || path.Contains(":")) throw new ArgumentException("内容文件路径无效。", nameof(path));
            foreach (string segment in path.Split('/'))
            {
                if (segment.Length == 0 || segment == "." || segment == "..") throw new ArgumentException("内容文件路径无效。", nameof(path));
                foreach (char character in segment)
                {
                    if (!(character >= 'a' && character <= 'z') && !(character >= 'A' && character <= 'Z') &&
                        !(character >= '0' && character <= '9') && character != '.' && character != '_' && character != '-')
                        throw new ArgumentException("内容文件路径必须使用 ASCII 路径字符。", nameof(path));
                }
            }
            string snapshot = Path.GetFullPath(Path.Combine(SnapshotPath(stagingId), "files"));
            string fullPath = Path.GetFullPath(Path.Combine(snapshot, path.Replace('/', Path.DirectorySeparatorChar)));
            string relative = Path.GetRelativePath(snapshot, fullPath);
            if (relative == ".." || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) || Path.IsPathRooted(relative))
                throw new ArgumentException("内容文件路径越出快照根目录。", nameof(path));
            return fullPath;
        }

        private string SnapshotPath(string stagingId) => Path.Combine(_snapshots, stagingId);
        private static void ValidateStagingId(string stagingId)
        {
            if (!Guid.TryParseExact(stagingId, "N", out _)) throw new ArgumentException("内容暂存标识格式无效。", nameof(stagingId));
        }
        private static void WriteUInt16(byte[] bytes, int offset, int value) { bytes[offset] = (byte)(value >> 8); bytes[offset + 1] = (byte)value; }
        private static int ReadUInt16(byte[] bytes, int offset) => bytes[offset] << 8 | bytes[offset + 1];
        private static void WriteInt64(byte[] bytes, int offset, long value) { for (int shift = 56; shift >= 0; shift -= 8) bytes[offset + (56 - shift) / 8] = (byte)(value >> shift); }
        private static long ReadInt64(byte[] bytes, int offset) { long value = 0; for (int index = 0; index < 8; index++) value = value << 8 | bytes[offset + index]; return value; }
    }

    /// <summary>固定响应的包源模拟实现，用于无网络自动化验证。</summary>
    public sealed class SimulationContentPackageSource : IContentPackageSource
    {
        private readonly System.Collections.Generic.Dictionary<string, byte[]> _files;

        public SimulationContentPackageSource(System.Collections.Generic.IReadOnlyDictionary<string, byte[]> files)
        {
            if (files == null) throw new ArgumentNullException(nameof(files));
            _files = new System.Collections.Generic.Dictionary<string, byte[]>(StringComparer.Ordinal);
            foreach (var file in files) _files.Add(file.Key, (byte[])(file.Value ?? throw new ArgumentException("模拟内容不能包含空值。", nameof(files))).Clone());
        }

        public ValueTask<byte[]> FetchAsync(string path, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!_files.TryGetValue(path, out byte[]? content)) throw new FileNotFoundException("模拟内容未配置。", path);
            return new ValueTask<byte[]>((byte[])content.Clone());
        }
    }
}
