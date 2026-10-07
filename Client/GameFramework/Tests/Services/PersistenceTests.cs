using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Lyking.GameFramework.Client.Services.Persistence;
using Xunit;

namespace Lyking.GameFramework.Client.Tests.Services;

public sealed class PersistenceTests
{
    [Fact]
    public async Task 文件存储以同目录替换保留可读存档()
    {
        string root = Path.Combine(Path.GetTempPath(), "lyking-save-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new FileAtomicSaveStore(root);
            var service = new SaveDataService(store, new IdentitySaveDataProtector());
            await service.SaveAsync("profile/main", 1, new byte[] { 7 });
            await service.SaveAsync("profile/main", 1, new byte[] { 8, 9 });

            Assert.Equal(new byte[] { 8, 9 }, await service.LoadAsync("profile/main", 1));
            Assert.Single(Directory.GetFiles(root, "*.save"));
            Assert.Empty(Directory.GetFiles(root, "*.tmp"));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task 存档自动逐版迁移并以原子写入升级()
    {
        var store = new MemorySaveStore();
        var service = new SaveDataService(store, new IdentitySaveDataProtector(), new[] { new AppendMigration(1, 2, 0x2A) });
        await service.SaveAsync("profile/main", 1, new byte[] { 1, 2, 3 });

        byte[] migrated = await service.LoadAsync("profile/main", 2);

        Assert.Equal(new byte[] { 1, 2, 3, 0x2A }, migrated);
        byte[] upgradedEnvelope = (await store.ReadCurrentAsync("profile/main", CancellationToken.None))!;
        Assert.Equal(2, SaveEnvelopeCodec.ReadPayloadVersion(upgradedEnvelope));
    }

    [Fact]
    public async Task 自定义保护器收到记录键并完成存档往返()
    {
        var store = new MemorySaveStore();
        var protector = new XorSaveProtector();
        var service = new SaveDataService(store, protector);
        byte[] payload = { 0x01, 0x7F, 0xA5 };

        await service.SaveAsync("account/slot", 1, payload);
        byte[] envelope = (await store.ReadCurrentAsync("account/slot", CancellationToken.None))!;

        Assert.Equal("account/slot", protector.LastKey);
        Assert.Equal(protector.Protect("account/slot", payload), envelope.Skip(14).Take(payload.Length));
        Assert.Equal(payload, await service.LoadAsync("account/slot", 1));
    }

    [Fact]
    public async Task 迁移失败时保留原始记录()
    {
        var store = new MemorySaveStore();
        var service = new SaveDataService(store, new IdentitySaveDataProtector(), new[] { new ThrowingMigration(1, 2) });
        await service.SaveAsync("slot", 1, new byte[] { 8, 9 });
        byte[] original = (await store.ReadCurrentAsync("slot", CancellationToken.None))!;

        await Assert.ThrowsAsync<InvalidOperationException>(async () => await service.LoadAsync("slot", 2));

        Assert.Equal(original, await store.ReadCurrentAsync("slot", CancellationToken.None)!);
    }

    [Fact]
    public async Task 未知新版本和信封损坏都拒绝且不覆盖旧记录()
    {
        var store = new MemorySaveStore();
        var service = new SaveDataService(store, new IdentitySaveDataProtector());
        await service.SaveAsync("slot", 3, new byte[] { 4, 5 });
        byte[] future = (await store.ReadCurrentAsync("slot", CancellationToken.None))!;

        await Assert.ThrowsAsync<UnsupportedSaveVersionException>(async () => await service.LoadAsync("slot", 2));
        Assert.Equal(future, await store.ReadCurrentAsync("slot", CancellationToken.None)!);

        future[future.Length - 1] ^= 0xFF;
        store.SetCurrent("slot", future);
        await Assert.ThrowsAsync<SaveDataIntegrityException>(async () => await service.LoadAsync("slot", 3));
        Assert.Equal(future, await store.ReadCurrentAsync("slot", CancellationToken.None)!);
    }

    [Fact]
    public async Task 写入取消发生在提交前时旧存档仍可读()
    {
        var store = new MemorySaveStore();
        var service = new SaveDataService(store, new IdentitySaveDataProtector());
        await service.SaveAsync("slot", 1, new byte[] { 1 });
        byte[] original = (await store.ReadCurrentAsync("slot", CancellationToken.None))!;
        using var cancellation = new CancellationTokenSource();
        store.AfterReadTemporary = cancellation.Cancel;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await service.SaveAsync("slot", 1, new byte[] { 2 }, cancellation.Token));

        Assert.Equal(original, await store.ReadCurrentAsync("slot", CancellationToken.None)!);
        Assert.Equal(new byte[] { 1 }, await service.LoadAsync("slot", 1));
    }

    private sealed class AppendMigration : ISaveDataMigrator
    {
        public AppendMigration(int fromVersion, int toVersion, byte suffix) { FromVersion = fromVersion; ToVersion = toVersion; Suffix = suffix; }
        public int FromVersion { get; }
        public int ToVersion { get; }
        public byte Suffix { get; }
        public ValueTask<byte[]> MigrateAsync(byte[] payload, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = new byte[payload.Length + 1];
            Array.Copy(payload, result, payload.Length);
            result[result.Length - 1] = Suffix;
            return new ValueTask<byte[]>(result);
        }
    }

    private sealed class ThrowingMigration : ISaveDataMigrator
    {
        public ThrowingMigration(int fromVersion, int toVersion) { FromVersion = fromVersion; ToVersion = toVersion; }
        public int FromVersion { get; }
        public int ToVersion { get; }
        public ValueTask<byte[]> MigrateAsync(byte[] payload, CancellationToken cancellationToken) => throw new InvalidOperationException("迁移拒绝");
    }

    private sealed class XorSaveProtector : ISaveDataProtector
    {
        public string? LastKey { get; private set; }
        public byte[] Protect(string key, byte[] plainData)
        {
            LastKey = key;
            return Transform(plainData);
        }
        public byte[] Unprotect(string key, byte[] protectedData)
        {
            LastKey = key;
            return Transform(protectedData);
        }
        private static byte[] Transform(byte[] data)
        {
            var result = (byte[])data.Clone();
            for (int index = 0; index < result.Length; index++) result[index] ^= 0xFF;
            return result;
        }
    }

    private sealed class MemorySaveStore : IAtomicSaveStore
    {
        private readonly Dictionary<string, byte[]> _current = new();
        private readonly Dictionary<string, byte[]> _temporary = new();
        public bool CancelBeforeActivate { get; set; }
        public Action? AfterReadTemporary { get; set; }
        public void SetCurrent(string key, byte[] value) => _current[key] = (byte[])value.Clone();
        public ValueTask<byte[]?> ReadCurrentAsync(string key, CancellationToken token) => new(_current.TryGetValue(key, out var value) ? (byte[])value.Clone() : null);
        public ValueTask WriteTemporaryAsync(string key, string operationId, byte[] value, CancellationToken token) { token.ThrowIfCancellationRequested(); _temporary[operationId] = (byte[])value.Clone(); return default; }
        public ValueTask<byte[]?> ReadTemporaryAsync(string key, string operationId, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            byte[]? data = _temporary.TryGetValue(operationId, out var value) ? (byte[])value.Clone() : null;
            AfterReadTemporary?.Invoke();
            return new ValueTask<byte[]?>(data);
        }
        public ValueTask ActivateTemporaryAsync(string key, string operationId)
        {
            if (CancelBeforeActivate) throw new OperationCanceledException();
            _current[key] = (byte[])_temporary[operationId].Clone();
            _temporary.Remove(operationId);
            return default;
        }
        public ValueTask DiscardTemporaryAsync(string key, string operationId, CancellationToken token) { _temporary.Remove(operationId); return default; }
    }
}
