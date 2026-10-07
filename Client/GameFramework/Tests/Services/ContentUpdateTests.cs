using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Lyking.GameFramework.Client.Services.ContentUpdate;
using Xunit;

namespace Lyking.GameFramework.Client.Tests.Services;

public sealed class ContentUpdateTests
{
    [Fact]
    public async Task 文件版本存储通过原子指针切换并保留旧快照()
    {
        string rootPath = Path.Combine(Path.GetTempPath(), "lyking-content-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var trust = new EcdsaContentManifestTrustStore();
            trust.AddTrustedRoot("root-1", key);
            var store = new FileContentVersionStore(rootPath);
            var service = new ContentUpdateService(store, trust, null, "my-game", "desktop-test", () => DateTimeOffset.UtcNow);
            byte[] first = { 1 };
            await service.ApplyAsync(Sign(Manifest("1.0.0", 1, false, new[] { File("state.bin", first) }), "root-1", key),
                new MemoryPackageSource(new Dictionary<string, byte[]> { ["state.bin"] = first }));
            byte[] second = { 2 };
            await service.ApplyAsync(Sign(Manifest("2.0.0", 2, false, new[] { File("state.bin", second) }), "root-1", key),
                new MemoryPackageSource(new Dictionary<string, byte[]> { ["state.bin"] = second }));

            Assert.Equal("2.0.0", (await store.ReadStateAsync(CancellationToken.None)).ActiveVersion);
            Assert.Equal(second, await store.ReadActiveFileAsync("state.bin", CancellationToken.None));
            Assert.Equal(2, Directory.GetDirectories(Path.Combine(rootPath, "snapshots")).Length);
        }
        finally
        {
            if (Directory.Exists(rootPath)) Directory.Delete(rootPath, recursive: true);
        }
    }

    [Fact]
    public async Task 持久清单哈希允许精确重放并拒绝同序号冲突清单()
    {
        string rootPath = Path.Combine(Path.GetTempPath(), "lyking-content-replay-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var trust = new EcdsaContentManifestTrustStore();
            trust.AddTrustedRoot("root-1", key);
            byte[] originalContent = { 1 };
            byte[] conflictingContent = { 2 };
            var originalManifest = Sign(Manifest("1.0.0", 1, false, new[] { File("state.bin", originalContent) }), "root-1", key);
            var store = new FileContentVersionStore(rootPath);
            var service = new ContentUpdateService(store, trust, null, "my-game", "desktop-test", () => DateTimeOffset.UtcNow);

            await service.ApplyAsync(originalManifest,
                new MemoryPackageSource(new Dictionary<string, byte[]> { ["state.bin"] = originalContent }));

            var reopenedStore = new FileContentVersionStore(rootPath);
            var reopenedService = new ContentUpdateService(reopenedStore, trust, null, "my-game", "desktop-test", () => DateTimeOffset.UtcNow);
            await reopenedService.ApplyAsync(originalManifest,
                new MemoryPackageSource(new Dictionary<string, byte[]> { ["state.bin"] = originalContent }));

            var conflictingManifest = Sign(
                Manifest("1.0.0", 1, false, new[] { File("state.bin", conflictingContent) }), "root-1", key);
            ContentManifestRejectedException conflict = await Assert.ThrowsAsync<ContentManifestRejectedException>(async () =>
                await reopenedService.ApplyAsync(conflictingManifest,
                    new MemoryPackageSource(new Dictionary<string, byte[]> { ["state.bin"] = conflictingContent })));

            Assert.Contains("冲突重放", conflict.Message);
            ContentVersionState finalState = await reopenedStore.ReadStateAsync(CancellationToken.None);
            Assert.Equal("1.0.0", finalState.ActiveVersion);
            Assert.Equal(1, finalState.ActiveSequence);
            Assert.Equal(1, finalState.HighestSequence);
            Assert.Equal(ManifestHash(originalManifest), finalState.ActiveManifestHash);
            Assert.Equal(originalContent, await reopenedStore.ReadActiveFileAsync("state.bin", CancellationToken.None));
            Assert.Single(Directory.GetDirectories(Path.Combine(rootPath, "snapshots")));
        }
        finally
        {
            if (Directory.Exists(rootPath)) Directory.Delete(rootPath, recursive: true);
        }
    }

