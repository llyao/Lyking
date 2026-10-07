using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Lyking.GameFramework.Client.Services.Persistence
{
    /// <summary>同目录暂存并通过文件系统原子重命名替换存档的适配器。</summary>
    public sealed class FileAtomicSaveStore : IAtomicSaveStore
    {
        private readonly string _root;

        public FileAtomicSaveStore(string rootDirectory)
        {
            if (string.IsNullOrWhiteSpace(rootDirectory)) throw new ArgumentException("存档根目录不能为空。", nameof(rootDirectory));
            _root = Path.GetFullPath(rootDirectory);
            Directory.CreateDirectory(_root);
        }

        public async ValueTask<byte[]?> ReadCurrentAsync(string key, CancellationToken cancellationToken)
        {
            string path = CurrentPath(key);
            if (!File.Exists(path)) return null;
            return await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
        }

        public async ValueTask WriteTemporaryAsync(string key, string operationId, byte[] data, CancellationToken cancellationToken)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            string path = TemporaryPath(key, operationId);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, useAsync: true))
            {
                await stream.WriteAsync(data, 0, data.Length, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                stream.Flush(true);
            }
        }

        public async ValueTask<byte[]?> ReadTemporaryAsync(string key, string operationId, CancellationToken cancellationToken)
        {
            string path = TemporaryPath(key, operationId);
            if (!File.Exists(path)) return null;
            return await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
        }

        public ValueTask ActivateTemporaryAsync(string key, string operationId)
        {
            string source = TemporaryPath(key, operationId);
            string destination = CurrentPath(key);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            if (File.Exists(destination)) File.Replace(source, destination, null);
            else File.Move(source, destination);
            return default;
        }

        public ValueTask DiscardTemporaryAsync(string key, string operationId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string path = TemporaryPath(key, operationId);
            if (File.Exists(path)) File.Delete(path);
            return default;
        }

        private string CurrentPath(string key) => Path.Combine(_root, KeyHash(key) + ".save");
        private string TemporaryPath(string key, string operationId)
        {
            if (!Guid.TryParseExact(operationId, "N", out _)) throw new ArgumentException("存档操作标识格式无效。", nameof(operationId));
            return Path.Combine(_root, KeyHash(key) + "." + operationId + ".tmp");
        }

        private static string KeyHash(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("存档键不能为空。", nameof(key));
            byte[] bytes;
            using (SHA256 sha = SHA256.Create()) bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(key));
            var builder = new StringBuilder(bytes.Length * 2);
            foreach (byte value in bytes) builder.Append(value.ToString("x2", System.Globalization.CultureInfo.InvariantCulture));
            return builder.ToString();
        }
    }
}
