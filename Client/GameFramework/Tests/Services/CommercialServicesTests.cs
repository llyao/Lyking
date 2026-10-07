using Lyking.GameFramework.Client.Services.Commercial;
using Lyking.GameFramework.Client.Core.Dispatching;
using Xunit;

namespace Lyking.GameFramework.Client.Tests.Services;

public sealed class PlatformServiceTests
{
    [Fact]
    public async Task 模拟账号与平台进度提供确定结果()
    {
        var accounts = new SimulationPlatformAccountService();
        var progress = new SimulationPlatformProgressService();

        PlatformIdentity identity = await accounts.SignInAsync();
        await progress.SubmitAchievementAsync("chapter.1", 0.5d);
        await progress.SubmitAchievementAsync("chapter.1", 0.25d);
        await progress.SubmitScoreAsync("arena.weekly", 1200);
        IReadOnlyList<LeaderboardEntry> entries = await progress.GetLeaderboardAsync("arena.weekly", 10);

        Assert.Equal("simulation", identity.Provider);
        Assert.Equal("chapter.1", Assert.Single(progress.Achievements).AchievementId);
        Assert.Equal(0.5d, progress.Achievements[0].Progress);
        Assert.Equal(1200L, Assert.Single(entries).Score);
    }

    [Fact]
    public void 隐私同意可单独授权并撤回()
    {
        var consent = new SimulationConsentService(new MainThreadDispatcher());
        var changes = new List<ConsentChanged>();
        consent.Changed += changes.Add;

        consent.SetConsent(ConsentPurpose.Analytics, true);
        consent.SetConsent(ConsentPurpose.CrashReporting, true);
        consent.SetConsent(ConsentPurpose.Analytics, false);

        Assert.False(consent.HasConsent(ConsentPurpose.Analytics));
        Assert.True(consent.HasConsent(ConsentPurpose.CrashReporting));
        Assert.Equal(3, changes.Count);
    }
}

public sealed class CommerceServiceTests
{
    [Fact]
    public async Task 购买和恢复只返回收据且相同交易可重复验证()
    {
        var product = new StoreProduct("starter.pack", "新手补给", "6.00", "CNY", isConsumable: false);
        var store = new SimulationStoreService(new[] { product });
        StorePurchaseResult purchase = await store.PurchaseAsync(product.ProductId);
        StoreReceipt receipt = Assert.IsType<StoreReceipt>(purchase.Receipt);
        var verifier = new SimulationReceiptVerifier(new[] { receipt });

        ReceiptVerificationResult first = await verifier.VerifyAsync(receipt);
        ReceiptVerificationResult second = await verifier.VerifyAsync(receipt);
        Assert.Empty(store.AcknowledgedTransactionIds);
        Assert.True(await store.AcknowledgeTransactionAsync(receipt, first));
        Assert.False(await store.AcknowledgeTransactionAsync(receipt, second));
        IReadOnlyList<StoreReceipt> restored = await store.RestorePurchasesAsync();

        Assert.Equal(PurchaseStatus.Purchased, purchase.Status);
        Assert.Equal(receipt.TransactionId, first.TransactionId);
        Assert.Equal(first, second);
        Assert.Equal(receipt.TransactionId, Assert.Single(restored).TransactionId);
        Assert.Equal(receipt.TransactionId, Assert.Single(store.AcknowledgedTransactionIds));
        Assert.Equal(1, verifier.UniqueTransactionCount);
        Assert.DoesNotContain(typeof(IReceiptVerifier).GetMethods(), method => method.Name.Contains("Grant", StringComparison.Ordinal));

        store.SetNextPurchaseStatus(PurchaseStatus.Cancelled);
        StorePurchaseResult cancelled = await store.PurchaseAsync(product.ProductId);
        Assert.Equal(PurchaseStatus.Cancelled, cancelled.Status);
        Assert.Null(cancelled.Receipt);
    }

    [Fact]
    public async Task 拒绝或未验证的收据不能确认平台交易()
    {
        var product = new StoreProduct("starter.pack", "新手补给", "6.00", "CNY", isConsumable: true);
        var store = new SimulationStoreService(new[] { product });
        StoreReceipt receipt = (await store.PurchaseAsync(product.ProductId)).Receipt!;
        var verifier = new SimulationReceiptVerifier(Array.Empty<StoreReceipt>());
        ReceiptVerificationResult rejected = await verifier.VerifyAsync(receipt);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            store.AcknowledgeTransactionAsync(receipt, rejected).AsTask());