    [Fact]
    public async Task 共享目录的并发旧版本激活不能覆盖较高序号指针()
    {
        string rootPath = Path.Combine(Path.GetTempPath(), "lyking-content-race-" + Guid.NewGuid().ToString("N"));
        var olderSource = new BlockingPackageSource(new Dictionary<string, byte[]> { ["state.bin"] = new byte[] { 1 } });
        Task? olderApply = null;
        try
        {
            using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var trust = new EcdsaContentManifestTrustStore();
            trust.AddTrustedRoot("root-1", key);
            var olderService = new ContentUpdateService(new FileContentVersionStore(rootPath), trust, null,
                "my-game", "desktop-test", () => DateTimeOffset.UtcNow);
            var newerStore = new FileContentVersionStore(rootPath);
            var newerService = new ContentUpdateService(newerStore, trust, null,
                "my-game", "desktop-test", () => DateTimeOffset.UtcNow);
            byte[] newerContent = { 2 };

            olderApply = olderService.ApplyAsync(
                Sign(Manifest("1.0.0", 1, false, new[] { File("state.bin", new byte[] { 1 }) }), "root-1", key),
                olderSource).AsTask();
            await olderSource.FetchStarted.WaitAsync(TimeSpan.FromSeconds(10));

            await newerService.ApplyAsync(
                Sign(Manifest("2.0.0", 2, false, new[] { File("state.bin", newerContent) }), "root-1", key),
                new MemoryPackageSource(new Dictionary<string, byte[]> { ["state.bin"] = newerContent }));

            olderSource.Release();
            await Assert.ThrowsAsync<ContentManifestRejectedException>(async () => await olderApply);
            ContentVersionState finalState = await newerStore.ReadStateAsync(CancellationToken.None);
            Assert.Equal("2.0.0", finalState.ActiveVersion);
            Assert.Equal(2, finalState.ActiveSequence);
            Assert.Equal(2, finalState.HighestSequence);
            Assert.Equal(newerContent, await newerStore.ReadActiveFileAsync("state.bin", CancellationToken.None));
        }
        finally
        {
            olderSource.Release();
            if (olderApply != null)
            {
                try { await olderApply; }
                catch { }
            }
            if (Directory.Exists(rootPath)) Directory.Delete(rootPath, recursive: true);
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task 共享目录并发应用完全相同清单视为幂等(bool shareTrustStore)
    {
        string rootPath = Path.Combine(Path.GetTempPath(), "lyking-content-same-race-" + Guid.NewGuid().ToString("N"));
        byte[] content = { 7, 8, 9 };
        var firstSource = new BlockingPackageSource(new Dictionary<string, byte[]> { ["state.bin"] = content });
        var secondSource = new BlockingPackageSource(new Dictionary<string, byte[]> { ["state.bin"] = content });
        Task? firstApply = null;
        Task? secondApply = null;
        try
        {
            using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            using var rotatedKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var firstTrust = new EcdsaContentManifestTrustStore();
            firstTrust.AddTrustedRoot("root-1", key);
            var secondTrust = shareTrustStore ? firstTrust : new EcdsaContentManifestTrustStore();
            if (!shareTrustStore) secondTrust.AddTrustedRoot("root-1", key);
            var rotation = new ContentKeyRotation("root-2", rotatedKey.ExportSubjectPublicKeyInfo(), 2, 10);
            ContentManifest manifest = Sign(
                Manifest("1.0.0", 1, false, new[] { File("state.bin", content) }, rotations: new[] { rotation }), "root-1", key);
            var firstStore = new FileContentVersionStore(rootPath);
            var secondStore = new FileContentVersionStore(rootPath);
            var firstService = new ContentUpdateService(firstStore, firstTrust, null, "my-game", "desktop-test", () => DateTimeOffset.UtcNow);
            var secondService = new ContentUpdateService(secondStore, secondTrust, null, "my-game", "desktop-test", () => DateTimeOffset.UtcNow);

            firstApply = firstService.ApplyAsync(manifest, firstSource).AsTask();
            secondApply = secondService.ApplyAsync(manifest, secondSource).AsTask();
            await Task.WhenAll(firstSource.FetchStarted, secondSource.FetchStarted).WaitAsync(TimeSpan.FromSeconds(10));
            firstSource.Release();
            secondSource.Release();
            await Task.WhenAll(firstApply, secondApply);

            ContentVersionState state = await firstStore.ReadStateAsync(CancellationToken.None);
            Assert.Equal("1.0.0", state.ActiveVersion);
            Assert.Equal(1, state.ActiveSequence);
            Assert.Equal(ManifestHash(manifest), state.ActiveManifestHash);
            Assert.Equal(content, await firstStore.ReadActiveFileAsync("state.bin", CancellationToken.None));
            ContentManifest nextManifest = Sign(Manifest("2.0.0", 2, false, Array.Empty<ContentFileEntry>(), keyId: "root-2"), "root-2", rotatedKey);
            Assert.True(firstTrust.VerifySignature(nextManifest.SigningKeyId, nextManifest.Sequence,
                ContentManifestCodec.EncodeCanonical(nextManifest), nextManifest.Signature));
            Assert.True(secondTrust.VerifySignature(nextManifest.SigningKeyId, nextManifest.Sequence,
                ContentManifestCodec.EncodeCanonical(nextManifest), nextManifest.Signature));
            Assert.Single(Directory.GetDirectories(Path.Combine(rootPath, "snapshots")));
        }
        finally
        {
            firstSource.Release();
            secondSource.Release();
            if (firstApply != null)
            {
                try { await firstApply; }
                catch { }
            }
            if (secondApply != null)
            {
                try { await secondApply; }
                catch { }
            }
            if (Directory.Exists(rootPath)) Directory.Delete(rootPath, recursive: true);
        }
    }

    [Fact]
    public async Task 活动内容缺失时恢复上一快照且保留最高序号()
    {
        string rootPath = Path.Combine(Path.GetTempPath(), "lyking-content-recovery-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var trust = new EcdsaContentManifestTrustStore();
            trust.AddTrustedRoot("root-1", key);
            var store = new FileContentVersionStore(rootPath);
            var service = new ContentUpdateService(store, trust, null, "my-game", "desktop-test", () => DateTimeOffset.UtcNow);
            byte[] first = { 1 };
            byte[] second = { 2 };
            var firstManifest = Sign(Manifest("1.0.0", 1, false, new[] { File("state.bin", first) }), "root-1", key);
            string firstManifestHash = ManifestHash(firstManifest);
            await service.ApplyAsync(firstManifest,
                new MemoryPackageSource(new Dictionary<string, byte[]> { ["state.bin"] = first }));
            await service.ApplyAsync(Sign(Manifest("2.0.0", 2, false, new[] { File("state.bin", second) }), "root-1", key),
                new MemoryPackageSource(new Dictionary<string, byte[]> { ["state.bin"] = second }));

            ContentVersionState active = await store.ReadStateAsync(CancellationToken.None);
            string activeFile = Path.Combine(rootPath, "snapshots", active.ActiveSnapshotId!, "files", "state.bin");
            System.IO.File.Delete(activeFile);
            Assert.Null(await store.ReadActiveFileAsync("state.bin", CancellationToken.None));

            var recoverableStore = Assert.IsAssignableFrom<IRecoverableContentVersionStore>(store);
            ContentVersionState restored = await recoverableStore.RestorePreviousAsync(CancellationToken.None);

            Assert.Equal("1.0.0", restored.ActiveVersion);
            Assert.Equal(1, restored.ActiveSequence);
            Assert.Equal(2, restored.HighestSequence);
            Assert.Equal(firstManifestHash, restored.ActiveManifestHash);
            Assert.Equal(first, await store.ReadActiveFileAsync("state.bin", CancellationToken.None));
        }
        finally
        {
            if (Directory.Exists(rootPath)) Directory.Delete(rootPath, recursive: true);
        }
    }

    [Fact]
    public async Task 旧版指针可读取且激活新版本后仍可恢复()
    {
        string rootPath = Path.Combine(Path.GetTempPath(), "lyking-content-legacy-" + Guid.NewGuid().ToString("N"));
        try
        {
            string oldSnapshotId = Guid.NewGuid().ToString("N");
            byte[] oldContent = { 4 };
            string oldFilesPath = Path.Combine(rootPath, "snapshots", oldSnapshotId, "files");
            Directory.CreateDirectory(oldFilesPath);
            await System.IO.File.WriteAllBytesAsync(Path.Combine(oldFilesPath, "state.bin"), oldContent);
            await System.IO.File.WriteAllBytesAsync(Path.Combine(rootPath, "current.ptr"),
                CreateLegacyPointer(oldSnapshotId, "1.0.0", 1, 1));

            var store = new FileContentVersionStore(rootPath);
            ContentVersionState legacyState = await store.ReadStateAsync(CancellationToken.None);
            Assert.Equal("1.0.0", legacyState.ActiveVersion);
            Assert.Equal(oldContent, await store.ReadActiveFileAsync("state.bin", CancellationToken.None));

            string nextSnapshotId = Guid.NewGuid().ToString("N");
            await store.BeginStagingAsync(nextSnapshotId, "2.0.0", 2, CancellationToken.None);
            byte[] newContent = { 5 };
            await store.WriteStagedFileAsync(nextSnapshotId, "state.bin", newContent, CancellationToken.None);
            Assert.True(store.ActivateStaged(nextSnapshotId, "2.0.0", 2, new string('a', 64)));

            ContentVersionState restored = await store.RestorePreviousAsync(CancellationToken.None);
            Assert.Equal("1.0.0", restored.ActiveVersion);
            Assert.Equal(1, restored.ActiveSequence);
            Assert.Equal(2, restored.HighestSequence);
            Assert.Null(restored.ActiveManifestHash);
            Assert.Equal(oldContent, await store.ReadActiveFileAsync("state.bin", CancellationToken.None));
        }
        finally
        {
            if (Directory.Exists(rootPath)) Directory.Delete(rootPath, recursive: true);
        }
    }

    [Fact]
    public async Task 版本二指针仍可恢复到前一快照()
    {
        string rootPath = Path.Combine(Path.GetTempPath(), "lyking-content-v2-" + Guid.NewGuid().ToString("N"));
        try
        {
            string previousSnapshotId = Guid.NewGuid().ToString("N");
            string activeSnapshotId = Guid.NewGuid().ToString("N");
            byte[] previousContent = { 6 };
            byte[] activeContent = { 7 };
            string previousFilesPath = Path.Combine(rootPath, "snapshots", previousSnapshotId, "files");
            string activeFilesPath = Path.Combine(rootPath, "snapshots", activeSnapshotId, "files");
            Directory.CreateDirectory(previousFilesPath);
            Directory.CreateDirectory(activeFilesPath);
            await System.IO.File.WriteAllBytesAsync(Path.Combine(previousFilesPath, "state.bin"), previousContent);
            await System.IO.File.WriteAllBytesAsync(Path.Combine(activeFilesPath, "state.bin"), activeContent);
            await System.IO.File.WriteAllBytesAsync(Path.Combine(rootPath, "current.ptr"),
                CreateVersionTwoPointer(activeSnapshotId, "2.0.0", 2, 2, previousSnapshotId, "1.0.0", 1));

            var store = new FileContentVersionStore(rootPath);
            ContentVersionState restored = await store.RestorePreviousAsync(CancellationToken.None);

            Assert.Equal("1.0.0", restored.ActiveVersion);
            Assert.Equal(1, restored.ActiveSequence);
            Assert.Equal(2, restored.HighestSequence);
            Assert.Null(restored.ActiveManifestHash);
            Assert.Equal(previousContent, await store.ReadActiveFileAsync("state.bin", CancellationToken.None));
        }
        finally
        {
            if (Directory.Exists(rootPath)) Directory.Delete(rootPath, recursive: true);
        }
    }

    [Fact]
    public async Task 规范化签名清单通过块校验后原子激活()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var trust = new EcdsaContentManifestTrustStore();
        trust.AddTrustedRoot("root-1", key);
        byte[] content = { 10, 20, 30 };
        var manifest = Sign(Manifest("2.0.0", 7, false, new[] { File("assets/data.bin", content) }), "root-1", key);
        var store = new MemoryContentVersionStore();
        var source = new MemoryPackageSource(new Dictionary<string, byte[]> { ["assets/data.bin"] = content });
        var service = CreateService(store, trust);

        await service.ApplyAsync(manifest, source);
        await service.ApplyAsync(manifest, source);

        Assert.Equal("2.0.0", store.State.ActiveVersion);
        Assert.Equal(7, store.State.HighestSequence);
        Assert.Equal(content, await store.ReadActiveFileAsync("assets/data.bin", CancellationToken.None));
    }

    [Fact]
    public async Task 清单签名绑定应用平台且过期或签名篡改均拒绝()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var trust = new EcdsaContentManifestTrustStore();
        trust.AddTrustedRoot("root-1", key);
        var store = new MemoryContentVersionStore();
        var service = CreateService(store, trust);
        var source = new MemoryPackageSource(new Dictionary<string, byte[]>());
        var wrongApp = Sign(Manifest("1.0.0", 1, false, Array.Empty<ContentFileEntry>(), app: "other-game"), "root-1", key);
        await Assert.ThrowsAsync<ContentManifestRejectedException>(async () => await service.ApplyAsync(wrongApp, source));
        var wrongPlatform = Sign(Manifest("1.0.0", 1, false, Array.Empty<ContentFileEntry>(), platform: "other-platform"), "root-1", key);
        await Assert.ThrowsAsync<ContentManifestRejectedException>(async () => await service.ApplyAsync(wrongPlatform, source));

        var expired = Sign(Manifest("1.0.0", 1, false, Array.Empty<ContentFileEntry>(), expires: DateTimeOffset.UtcNow.AddMinutes(-1)), "root-1", key);
        await Assert.ThrowsAsync<ContentManifestRejectedException>(async () => await service.ApplyAsync(expired, source));

        var valid = Sign(Manifest("1.0.0", 1, false, Array.Empty<ContentFileEntry>()), "root-1", key);
        byte[] tamperedSignature = valid.Signature;
        tamperedSignature[0] ^= 1;
        await Assert.ThrowsAsync<ContentManifestRejectedException>(async () => await service.ApplyAsync(valid.WithSignature(tamperedSignature), source));
        Assert.Null(store.State.ActiveVersion);
    }

