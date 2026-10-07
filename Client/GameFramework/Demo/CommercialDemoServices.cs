using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Lyking.GameFramework.Client.Core.Dispatching;
using Lyking.GameFramework.Client.Core.ServiceLocation;
using Lyking.GameFramework.Client.Services.Commercial;
using Lyking.GameFramework.Client.Services.ContentUpdate;
using Lyking.GameFramework.Client.Services.Input;
using Lyking.GameFramework.Client.Services.Persistence;

namespace Lyking.GameFramework.Client.Demo;

/// <summary>为无头演示显式装配模拟服务，并在结束时清理临时数据。</summary>
internal sealed class CommercialDemoServices : IDisposable
{
    private readonly string _temporaryRoot = Path.Combine(Path.GetTempPath(), "Lyking.Client.Demo", Guid.NewGuid().ToString("N"));
    private readonly ECDsa _publisherKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    private readonly ECDsa _trustedPublicKey = ECDsa.Create();
    private readonly DateTimeOffset _manifestTime = DateTimeOffset.UtcNow;
    private readonly IMainThreadDispatcher _dispatcher;
    private readonly SimulationConsentService _consent;
    private readonly SimulationPushNotificationService _push;
    private readonly SimulationLinkAttributionService _links;
    private readonly SimulationAdsService _ads;
    private readonly SimulationObservabilityService _observability;
    private readonly FileContentVersionStore _contentStore;
    private readonly EcdsaContentManifestTrustStore _trustStore = new();