        Assert.Empty(typeof(ReceiptVerificationResult).GetConstructors());
        Assert.Empty(store.AcknowledgedTransactionIds);
    }

    [Fact]
    public async Task 同交易标识不能验证不同商品收据()
    {
        var trusted = new StoreReceipt("tx-1", "starter.pack", "simulation", "opaque");
        var forged = new StoreReceipt("tx-1", "premium.currency", "simulation", "different");
        var verifier = new SimulationReceiptVerifier(new[] { trusted });

        ReceiptVerificationResult result = await verifier.VerifyAsync(forged);
        ReceiptVerificationResult valid = await verifier.VerifyAsync(trusted);

        Assert.Equal(ReceiptVerificationStatus.Rejected, result.Status);
        Assert.Equal(ReceiptVerificationStatus.Verified, valid.Status);
    }
}

public sealed class CommercialIntegrationServiceTests
{
    [Fact]
    public async Task 广告推送深链与归因可在模拟层驱动()
    {
        var dispatcher = new MainThreadDispatcher();
        var consent = new SimulationConsentService(dispatcher);
        var ads = new SimulationAdsService(consent);
        var push = new SimulationPushNotificationService(consent, dispatcher);
        var links = new SimulationLinkAttributionService(consent, dispatcher);
        int delivered = 0;
        push.NotificationReceived += _ => delivered++;
        LinkAttribution? receivedLink = null;
        LinkAttribution? receivedAttribution = null;
        links.LinkReceived += value => receivedLink = value;
        links.AttributionReceived += value => receivedAttribution = value;
        ads.SetOutcome("rewarded.double", AdFormat.Rewarded, AdShowStatus.Completed);

        AdShowResult blockedAd = await ads.ShowAsync("rewarded.double", AdFormat.Rewarded, personalized: true);
        consent.SetConsent(ConsentPurpose.PersonalizedAds, true);
        AdShowResult ad = await ads.ShowAsync("rewarded.double", AdFormat.Rewarded, personalized: true);
        consent.SetConsent(ConsentPurpose.PushNotifications, true);
        await push.RegisterAsync();
        RunOnBackgroundThread(() => push.Emit(new PushNotification("event.1", new Dictionary<string, string> { ["kind"] = "gift" })));
        Assert.Equal(0, delivered);
        dispatcher.Pump();
        await links.StartAsync();
        links.EmitLink(new LinkAttribution("https://game.example/open", "campaign-1", "source-1"));

        Assert.Equal(AdShowStatus.Completed, ad.Status);
        Assert.Equal(AdShowStatus.Unavailable, blockedAd.Status);
        Assert.True(push.IsRegistered);
        Assert.Equal(1, delivered);
        Assert.Equal("campaign-1", receivedLink?.Campaign);
        Assert.Null(receivedAttribution);

        consent.SetConsent(ConsentPurpose.PushNotifications, false);
        push.Emit(new PushNotification("event.2"));
        Assert.False(push.IsRegistered);
        Assert.Equal(1, delivered);

        receivedAttribution = null;
        RunOnBackgroundThread(() => links.EmitLink(new LinkAttribution("https://game.example/open", "campaign-late", "source-1")));
        consent.SetConsent(ConsentPurpose.Analytics, true);
        dispatcher.Pump();
        Assert.Null(receivedAttribution);

        links.EmitLink(new LinkAttribution("https://game.example/open", "campaign-1", "source-1"));
        Assert.Equal("campaign-1", receivedAttribution?.Campaign);
    }

    private static void RunOnBackgroundThread(Action action)
    {
        var thread = new System.Threading.Thread(() => action());
        thread.Start();
        thread.Join();
    }

    [Fact]
    public void 深链拒绝脚本与本地文件协议()
    {
        Assert.Throws<ArgumentException>(() => new LinkAttribution("javascript:alert(1)"));
        Assert.Throws<ArgumentException>(() => new LinkAttribution("file:///private/save.dat"));
    }

