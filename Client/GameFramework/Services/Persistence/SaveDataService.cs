using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Lyking.GameFramework.Client.Services.Persistence
{
    /// <summary>版本化存档门面；迁移成功后自动写入最新格式，失败时当前记录保持原样。</summary>
    public sealed class SaveDataService
    {
        private readonly IAtomicSaveStore _store;
        private readonly ISaveDataProtector _protector;
        private readonly Dictionary<int, ISaveDataMigrator> _migrators = new();
        private readonly SemaphoreSlim _gate = new(1, 1);

        public SaveDataService(IAtomicSaveStore store, ISaveDataProtector protector, IEnumerable<ISaveDataMigrator>? migrators = null)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _protector = protector ?? throw new ArgumentNullException(nameof(protector));
            if (migrators == null) return;
            foreach (ISaveDataMigrator migrator in migrators)
            {
                if (migrator == null) throw new ArgumentException("迁移器集合不能包含空值。", nameof(migrators));
                if (migrator.FromVersion <= 0 || migrator.ToVersion != migrator.FromVersion + 1)
                    throw new ArgumentException("迁移器必须从正版本迁移到紧邻的下一版本。", nameof(migrators));
                if (!_migrators.TryAdd(migrator.FromVersion, migrator))
                    throw new ArgumentException($"存档版本 {migrator.FromVersion} 存在重复迁移器。", nameof(migrators));
            }
        }

        public async ValueTask SaveAsync(string key, int payloadVersion, byte[] payload, CancellationToken cancellationToken = default)
        {
            ValidateArguments(key, payloadVersion, payload);
            await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await SaveUnlockedAsync(key, payloadVersion, payload, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _gate.Release();
            }
        }

        public async ValueTask<byte[]> LoadAsync(string key, int currentPayloadVersion, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("存档键不能为空。", nameof(key));
            if (currentPayloadVersion <= 0) throw new ArgumentOutOfRangeException(nameof(currentPayloadVersion));
            await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                byte[]? envelope = await _store.ReadCurrentAsync(key, cancellationToken).ConfigureAwait(false);
                if (envelope == null) throw new SaveRecordNotFoundException(key);
                byte[] payload = SaveEnvelopeCodec.Decode(key, envelope, _protector, out int version);
                if (version > currentPayloadVersion) throw new UnsupportedSaveVersionException(version, currentPayloadVersion);
                if (version == currentPayloadVersion) return payload;

                int originalVersion = version;
                while (version < currentPayloadVersion)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!_migrators.TryGetValue(version, out ISaveDataMigrator? migrator))
                        throw new SaveDataException($"缺少存档版本 {version} 到 {version + 1} 的迁移器。");
                    payload = await migrator.MigrateAsync((byte[])payload.Clone(), cancellationToken).ConfigureAwait(false)
                        ?? throw new SaveDataException($"存档版本 {version} 的迁移器返回空数据。");
                    version = migrator.ToVersion;
                }

                if (version != currentPayloadVersion) throw new SaveDataException("存档迁移链未到达当前格式版本。");
                cancellationToken.ThrowIfCancellationRequested();
                await SaveUnlockedAsync(key, version, payload, cancellationToken).ConfigureAwait(false);
                return payload;
            }
            finally
            {
                _gate.Release();
            }
        }

        private async ValueTask SaveUnlockedAsync(string key, int payloadVersion, byte[] payload, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string operationId = Guid.NewGuid().ToString("N");
            bool activated = false;
            try
            {
                byte[] envelope = SaveEnvelopeCodec.Encode(key, payloadVersion, payload, _protector);
                await _store.WriteTemporaryAsync(key, operationId, envelope, cancellationToken).ConfigureAwait(false);
                byte[]? staged = await _store.ReadTemporaryAsync(key, operationId, cancellationToken).ConfigureAwait(false);
                if (staged == null) throw new SaveDataException("存档暂存记录写入后无法读取。");
                byte[] verified = SaveEnvelopeCodec.Decode(key, staged, _protector, out int verifiedVersion);
                if (verifiedVersion != payloadVersion || !EqualBytes(verified, payload))
                    throw new SaveDataIntegrityException("存档暂存内容与待写入数据不一致。");
                cancellationToken.ThrowIfCancellationRequested();
                await _store.ActivateTemporaryAsync(key, operationId).ConfigureAwait(false);
                activated = true;
            }
            finally
            {
                if (!activated)
                {
                    try { await _store.DiscardTemporaryAsync(key, operationId, CancellationToken.None).ConfigureAwait(false); }
                    catch { }
                }
            }
        }

        private static void ValidateArguments(string key, int payloadVersion, byte[] payload)
        {
            if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("存档键不能为空。", nameof(key));
            if (payloadVersion <= 0) throw new ArgumentOutOfRangeException(nameof(payloadVersion));
            if (payload == null) throw new ArgumentNullException(nameof(payload));
        }

        private static bool EqualBytes(byte[] left, byte[] right)
        {
            if (left.Length != right.Length) return false;
            int difference = 0;
            for (int index = 0; index < left.Length; index++) difference |= left[index] ^ right[index];
            return difference == 0;
        }
    }
}