    public CommercialDemoServices(IMainThreadDispatcher dispatcher)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _consent = new SimulationConsentService(_dispatcher);
        Directory.CreateDirectory(_temporaryRoot);
        _trustedPublicKey.ImportSubjectPublicKeyInfo(_publisherKey.ExportSubjectPublicKeyInfo(), out _);
        _trustStore.AddTrustedRoot("demo-root", _trustedPublicKey);
        _push = new SimulationPushNotificationService(_consent, _dispatcher);
        _links = new SimulationLinkAttributionService(_consent, _dispatcher);
        _ads = new SimulationAdsService(_consent);
        _observability = new SimulationObservabilityService(_consent, capacity: 8);
        _contentStore = new FileContentVersionStore(Path.Combine(_temporaryRoot, "content"));
    }

    public void Register(IServiceRegistry services)
    {
        services.Register<IPlatformAccountService>(new SimulationPlatformAccountService());
        services.Register<IPlatformProgressService>(new SimulationPlatformProgressService());
        services.Register<IConsentService>(_consent);
        services.Register<IStoreService>(new SimulationStoreService(new[]
        {
            new StoreProduct("starter.pack", "新手补给", "6.00", "CNY", isConsumable: true),
        }));
        services.Register<IAdsService>(_ads);
        services.Register<SimulationAdsService>(_ads);
        services.Register<IPushNotificationService>(_push);
        services.Register<ILinkAttributionService>(_links);
        services.Register<RemoteConfigService>(new RemoteConfigService(
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["matchmaking.timeout"] = "30",
                ["shop.enabled"] = "false",
            },
            IsValidConfig));
        services.Register<IObservabilityService>(_observability);
        services.Register<SimulationObservabilityService>(_observability);
        services.Register<InputService>(new InputService(_dispatcher));

        var saveService = new SaveDataService(
            new FileAtomicSaveStore(Path.Combine(_temporaryRoot, "saves")),
            new IdentitySaveDataProtector(),
            new[] { new DemoSaveMigrator() });
        services.Register<SaveDataService>(saveService);

        var updateService = new ContentUpdateService(
            _contentStore,
            _trustStore,
            rollbackVerifier: null,
            applicationId: "lyking-demo",
            platformId: "headless",
            utcNow: () => _manifestTime);
        services.Register<ContentUpdateService>(updateService);
        services.Register<IContentVersionStore>(_contentStore);
    }

    public async Task RunAsync(IServiceRegistry services)
    {
        Console.WriteLine("---- 商业客户端服务演示 ----");

        PlatformIdentity identity = await services.Resolve<IPlatformAccountService>().SignInAsync();
        Console.WriteLine($"[账号] 使用 {identity.Provider} 模拟身份登录。短期凭据不会写入存档或遥测。");
        await services.Resolve<IPlatformProgressService>().SubmitAchievementAsync("chapter.1", 1d);

        var ads = services.Resolve<SimulationAdsService>();
        ads.SetOutcome("rewarded.double", AdFormat.Rewarded, AdShowStatus.Completed);
        AdShowResult deniedAd = await ads.ShowAsync("rewarded.double", AdFormat.Rewarded, personalized: true);
        _consent.SetConsent(ConsentPurpose.Analytics, true);
        _consent.SetConsent(ConsentPurpose.Performance, true);
        _consent.SetConsent(ConsentPurpose.PersonalizedAds, true);
        _consent.SetConsent(ConsentPurpose.PushNotifications, true);
        AdShowResult shownAd = await ads.ShowAsync("rewarded.double", AdFormat.Rewarded, personalized: true);
        Console.WriteLine($"[隐私/广告] 未授权结果 {deniedAd.Status}；授权后结果 {shownAd.Status}，奖励仍由游戏规则决定。");

        StorePurchaseResult purchase = await services.Resolve<IStoreService>().PurchaseAsync("starter.pack");
        if (purchase.Receipt is StoreReceipt receipt)
        {
            var verifier = new SimulationReceiptVerifier(new[] { receipt });
            services.Register<IReceiptVerifier>(verifier);
            ReceiptVerificationResult first = await verifier.VerifyAsync(receipt);
            ReceiptVerificationResult repeated = await verifier.VerifyAsync(receipt);
            IStoreService store = services.Resolve<IStoreService>();
            bool acknowledged = first.Status == ReceiptVerificationStatus.Verified
                && await store.AcknowledgeTransactionAsync(receipt, first);
            IReadOnlyList<StoreReceipt> restored = await store.RestorePurchasesAsync();
            Console.WriteLine($"[支付] 交易 {receipt.TransactionId} 验证为 {first.Status}；重复验证幂等，验证后首次确认={acknowledged}，恢复 {restored.Count} 笔。本客户端不发放权益。");
            _ = repeated;
        }

        RemoteConfigService remoteConfig = services.Resolve<RemoteConfigService>();
        RemoteConfigApplyResult configResult = remoteConfig.Apply(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["matchmaking.timeout"] = "45",
            ["shop.enabled"] = "true",
        });
        Console.WriteLine($"[远程配置] {configResult.Status}：匹配超时 {remoteConfig.Current["matchmaking.timeout"]} 秒。");

        var input = services.Resolve<InputService>();
        input.Map("player.confirm", "Confirm");
        input.ActionTriggered += action => Console.WriteLine($"[输入] {action.Device} 派发动作 {action.Action}。");
        input.Submit(new InputSample("keyboard", "Confirm", 1f));

        _push.NotificationReceived += notification => Console.WriteLine($"[推送] 收到模拟通知 {notification.NotificationId}。");
        await _push.RegisterAsync();
        _push.Emit(new PushNotification("event.welcome"));
        _links.LinkReceived += link => Console.WriteLine($"[深链] 打开活动入口 {link.Campaign}。");
        _links.AttributionReceived += link => Console.WriteLine($"[归因] 已获同意的活动来源 {link.Source}。");
        await _links.StartAsync();
        _links.EmitLink(new LinkAttribution("https://game.example/open", "launch-week", "demo"));

        var telemetry = services.Resolve<SimulationObservabilityService>();
        telemetry.Track("level.started", new Dictionary<string, object?>
        {
            ["level"] = 1,
            ["access_token"] = "演示敏感字段",
        });
        telemetry.RecordPerformance("frame.time.ms", 16.7d);
        Console.WriteLine($"[遥测] 隐私同意下排队 {telemetry.PendingCount} 条，敏感字段由模拟出口过滤。");
        _ = telemetry.Drain();

        await DemonstrateSaveMigrationAsync(services.Resolve<SaveDataService>());
        await DemonstrateContentUpdateAsync(services);

        _consent.SetConsent(ConsentPurpose.Analytics, false);
        Console.WriteLine($"[遥测] 撤回分析同意后继续发送：{telemetry.Track("level.ended")}。");
        await services.Resolve<IPlatformAccountService>().SignOutAsync();
        Console.WriteLine("---- 商业客户端服务演示结束 ----");
    }

    public void Dispose()
    {
        _push.Dispose();
        _observability.Dispose();
        _publisherKey.Dispose();
        _trustedPublicKey.Dispose();
        if (Directory.Exists(_temporaryRoot))
        {
            Directory.Delete(_temporaryRoot, recursive: true);
        }
    }

    private async Task DemonstrateSaveMigrationAsync(SaveDataService saves)
    {
        await saves.SaveAsync("profile", 1, Encoding.UTF8.GetBytes("玩家=流光"));
        byte[] migrated = await saves.LoadAsync("profile", currentPayloadVersion: 2);
        Console.WriteLine($"[存档] 迁移并原子保存到版本 2：{Encoding.UTF8.GetString(migrated)}。");
    }

    private async Task DemonstrateContentUpdateAsync(IServiceRegistry services)
    {
        byte[] package = Encoding.UTF8.GetBytes("欢迎来到演示活动");
        string hash;
        using (SHA256 sha256 = SHA256.Create())
        {
            hash = Convert.ToHexString(sha256.ComputeHash(package)).ToLowerInvariant();
        }
        var manifest = new ContentManifest(
            "lyking-demo",
            "headless",
            "demo-content-1",
            sequence: 1,
            _manifestTime.AddMinutes(-1),
            _manifestTime.AddHours(1),
            "demo-root",
            new[] { new ContentFileEntry("welcome.txt", hash, package.LongLength) });
        byte[] canonical = ContentManifestCodec.EncodeCanonical(manifest);
        manifest = manifest.WithSignature(_publisherKey.SignData(canonical, HashAlgorithmName.SHA256));

        await services.Resolve<ContentUpdateService>().ApplyAsync(
            manifest,
            new SimulationContentPackageSource(new Dictionary<string, byte[]>(StringComparer.Ordinal)
            {
                ["welcome.txt"] = package,
            }));
        byte[]? active = await services.Resolve<IContentVersionStore>().ReadActiveFileAsync("welcome.txt", CancellationToken.None);
        Console.WriteLine($"[内容更新] 签名与块校验通过，原子激活版本 demo-content-1：{Encoding.UTF8.GetString(active ?? Array.Empty<byte>())}。");
    }

    private static bool IsValidConfig(IReadOnlyDictionary<string, string> values) =>
        values.TryGetValue("matchmaking.timeout", out string? rawTimeout)
        && int.TryParse(rawTimeout, out int timeout)
        && timeout is >= 5 and <= 120
        && values.TryGetValue("shop.enabled", out string? rawEnabled)
        && bool.TryParse(rawEnabled, out _);

    private sealed class DemoSaveMigrator : ISaveDataMigrator
    {
        public int FromVersion => 1;
        public int ToVersion => 2;

        public ValueTask<byte[]> MigrateAsync(byte[] payload, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string oldValue = Encoding.UTF8.GetString(payload);
            return new ValueTask<byte[]>(Encoding.UTF8.GetBytes("角色=" + oldValue.Substring("玩家=".Length)));
        }
    }
}