    [Fact]
    public void 远程配置只原子应用有效快照并在拒绝时回退()
    {
        var service = new RemoteConfigService(
            new Dictionary<string, string> { ["matchmaking.timeout"] = "30", ["shop.enabled"] = "false" },
            values => values.TryGetValue("matchmaking.timeout", out string? raw)
                && int.TryParse(raw, out int timeout) && timeout is >= 5 and <= 120
                && values.TryGetValue("shop.enabled", out string? enabled)
                && bool.TryParse(enabled, out _));

        RemoteConfigApplyResult applied = service.Apply(new Dictionary<string, string>
        {
            ["matchmaking.timeout"] = "45",
            ["shop.enabled"] = "true",
        });
        Assert.Equal(RemoteConfigApplyStatus.Applied, applied.Status);
        Assert.Equal("45", service.Current["matchmaking.timeout"]);

        RemoteConfigApplyResult rejected = service.Apply(new Dictionary<string, string>
        {
            ["matchmaking.timeout"] = "999",
            ["shop.enabled"] = "true",
        });

        Assert.Equal(RemoteConfigApplyStatus.RejectedAndFallbackApplied, rejected.Status);
        Assert.Equal("30", service.Current["matchmaking.timeout"]);
        Assert.Equal("false", service.Current["shop.enabled"]);
    }
}

public sealed class ObservabilityServiceTests
{
    [Fact]
    public void 同意撤回时前置观察者异常不会阻断遥测队列清理()
    {
        var consent = new SimulationConsentService(new MainThreadDispatcher());
        consent.Changed += change =>
        {
            if (!change.Granted)
            {
                throw new InvalidOperationException("模拟观察者失败。");
            }
        };
        var telemetry = new SimulationObservabilityService(consent, capacity: 2);
        consent.SetConsent(ConsentPurpose.Analytics, true);
        Assert.True(telemetry.Track("level.started"));

        Assert.Throws<AggregateException>(() => consent.SetConsent(ConsentPurpose.Analytics, false));

        Assert.False(consent.HasConsent(ConsentPurpose.Analytics));
        Assert.Equal(0, telemetry.PendingCount);
    }

    [Fact]
    public void 遥测受同意门控过滤敏感字段并限制队列容量()
    {
        var consent = new SimulationConsentService(new MainThreadDispatcher());
        var telemetry = new SimulationObservabilityService(consent, capacity: 2);

        Assert.False(telemetry.Track("level.started", new Dictionary<string, object?> { ["level"] = 1 }));
        consent.SetConsent(ConsentPurpose.Analytics, true);
        consent.SetConsent(ConsentPurpose.Performance, true);
        Assert.True(telemetry.Track("level.started", new Dictionary<string, object?>
        {
            ["level"] = 1,
            ["email"] = "player@example.com",
            ["access_token"] = "secret",
            ["note"] = "someone@example.com",
            ["freeform"] = "+86 138-0013-8000",
            ["client_id"] = "device-42",
        }));
        Assert.True(telemetry.RecordPerformance("frame.time", 16.7d));
        Assert.False(telemetry.Track("level.ended", new Dictionary<string, object?>()));

        IReadOnlyList<DiagnosticRecord> records = telemetry.Drain();
        DiagnosticRecord record = Assert.Single(records, item => item.Name == "level.started");
        Assert.Single(record.Fields);
        Assert.Equal(1, record.Fields["level"]);
        Assert.Equal(1, telemetry.DroppedCount);

        consent.SetConsent(ConsentPurpose.Analytics, false);
        Assert.False(telemetry.Track("level.started", new Dictionary<string, object?>()));

        consent.SetConsent(ConsentPurpose.Analytics, true);
        Assert.True(telemetry.Track("level.started"));
        consent.SetConsent(ConsentPurpose.Analytics, false);
        Assert.Equal(0, telemetry.PendingCount);
    }

    [Fact]
    public void 崩溃记录采用独立同意开关()
    {
        var consent = new SimulationConsentService(new MainThreadDispatcher());
        var telemetry = new SimulationObservabilityService(consent, capacity: 4);

        Assert.False(telemetry.RecordCrash("InvalidOperationException", "private details"));
        consent.SetConsent(ConsentPurpose.CrashReporting, true);

        Assert.True(telemetry.RecordCrash("InvalidOperationException", "private details"));
        DiagnosticRecord record = Assert.Single(telemetry.Drain());
        Assert.Equal("InvalidOperationException", record.Name);
        Assert.DoesNotContain("private details", record.Fields.Values.OfType<string>());
    }
}
