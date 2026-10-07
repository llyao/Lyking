using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace Lyking.GameFramework.Client.Services.ContentUpdate
{
    /// <summary>先验签并校验所有暂存内容，再原子切换版本指针的更新事务。</summary>
    public sealed class ContentUpdateService
    {
        private readonly IContentVersionStore _store;
        private readonly IContentManifestTrustStore _trustStore;
        private readonly IRollbackAuthorizationVerifier? _rollbackVerifier;
        private readonly string _applicationId;
        private readonly string _platformId;
        private readonly Func<DateTimeOffset> _utcNow;
        private readonly SemaphoreSlim _gate = new(1, 1);

        public ContentUpdateService(IContentVersionStore store, IContentManifestTrustStore trustStore,
            IRollbackAuthorizationVerifier? rollbackVerifier, string applicationId, string platformId,
            Func<DateTimeOffset>? utcNow = null)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _trustStore = trustStore ?? throw new ArgumentNullException(nameof(trustStore));
            _rollbackVerifier = rollbackVerifier;
            _applicationId = string.IsNullOrWhiteSpace(applicationId) ? throw new ArgumentException("应用标识不能为空。", nameof(applicationId)) : applicationId;
            _platformId = string.IsNullOrWhiteSpace(platformId) ? throw new ArgumentException("平台标识不能为空。", nameof(platformId)) : platformId;
            _utcNow = utcNow ?? (() => DateTimeOffset.UtcNow);
        }

        public async ValueTask ApplyAsync(ContentManifest manifest, IContentPackageSource source,
            ContentRollbackAuthorization? rollbackAuthorization = null, CancellationToken cancellationToken = default)
        {
            if (manifest == null) throw new ArgumentNullException(nameof(manifest));
            if (source == null) throw new ArgumentNullException(nameof(source));
            await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                byte[] canonical;
                try { canonical = ContentManifestCodec.EncodeCanonical(manifest); }
                catch (ContentUpdateException) { throw; }
                catch (Exception exception) { throw new ContentManifestFormatException($"清单规范化失败：{exception.Message}"); }

                byte[] manifestHashBytes;
                using (SHA256 sha = SHA256.Create()) manifestHashBytes = sha.ComputeHash(canonical);
                string manifestHash = ToLowerHex(manifestHashBytes);

                if (!string.Equals(manifest.ApplicationId, _applicationId, StringComparison.Ordinal) ||
                    !string.Equals(manifest.PlatformId, _platformId, StringComparison.Ordinal))
                    throw new ContentManifestRejectedException("清单应用或平台标识不匹配。");
                DateTimeOffset now = _utcNow().ToUniversalTime();
                if (now < manifest.NotBeforeUtc.ToUniversalTime() || now >= manifest.ExpiresAtUtc.ToUniversalTime())
                    throw new ContentManifestRejectedException("清单尚未生效或已过期。");
                byte[] signature = manifest.Signature;
                if (signature.Length == 0 || signature.Length > 128 ||
                    !_trustStore.VerifySignature(manifest.SigningKeyId, manifest.Sequence, canonical, signature))
                    throw new ContentManifestRejectedException("清单签名无效或签名密钥不受信。");

                ContentVersionState state = await _store.ReadStateAsync(cancellationToken).ConfigureAwait(false);
                bool replay = manifest.Sequence <= state.HighestSequence;
                bool sameActiveVersion = state.ActiveSequence == manifest.Sequence && state.ActiveVersion == manifest.Version;
                if (!manifest.IsRecovery && sameActiveVersion)
                {
                    if (string.Equals(state.ActiveManifestHash, manifestHash, StringComparison.Ordinal)) return;
                    throw new ContentManifestRejectedException("活动序号和版本对应不同的清单内容，已拒绝冲突重放。");
                }
                if (replay)
                    throw new ContentManifestRejectedException("清单序号不是单调递增值，已拒绝回放。");
                if (manifest.IsRecovery || replay)
                {
                    if (!manifest.IsRecovery || rollbackAuthorization == null || _rollbackVerifier == null)
                        throw new ContentManifestRejectedException("恢复清单必须具有有效的外部恢复授权。");
                    bool authorized = await _rollbackVerifier.ValidateAsync(rollbackAuthorization, _applicationId, _platformId, state, manifest, cancellationToken).ConfigureAwait(false);
                    if (!authorized) throw new ContentManifestRejectedException("外部恢复授权无效。");
                }

                string stagingId = Guid.NewGuid().ToString("N");
                bool activated = false;
                IContentKeyRotationTransaction? rotationTransaction = null;
                try
                {
                    await _store.BeginStagingAsync(stagingId, manifest.Version, manifest.Sequence, cancellationToken).ConfigureAwait(false);
                    foreach (ContentFileEntry file in manifest.Files)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        byte[] downloaded = await source.FetchAsync(file.Path, cancellationToken).ConfigureAwait(false)
                            ?? throw new ContentUpdateException($"内容源返回空文件：{file.Path}。");
                        if (downloaded.LongLength != file.Length || !HasExpectedHash(downloaded, file.Sha256)) throw new ContentBlockIntegrityException(file.Path);
                        await _store.WriteStagedFileAsync(stagingId, file.Path, downloaded, cancellationToken).ConfigureAwait(false);
                        byte[]? staged = await _store.ReadStagedFileAsync(stagingId, file.Path, cancellationToken).ConfigureAwait(false);
                        if (staged == null || staged.LongLength != file.Length || !HasExpectedHash(staged, file.Sha256))
                            throw new ContentBlockIntegrityException(file.Path);
                    }

                    foreach (ContentKeyRotation rotation in manifest.Rotations)
                    {
                        if (rotation.ActivateAtSequence <= manifest.Sequence)
                            throw new ContentManifestFormatException($"轮换密钥必须在后续序号生效：{rotation.KeyId}。");
                    }
                    cancellationToken.ThrowIfCancellationRequested();
                    if (manifest.Rotations.Count > 0)
                    {
                        try
                        {
                            rotationTransaction = _trustStore.PrepareRotations(manifest.Rotations);
                            rotationTransaction.Publish();
                        }
                        catch (ContentManifestFormatException)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            if (!manifest.IsRecovery)
                            {
                                ContentVersionState latestState = await _store.ReadStateAsync(cancellationToken).ConfigureAwait(false);
                                if (IsSameActiveManifest(latestState, manifest, manifestHash)) return;
                            }

                            throw;
                        }
                    }
                    bool activatedNow = _store.ActivateStaged(stagingId, manifest.Version, manifest.Sequence, manifestHash);
                    if (activatedNow) activated = true;
                    rotationTransaction?.Complete();
                    if (!activatedNow) return;
                }
                finally
                {
                    rotationTransaction?.Dispose();
                    if (!activated)
                    {
                        try { await _store.DiscardStagedAsync(stagingId, CancellationToken.None).ConfigureAwait(false); }
                        catch { }
                    }
                }
            }
            finally
            {
                _gate.Release();
            }
        }

        private static bool HasExpectedHash(byte[] content, string expected)
        {
            byte[] hash;
            using (SHA256 sha = SHA256.Create()) hash = sha.ComputeHash(content);
            int difference = 0;
            for (int index = 0; index < hash.Length; index++)
            {
                int high = Hex(expected[index * 2]);
                int low = Hex(expected[index * 2 + 1]);
                difference |= hash[index] ^ (high << 4 | low);
            }
            return difference == 0;
        }

        private static string ToLowerHex(byte[] bytes)
        {
            const string digits = "0123456789abcdef";
            var characters = new char[bytes.Length * 2];
            for (int index = 0; index < bytes.Length; index++)
            {
                characters[index * 2] = digits[bytes[index] >> 4];
                characters[index * 2 + 1] = digits[bytes[index] & 0x0f];
            }

            return new string(characters);
        }

        private static bool IsSameActiveManifest(ContentVersionState state, ContentManifest manifest, string manifestHash) =>
            state.ActiveSequence == manifest.Sequence
            && string.Equals(state.ActiveVersion, manifest.Version, StringComparison.Ordinal)
            && string.Equals(state.ActiveManifestHash, manifestHash, StringComparison.Ordinal);

        private static int Hex(char value) => value >= '0' && value <= '9' ? value - '0' : value - 'a' + 10;
    }
}