    [Fact]
    public async Task 内容哈希失败和取消都保留已激活版本()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var trust = new EcdsaContentManifestTrustStore();
        trust.AddTrustedRoot("root-1", key);
        var store = new MemoryContentVersionStore();
        var service = CreateService(store, trust);
        byte[] original = { 1 };
        await service.ApplyAsync(Sign(Manifest("1.0.0", 1, false, new[] { File("a.bin", original) }), "root-1", key), new MemoryPackageSource(new Dictionary<string, byte[]> { ["a.bin"] = original }));
        var state = store.State;

        var bad = Sign(Manifest("2.0.0", 2, false, new[] { File("a.bin", new byte[] { 2 }) }), "root-1", key);
        await Assert.ThrowsAsync<ContentBlockIntegrityException>(async () => await service.ApplyAsync(bad, new MemoryPackageSource(new Dictionary<string, byte[]> { ["a.bin"] = new byte[] { 9 } })));
        Assert.Equal(state, store.State);
        Assert.Equal(original, await store.ReadActiveFileAsync("a.bin", CancellationToken.None));

        using var cancellation = new CancellationTokenSource();
        var cancelSource = new MemoryPackageSource(new Dictionary<string, byte[]> { ["a.bin"] = new byte[] { 2 } }, cancellation);
        var next = Sign(Manifest("2.0.0", 2, false, new[] { File("a.bin", new byte[] { 2 }) }), "root-1", key);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await service.ApplyAsync(next, cancelSource, cancellationToken: cancellation.Token));
        Assert.Equal(state, store.State);
    }

    [Fact]
    public async Task 重放被拒绝且恢复必须带外部授权并保持最高序号()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var trust = new EcdsaContentManifestTrustStore();
        trust.AddTrustedRoot("root-1", key);
        var store = new MemoryContentVersionStore();
        var service = CreateService(store, trust);
        var source = new MemoryPackageSource(new Dictionary<string, byte[]>());
        await service.ApplyAsync(Sign(Manifest("3.0.0", 3, false, Array.Empty<ContentFileEntry>()), "root-1", key), source);
        var replay = Sign(Manifest("2.0.0", 2, false, Array.Empty<ContentFileEntry>()), "root-1", key);
        await Assert.ThrowsAsync<ContentManifestRejectedException>(async () => await service.ApplyAsync(replay, source));

        var staleRecovery = Sign(Manifest("2.0.0", 2, true, Array.Empty<ContentFileEntry>()), "root-1", key);
        await Assert.ThrowsAsync<ContentManifestRejectedException>(async () => await service.ApplyAsync(staleRecovery, source, new ContentRollbackAuthorization(new byte[] { 1 })));
        var recovery = Sign(Manifest("2.0.0", 4, true, Array.Empty<ContentFileEntry>()), "root-1", key);
        await Assert.ThrowsAsync<ContentManifestRejectedException>(async () => await service.ApplyAsync(recovery, source, new ContentRollbackAuthorization(new byte[] { 1 })));
        var authorized = CreateService(store, trust, new AllowRollback());
        await authorized.ApplyAsync(recovery, source, new ContentRollbackAuthorization(new byte[] { 1 }));

        Assert.Equal("2.0.0", store.State.ActiveVersion);
        Assert.Equal(4, store.State.HighestSequence);
        Assert.Equal(4, store.State.ActiveSequence);
    }

    [Fact]
    public void 规范化编码忽略输入顺序并覆盖所有信任字段()
    {
        var first = Manifest("1.0.0", 1, false, new[] { new ContentFileEntry("b.bin", new string('a', 64), 2), new ContentFileEntry("a.bin", new string('b', 64), 1) });
        var second = new ContentManifest(first.ApplicationId, first.PlatformId, first.Version, first.Sequence,
            first.NotBeforeUtc, first.ExpiresAtUtc, first.SigningKeyId,
            new[] { new ContentFileEntry("a.bin", new string('b', 64), 1), new ContentFileEntry("b.bin", new string('a', 64), 2) },
            first.Rotations, first.IsRecovery);

        Assert.Equal(ContentManifestCodec.EncodeCanonical(first), ContentManifestCodec.EncodeCanonical(second));
        var changed = new ContentManifest(first.ApplicationId, first.PlatformId, first.Version, 2,
            first.NotBeforeUtc, first.ExpiresAtUtc, first.SigningKeyId, first.Files, first.Rotations, first.IsRecovery);
        Assert.NotEqual(ContentManifestCodec.EncodeCanonical(first), ContentManifestCodec.EncodeCanonical(changed));
    }

    [Fact]
    public async Task 当前受信密钥签发的轮换后可验证新密钥清单()
    {
        using var root = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var rotated = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var trust = new EcdsaContentManifestTrustStore();
        trust.AddTrustedRoot("root-1", root);
        var rotation = new ContentKeyRotation("key-2", rotated.ExportSubjectPublicKeyInfo(), 2, 10, "root-1", 5);
        var store = new MemoryContentVersionStore();
        var service = CreateService(store, trust);
        var first = Sign(Manifest("1.0.0", 1, false, Array.Empty<ContentFileEntry>(), rotations: new[] { rotation }), "root-1", root);
        await service.ApplyAsync(first, new MemoryPackageSource(new Dictionary<string, byte[]>()));

        var second = Sign(Manifest("2.0.0", 2, false, Array.Empty<ContentFileEntry>(), keyId: "key-2"), "key-2", rotated);
        await service.ApplyAsync(second, new MemoryPackageSource(new Dictionary<string, byte[]>()));

        Assert.Equal("2.0.0", store.State.ActiveVersion);
        var retiredRoot = Sign(Manifest("3.0.0", 6, false, Array.Empty<ContentFileEntry>()), "root-1", root);
        await Assert.ThrowsAsync<ContentManifestRejectedException>(async () => await service.ApplyAsync(retiredRoot, new MemoryPackageSource(new Dictionary<string, byte[]>())));
        await Assert.ThrowsAsync<ContentManifestRejectedException>(async () => await service.ApplyAsync(
            Sign(Manifest("3.0.0", 11, false, Array.Empty<ContentFileEntry>(), keyId: "key-2"), "key-2", rotated),
            new MemoryPackageSource(new Dictionary<string, byte[]>())));
    }

    [Fact]
    public async Task 指针激活失败保留旧版本且轮换只来自已验签清单()
    {
        using var root = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var rotated = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var trust = new EcdsaContentManifestTrustStore();
        trust.AddTrustedRoot("root-1", root);
        var store = new MemoryContentVersionStore();
        var service = CreateService(store, trust);
        await service.ApplyAsync(Sign(Manifest("1.0.0", 1, false, Array.Empty<ContentFileEntry>()), "root-1", root), new MemoryPackageSource(new Dictionary<string, byte[]>()));
        ContentVersionState oldState = store.State;
        var rotation = new ContentKeyRotation("key-2", rotated.ExportSubjectPublicKeyInfo(), 3, 20);
        var next = Sign(Manifest("2.0.0", 2, false, Array.Empty<ContentFileEntry>(), rotations: new[] { rotation }), "root-1", root);
        store.FailActivation = true;

        await Assert.ThrowsAsync<InvalidOperationException>(async () => await service.ApplyAsync(next, new MemoryPackageSource(new Dictionary<string, byte[]>())));

        Assert.Same(oldState, store.State);
        var future = Sign(Manifest("3.0.0", 3, false, Array.Empty<ContentFileEntry>(), keyId: "key-2"), "key-2", rotated);
        Assert.False(trust.VerifySignature(future.SigningKeyId, future.Sequence, ContentManifestCodec.EncodeCanonical(future), future.Signature));
    }

    private static ContentUpdateService CreateService(MemoryContentVersionStore store, EcdsaContentManifestTrustStore trust, IRollbackAuthorizationVerifier? rollback = null) =>
        new(store, trust, rollback, "my-game", "desktop-test", () => DateTimeOffset.UtcNow);

    private static ContentManifest Manifest(string version, long sequence, bool recovery, IReadOnlyList<ContentFileEntry> files,
        string app = "my-game", string platform = "desktop-test", DateTimeOffset? expires = null,
        string keyId = "root-1", IReadOnlyList<ContentKeyRotation>? rotations = null) =>
        new(app, platform, version, sequence, DateTimeOffset.UtcNow.AddMinutes(-5), expires ?? DateTimeOffset.UtcNow.AddHours(1), keyId, files, rotations, recovery);

    private static ContentFileEntry File(string path, byte[] bytes) => new(path, Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(), bytes.Length);

    private static string ManifestHash(ContentManifest manifest)
    {
        using SHA256 sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(ContentManifestCodec.EncodeCanonical(manifest))).ToLowerInvariant();
    }

    private static byte[] CreateLegacyPointer(string snapshotId, string version, long sequence, long highWaterMark)
    {
        byte[] snapshot = Encoding.ASCII.GetBytes(snapshotId);
        byte[] versionBytes = Encoding.UTF8.GetBytes(version);
        var pointer = new byte[25 + snapshot.Length + versionBytes.Length + 32];
        Buffer.BlockCopy(Encoding.ASCII.GetBytes("LYCV"), 0, pointer, 0, 4);
        pointer[4] = 1;
        BinaryPrimitives.WriteUInt16BigEndian(pointer.AsSpan(5, 2), (ushort)snapshot.Length);
        BinaryPrimitives.WriteUInt16BigEndian(pointer.AsSpan(7, 2), (ushort)versionBytes.Length);
        BinaryPrimitives.WriteInt64BigEndian(pointer.AsSpan(9, 8), sequence);
        BinaryPrimitives.WriteInt64BigEndian(pointer.AsSpan(17, 8), highWaterMark);
        Buffer.BlockCopy(snapshot, 0, pointer, 25, snapshot.Length);
        Buffer.BlockCopy(versionBytes, 0, pointer, 25 + snapshot.Length, versionBytes.Length);
        using SHA256 sha = SHA256.Create();
        byte[] checksum = sha.ComputeHash(pointer, 0, pointer.Length - 32);
        Buffer.BlockCopy(checksum, 0, pointer, pointer.Length - checksum.Length, checksum.Length);
        return pointer;
    }

    private static byte[] CreateVersionTwoPointer(string snapshotId, string version, long sequence, long highWaterMark,
        string previousSnapshotId, string previousVersion, long previousSequence)
    {
        byte[] snapshot = Encoding.ASCII.GetBytes(snapshotId);
        byte[] versionBytes = Encoding.UTF8.GetBytes(version);
        byte[] previousSnapshot = Encoding.ASCII.GetBytes(previousSnapshotId);
        byte[] previousVersionBytes = Encoding.UTF8.GetBytes(previousVersion);
        var pointer = new byte[37 + snapshot.Length + versionBytes.Length + previousSnapshot.Length + previousVersionBytes.Length + 32];
        Buffer.BlockCopy(Encoding.ASCII.GetBytes("LYCV"), 0, pointer, 0, 4);
        pointer[4] = 2;
        BinaryPrimitives.WriteUInt16BigEndian(pointer.AsSpan(5, 2), (ushort)snapshot.Length);
        BinaryPrimitives.WriteUInt16BigEndian(pointer.AsSpan(7, 2), (ushort)versionBytes.Length);
        BinaryPrimitives.WriteInt64BigEndian(pointer.AsSpan(9, 8), sequence);
        BinaryPrimitives.WriteInt64BigEndian(pointer.AsSpan(17, 8), highWaterMark);
        BinaryPrimitives.WriteUInt16BigEndian(pointer.AsSpan(25, 2), (ushort)previousSnapshot.Length);
        BinaryPrimitives.WriteUInt16BigEndian(pointer.AsSpan(27, 2), (ushort)previousVersionBytes.Length);
        BinaryPrimitives.WriteInt64BigEndian(pointer.AsSpan(29, 8), previousSequence);
        int offset = 37;
        Buffer.BlockCopy(snapshot, 0, pointer, offset, snapshot.Length);
        offset += snapshot.Length;
        Buffer.BlockCopy(versionBytes, 0, pointer, offset, versionBytes.Length);
        offset += versionBytes.Length;
        Buffer.BlockCopy(previousSnapshot, 0, pointer, offset, previousSnapshot.Length);
        offset += previousSnapshot.Length;
        Buffer.BlockCopy(previousVersionBytes, 0, pointer, offset, previousVersionBytes.Length);
        using SHA256 sha = SHA256.Create();
        byte[] checksum = sha.ComputeHash(pointer, 0, pointer.Length - 32);
        Buffer.BlockCopy(checksum, 0, pointer, pointer.Length - checksum.Length, checksum.Length);
        return pointer;
    }

    private static ContentManifest Sign(ContentManifest unsigned, string keyId, ECDsa key) =>
        unsigned.WithSignature(key.SignData(ContentManifestCodec.EncodeCanonical(unsigned), HashAlgorithmName.SHA256));

    private sealed class AllowRollback : IRollbackAuthorizationVerifier
    {
        public ValueTask<bool> ValidateAsync(ContentRollbackAuthorization authorization, string applicationId, string platformId,
            ContentVersionState currentState, ContentManifest manifest, CancellationToken cancellationToken) => new(true);
    }

    private sealed class MemoryPackageSource : IContentPackageSource
    {
        private readonly IReadOnlyDictionary<string, byte[]> _files;
        private readonly CancellationTokenSource? _cancel;
        public MemoryPackageSource(IReadOnlyDictionary<string, byte[]> files, CancellationTokenSource? cancel = null) { _files = files; _cancel = cancel; }
        public ValueTask<byte[]> FetchAsync(string path, CancellationToken cancellationToken)
        {
            _cancel?.Cancel();
            cancellationToken.ThrowIfCancellationRequested();
            return new ValueTask<byte[]>((byte[])_files[path].Clone());
        }
    }

    private sealed class BlockingPackageSource : IContentPackageSource
    {
        private readonly IReadOnlyDictionary<string, byte[]> _files;
        private readonly TaskCompletionSource _fetchStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public BlockingPackageSource(IReadOnlyDictionary<string, byte[]> files) => _files = files;
        public Task FetchStarted => _fetchStarted.Task;
        public void Release() => _release.TrySetResult();

        public async ValueTask<byte[]> FetchAsync(string path, CancellationToken cancellationToken)
        {
            _fetchStarted.TrySetResult();
            await _release.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            return (byte[])_files[path].Clone();
        }
    }

    private sealed class MemoryContentVersionStore : IContentVersionStore
    {
        private readonly Dictionary<string, Dictionary<string, byte[]>> _versions = new();
        public ContentVersionState State { get; private set; } = new(null, null, 0, 0);
        public bool FailActivation { get; set; }
        public ValueTask<ContentVersionState> ReadStateAsync(CancellationToken cancellationToken) => new(State);
        public ValueTask BeginStagingAsync(string stagingId, string version, long sequence, CancellationToken cancellationToken) { _versions.Add(stagingId, new Dictionary<string, byte[]>()); return default; }
        public ValueTask WriteStagedFileAsync(string stagingId, string path, byte[] content, CancellationToken cancellationToken) { _versions[stagingId][path] = (byte[])content.Clone(); return default; }
        public ValueTask<byte[]?> ReadStagedFileAsync(string stagingId, string path, CancellationToken cancellationToken) => new(_versions[stagingId].TryGetValue(path, out var content) ? (byte[])content.Clone() : null);
        public bool ActivateStaged(string stagingId, string version, long sequence, string manifestHash)
        {
            if (FailActivation) throw new InvalidOperationException("注入的指针激活故障");
            if (sequence <= State.HighestSequence)
            {
                if (sequence == State.ActiveSequence
                    && string.Equals(version, State.ActiveVersion, StringComparison.Ordinal)
                    && string.Equals(manifestHash, State.ActiveManifestHash, StringComparison.Ordinal))
                {
                    return false;
                }

                throw new ContentManifestRejectedException("内容清单序号已被其他更新激活或超过当前高水位。");
            }

            State = new ContentVersionState(version, stagingId, sequence, Math.Max(State.HighestSequence, sequence), manifestHash);
            return true;
        }
        public ValueTask DiscardStagedAsync(string stagingId, CancellationToken cancellationToken) { _versions.Remove(stagingId); return default; }
        public ValueTask<byte[]?> ReadActiveFileAsync(string path, CancellationToken cancellationToken) =>
            new(State.ActiveSnapshotId is not null && _versions[State.ActiveSnapshotId].TryGetValue(path, out var content) ? (byte[])content.Clone() : null);
    }
}
