using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace Lyking.GameFramework.Client.Services.ContentUpdate
{
    public sealed class ContentFileEntry
    {
        public ContentFileEntry(string path, string sha256, long length)
        {
            Path = path ?? throw new ArgumentNullException(nameof(path));
            Sha256 = sha256 ?? throw new ArgumentNullException(nameof(sha256));
            Length = length;
        }
        public string Path { get; }
        public string Sha256 { get; }
        public long Length { get; }
    }

    /// <summary>由现有受信签名密钥签发的公钥轮换声明。</summary>
    public sealed class ContentKeyRotation
    {
        private readonly byte[] _subjectPublicKeyInfo;
        public ContentKeyRotation(string keyId, byte[] subjectPublicKeyInfo, long activateAtSequence, long retireAtSequence,
            string? retireKeyId = null, long? retireExistingKeyAtSequence = null)
        {
            KeyId = keyId ?? throw new ArgumentNullException(nameof(keyId));
            _subjectPublicKeyInfo = (byte[])(subjectPublicKeyInfo ?? throw new ArgumentNullException(nameof(subjectPublicKeyInfo))).Clone();
            ActivateAtSequence = activateAtSequence;
            RetireAtSequence = retireAtSequence;
            RetireKeyId = retireKeyId;
            RetireExistingKeyAtSequence = retireExistingKeyAtSequence;
        }
        public string KeyId { get; }
        public byte[] SubjectPublicKeyInfo => (byte[])_subjectPublicKeyInfo.Clone();
        public long ActivateAtSequence { get; }
        public long RetireAtSequence { get; }
        public string? RetireKeyId { get; }
        public long? RetireExistingKeyAtSequence { get; }
    }

    public sealed class ContentManifest
    {
        private readonly ContentFileEntry[] _files;
        private readonly ContentKeyRotation[] _rotations;
        private readonly byte[] _signature;

        public ContentManifest(string applicationId, string platformId, string version, long sequence,
            DateTimeOffset notBeforeUtc, DateTimeOffset expiresAtUtc, string signingKeyId,
            IReadOnlyList<ContentFileEntry> files, IReadOnlyList<ContentKeyRotation>? rotations = null,
            bool isRecovery = false, byte[]? signature = null)
        {
            ApplicationId = applicationId ?? throw new ArgumentNullException(nameof(applicationId));
            PlatformId = platformId ?? throw new ArgumentNullException(nameof(platformId));
            Version = version ?? throw new ArgumentNullException(nameof(version));
            Sequence = sequence;
            NotBeforeUtc = notBeforeUtc;
            ExpiresAtUtc = expiresAtUtc;
            SigningKeyId = signingKeyId ?? throw new ArgumentNullException(nameof(signingKeyId));
            if (files == null) throw new ArgumentNullException(nameof(files));
            _files = new ContentFileEntry[files.Count];
            for (int index = 0; index < files.Count; index++) _files[index] = files[index] ?? throw new ArgumentException("清单文件项不能包含空值。", nameof(files));
            _rotations = new ContentKeyRotation[rotations?.Count ?? 0];
            for (int index = 0; index < _rotations.Length; index++) _rotations[index] = rotations![index] ?? throw new ArgumentException("密钥轮换项不能包含空值。", nameof(rotations));
            IsRecovery = isRecovery;
            _signature = (byte[])(signature ?? Array.Empty<byte>()).Clone();
        }

        public string ApplicationId { get; }
        public string PlatformId { get; }
        public string Version { get; }
        public long Sequence { get; }
        public DateTimeOffset NotBeforeUtc { get; }
        public DateTimeOffset ExpiresAtUtc { get; }
        public string SigningKeyId { get; }
        public IReadOnlyList<ContentFileEntry> Files => Array.AsReadOnly(_files);
        public IReadOnlyList<ContentKeyRotation> Rotations => Array.AsReadOnly(_rotations);
        public bool IsRecovery { get; }
        public byte[] Signature => (byte[])_signature.Clone();

        public ContentManifest WithSignature(byte[] signature) => new(
            ApplicationId, PlatformId, Version, Sequence, NotBeforeUtc, ExpiresAtUtc, SigningKeyId, _files, _rotations, IsRecovery, signature);
    }

    public sealed class ContentVersionState
    {
        public ContentVersionState(string? activeVersion, string? activeSnapshotId, long activeSequence, long highestSequence,
            string? activeManifestHash = null)
        {
            ActiveVersion = activeVersion;
            ActiveSnapshotId = activeSnapshotId;
            ActiveSequence = activeSequence;
            HighestSequence = highestSequence;
            ActiveManifestHash = activeManifestHash;
        }
        public string? ActiveVersion { get; }
        public string? ActiveSnapshotId { get; }
        public long ActiveSequence { get; }
        public long HighestSequence { get; }
        public string? ActiveManifestHash { get; }
    }

    /// <summary>暂存一个完整内容快照，并通过原子版本指针切换当前内容。</summary>
    public interface IContentVersionStore
    {
        ValueTask<ContentVersionState> ReadStateAsync(CancellationToken cancellationToken);
        ValueTask BeginStagingAsync(string stagingId, string version, long sequence, CancellationToken cancellationToken);
        ValueTask WriteStagedFileAsync(string stagingId, string path, byte[] content, CancellationToken cancellationToken);
        ValueTask<byte[]?> ReadStagedFileAsync(string stagingId, string path, CancellationToken cancellationToken);
        /// <returns>已切换时返回 <see langword="true"/>；已激活完全相同的版本时返回 <see langword="false"/>。</returns>
        bool ActivateStaged(string stagingId, string version, long sequence, string manifestHash);
        ValueTask DiscardStagedAsync(string stagingId, CancellationToken cancellationToken);
        ValueTask<byte[]?> ReadActiveFileAsync(string path, CancellationToken cancellationToken);
    }

    public interface IContentPackageSource
    {
        ValueTask<byte[]> FetchAsync(string path, CancellationToken cancellationToken);
    }

    /// <summary>
    /// 清单公钥信任根由应用或平台可信配置提供。生产实现须在进程重启后恢复已提交的密钥轮换，
    /// 并保证轮换只在内容版本指针激活后持久生效。
    /// </summary>
    public interface IContentManifestTrustStore
    {
        bool VerifySignature(string keyId, long sequence, byte[] canonicalData, byte[] signature);
        IContentKeyRotationTransaction PrepareRotations(IReadOnlyList<ContentKeyRotation> rotations);
    }

    /// <summary>在版本指针激活期间暂存信任变更；未完成时释放会恢复旧信任映射。</summary>
    public interface IContentKeyRotationTransaction : IDisposable
    {
        void Publish();
        void Complete();
    }

    /// <summary>恢复授权应由服务端签名令牌、一次性挑战或等价可信机制实现。</summary>
    public interface IRollbackAuthorizationVerifier
    {
        ValueTask<bool> ValidateAsync(ContentRollbackAuthorization authorization, string applicationId, string platformId,
            ContentVersionState currentState, ContentManifest manifest, CancellationToken cancellationToken);
    }

    public sealed class ContentRollbackAuthorization
    {
        private readonly byte[] _proof;
        public ContentRollbackAuthorization(byte[] proof) => _proof = (byte[])(proof ?? throw new ArgumentNullException(nameof(proof))).Clone();
        public byte[] Proof => (byte[])_proof.Clone();
    }

    public class ContentUpdateException : Exception
    {
        public ContentUpdateException(string message) : base(message) { }
        public ContentUpdateException(string message, Exception innerException) : base(message, innerException) { }
    }

    public sealed class ContentManifestRejectedException : ContentUpdateException
    {
        public ContentManifestRejectedException(string message) : base(message) { }
    }

    public sealed class ContentBlockIntegrityException : ContentUpdateException
    {
        public ContentBlockIntegrityException(string path) : base($"内容块校验失败：{path}。") { }
    }

    public sealed class ContentManifestFormatException : ContentUpdateException
    {
        public ContentManifestFormatException(string message) : base(message) { }
    }

    /// <summary>进程内 ECDSA 信任存储；仅供测试和无头演示，不持久化密钥轮换。</summary>
    public sealed class EcdsaContentManifestTrustStore : IContentManifestTrustStore
    {
        private const string P256CurveOid = "1.2.840.10045.3.1.7";
        private const int P256IeeeP1363SignatureLength = 64;
        private readonly SemaphoreSlim _gate = new(1, 1);
        private Dictionary<string, TrustedKey> _keys = new(StringComparer.Ordinal);

        /// <summary>注册随应用可信配置提供的 ECDSA 公钥；此实例不取得密钥对象所有权。</summary>
        public void AddTrustedRoot(string keyId, ECDsa publicKey, long activateAtSequence = 0, long retireAtSequence = long.MaxValue)
        {
            if (string.IsNullOrWhiteSpace(keyId)) throw new ArgumentException("密钥标识不能为空。", nameof(keyId));
            if (publicKey == null) throw new ArgumentNullException(nameof(publicKey));
            if (!IsP256Key(publicKey)) throw new ArgumentException("清单信任密钥必须使用 ECDSA P-256。", nameof(publicKey));
            if (activateAtSequence < 0 || retireAtSequence < activateAtSequence) throw new ArgumentOutOfRangeException(nameof(activateAtSequence));
            _gate.Wait();
            try
            {
                if (_keys.ContainsKey(keyId)) throw new ArgumentException($"受信密钥已存在：{keyId}。", nameof(keyId));
                _keys.Add(keyId, new TrustedKey(keyId, publicKey, activateAtSequence, retireAtSequence));
            }
            finally { _gate.Release(); }
        }

        public bool VerifySignature(string keyId, long sequence, byte[] canonicalData, byte[] signature)
        {
            if (canonicalData == null) throw new ArgumentNullException(nameof(canonicalData));
            if (signature == null) throw new ArgumentNullException(nameof(signature));
            if (signature.Length != P256IeeeP1363SignatureLength) return false;
            _gate.Wait();
            try
            {
                if (!_keys.TryGetValue(keyId, out TrustedKey? key) || sequence < key.ActivateAtSequence || sequence > key.RetireAtSequence) return false;
                try { return key.PublicKey.VerifyData(canonicalData, signature, HashAlgorithmName.SHA256); }
                catch (CryptographicException) { return false; }
            }
            finally { _gate.Release(); }
        }

        public IContentKeyRotationTransaction PrepareRotations(IReadOnlyList<ContentKeyRotation> rotations)
        {
            if (rotations == null) throw new ArgumentNullException(nameof(rotations));
            _gate.Wait();
            var additions = new List<TrustedKey>();
            try
            {
                var retirements = new List<KeyValuePair<TrustedKey, long>>();
                var newKeys = new HashSet<string>(StringComparer.Ordinal);
                var retiringIds = new HashSet<string>(StringComparer.Ordinal);
                var prepared = new Dictionary<string, TrustedKey>(_keys, StringComparer.Ordinal);
                try
                {
                    foreach (ContentKeyRotation rotation in rotations)
                    {
                        if (rotation == null || string.IsNullOrWhiteSpace(rotation.KeyId) || rotation.ActivateAtSequence < 0 || rotation.RetireAtSequence < rotation.ActivateAtSequence)
                            throw new ContentManifestFormatException("清单密钥轮换字段无效。");
                        if (_keys.ContainsKey(rotation.KeyId) || !newKeys.Add(rotation.KeyId))
                            throw new ContentManifestFormatException($"清单重复轮换已受信密钥：{rotation.KeyId}。");
                        if (rotation.RetireKeyId != null)
                        {
                            if (!retiringIds.Add(rotation.RetireKeyId))
                                throw new ContentManifestFormatException($"清单重复退役密钥：{rotation.RetireKeyId}。");
                            if (!prepared.TryGetValue(rotation.RetireKeyId, out TrustedKey? retiringKey) ||
                                rotation.RetireExistingKeyAtSequence == null || rotation.RetireExistingKeyAtSequence.Value <= retiringKey.ActivateAtSequence)
                                throw new ContentManifestFormatException($"待退役密钥或退役序号无效：{rotation.RetireKeyId}。");
                            retirements.Add(new KeyValuePair<TrustedKey, long>(retiringKey, rotation.RetireExistingKeyAtSequence.Value));
                        }
                        else if (rotation.RetireExistingKeyAtSequence != null)
                        {
                            throw new ContentManifestFormatException("指定退役序号时必须同时指定待退役密钥。");
                        }
                        ECDsa publicKey = ECDsa.Create();
                        try
                        {
                            byte[] subjectPublicKeyInfo = rotation.SubjectPublicKeyInfo;
                            publicKey.ImportSubjectPublicKeyInfo(subjectPublicKeyInfo, out int bytesRead);
                            if (bytesRead != subjectPublicKeyInfo.Length || !IsP256Key(publicKey))
                                throw new CryptographicException("公钥数据必须是完整的 ECDSA P-256 SubjectPublicKeyInfo。");
                            var trusted = new TrustedKey(rotation.KeyId, publicKey, rotation.ActivateAtSequence, rotation.RetireAtSequence, ownsKey: true);
                            additions.Add(trusted);
                            prepared.Add(rotation.KeyId, trusted);
                        }
                        catch
                        {
                            publicKey.Dispose();
                            throw new ContentManifestFormatException($"密钥轮换公钥无效：{rotation.KeyId}。");
                        }
                    }

                    foreach (var retirement in retirements)
                    {
                        prepared[retirement.Key.KeyId] = retirement.Key.WithRetirement(retirement.Value);
                    }
                    return new RotationTransaction(this, _keys, prepared, additions);
                }
                catch
                {
                    foreach (TrustedKey addition in additions) addition.DisposeIfOwned();
                    throw;
                }
            }
            catch
            {
                _gate.Release();
                throw;
            }
        }

        private sealed class TrustedKey
        {
            private readonly bool _ownsKey;
            public TrustedKey(string keyId, ECDsa publicKey, long activateAtSequence, long retireAtSequence, bool ownsKey = false)
            {
                KeyId = keyId;
                PublicKey = publicKey;
                ActivateAtSequence = activateAtSequence;
                RetireAtSequence = retireAtSequence;
                _ownsKey = ownsKey;
            }
            public string KeyId { get; }
            public ECDsa PublicKey { get; }
            public long ActivateAtSequence { get; }
            public long RetireAtSequence { get; }
            public TrustedKey WithRetirement(long sequence) => new(KeyId, PublicKey, ActivateAtSequence, Math.Min(RetireAtSequence, sequence), _ownsKey);
            public void DisposeIfOwned() { if (_ownsKey) PublicKey.Dispose(); }
        }

        private static bool IsP256Key(ECDsa publicKey)
        {
            if (publicKey.KeySize != 256) return false;
            try
            {
                return string.Equals(publicKey.ExportParameters(false).Curve.Oid.Value, P256CurveOid, StringComparison.Ordinal);
            }
            catch (CryptographicException)
            {
                return false;
            }
        }

        private sealed class RotationTransaction : IContentKeyRotationTransaction
        {
            private readonly EcdsaContentManifestTrustStore _owner;
            private readonly Dictionary<string, TrustedKey> _oldKeys;
            private readonly Dictionary<string, TrustedKey> _preparedKeys;
            private readonly List<TrustedKey> _additions;
            private bool _published;
            private bool _disposed;

            public RotationTransaction(EcdsaContentManifestTrustStore owner, Dictionary<string, TrustedKey> oldKeys,
                Dictionary<string, TrustedKey> preparedKeys, List<TrustedKey> additions)
            {
                _owner = owner; _oldKeys = oldKeys; _preparedKeys = preparedKeys; _additions = additions;
            }

            public void Publish()
            {
                if (_disposed || _published) throw new InvalidOperationException("密钥轮换事务状态无效。");
                _owner._keys = _preparedKeys;
                _published = true;
            }

            public void Complete()
            {
                if (_disposed || !_published) throw new InvalidOperationException("密钥轮换事务尚未发布。");
                _disposed = true;
                _owner._gate.Release();
            }

            public void Dispose()
            {
                if (_disposed) return;
                if (_published) _owner._keys = _oldKeys;
                foreach (TrustedKey addition in _additions) addition.DisposeIfOwned();
                _disposed = true;
                _owner._gate.Release();
            }
        }
    }
}
