using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Lyking.GameFramework.Client.Core.Dispatching;

namespace Lyking.GameFramework.Client.Services.Commercial
{
    public enum AdFormat
    {
        Banner,
        Interstitial,
        Rewarded,
    }

    public enum AdShowStatus
    {
        Completed,
        Skipped,
        Unavailable,
        Failed,
    }

    public sealed class AdShowResult
    {
        public AdShowResult(AdShowStatus status, string? error = null)
        {
            Status = status;
            Error = error;
        }

        public AdShowStatus Status { get; }
        public string? Error { get; }
    }

    public interface IAdsService
    {
        ValueTask<AdShowResult> ShowAsync(
            string placement,
            AdFormat format,
            bool personalized = false,
            CancellationToken cancellationToken = default);
        void Hide(string placement);
    }

    public sealed class SimulationAdsService : IAdsService
    {
        private readonly IConsentService _consent;
        private readonly Dictionary<string, AdShowStatus> _outcomes = new Dictionary<string, AdShowStatus>(StringComparer.Ordinal);
        private readonly HashSet<string> _visiblePlacements = new HashSet<string>(StringComparer.Ordinal);

        public SimulationAdsService(IConsentService consent)
        {
            _consent = consent ?? throw new ArgumentNullException(nameof(consent));
        }

        public void SetOutcome(string placement, AdFormat format, AdShowStatus status)
        {
            RequireText(placement, nameof(placement));
            _outcomes[MakeKey(placement, format)] = status;
        }

        public ValueTask<AdShowResult> ShowAsync(
            string placement,
            AdFormat format,
            bool personalized = false,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RequireText(placement, nameof(placement));
            if (personalized && !_consent.HasConsent(ConsentPurpose.PersonalizedAds))
            {
                return new ValueTask<AdShowResult>(new AdShowResult(AdShowStatus.Unavailable));
            }

            if (format == AdFormat.Banner)
            {
                _visiblePlacements.Add(placement);
            }

            AdShowStatus status = _outcomes.TryGetValue(MakeKey(placement, format), out AdShowStatus configured)
                ? configured
                : AdShowStatus.Unavailable;
            return new ValueTask<AdShowResult>(new AdShowResult(status));
        }

        public void Hide(string placement)
        {
            RequireText(placement, nameof(placement));
            _visiblePlacements.Remove(placement);
        }

        public bool IsVisible(string placement) => _visiblePlacements.Contains(placement);

        private static string MakeKey(string placement, AdFormat format) => ((int)format).ToString(System.Globalization.CultureInfo.InvariantCulture) + ":" + placement;

        private static void RequireText(string value, string name)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("值不能为空。", name);
            }
        }
    }

    public sealed class PushNotification
    {
        public PushNotification(string notificationId, IReadOnlyDictionary<string, string>? data = null)
        {
            NotificationId = string.IsNullOrWhiteSpace(notificationId)
                ? throw new ArgumentException("通知 ID 不能为空。", nameof(notificationId))
                : notificationId;
            Data = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(
                data ?? new Dictionary<string, string>(), StringComparer.Ordinal));
        }

        public string NotificationId { get; }
        public IReadOnlyDictionary<string, string> Data { get; }
    }

    public interface IPushNotificationService
    {
        event Action<PushNotification>? NotificationReceived;
        bool IsRegistered { get; }
        ValueTask RegisterAsync(CancellationToken cancellationToken = default);
    }

    public sealed class SimulationPushNotificationService : IPushNotificationService, IDisposable
    {
        private readonly IConsentService _consent;
        private readonly IMainThreadDispatcher _dispatcher;
        private int _registrationVersion;
        private volatile bool _isRegistered;

        public SimulationPushNotificationService(IConsentService consent, IMainThreadDispatcher dispatcher)
        {
            _consent = consent ?? throw new ArgumentNullException(nameof(consent));
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
            _consent.Changed += OnConsentChanged;
        }

        public event Action<PushNotification>? NotificationReceived;
        public bool IsRegistered => _isRegistered;

        public ValueTask RegisterAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return new ValueTask(_dispatcher.InvokeAsync(() =>
            {
                _isRegistered = _consent.HasConsent(ConsentPurpose.PushNotifications);
                Interlocked.Increment(ref _registrationVersion);
            }, cancellationToken));
        }

        public void Dispose()
        {
            _isRegistered = false;
            Interlocked.Increment(ref _registrationVersion);
            _consent.Changed -= OnConsentChanged;
        }

        public void Emit(PushNotification notification)
        {
            if (notification == null)
            {
                throw new ArgumentNullException(nameof(notification));
            }

            if (IsRegistered)
            {
                int registrationVersion = Volatile.Read(ref _registrationVersion);
                Dispatch(() =>
                {
                    if (IsRegistered
                        && registrationVersion == Volatile.Read(ref _registrationVersion)
                        && _consent.HasConsent(ConsentPurpose.PushNotifications))
                    {
                        NotificationReceived?.Invoke(notification);
                    }
                });
            }
        }

        private void Dispatch(Action action)
        {
            if (_dispatcher.IsMainThread)
            {
                action();
            }
            else
            {
                _dispatcher.Post(action);
            }
        }

        private void OnConsentChanged(ConsentChanged change)
        {
            if (change.Purpose == ConsentPurpose.PushNotifications && !change.Granted)
            {
                _isRegistered = false;
                Interlocked.Increment(ref _registrationVersion);
            }
        }
    }

    public sealed class LinkAttribution
    {
        public LinkAttribution(string url, string? campaign = null, string? source = null)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? parsed)
                || string.Equals(parsed.Scheme, "javascript", StringComparison.OrdinalIgnoreCase)
                || string.Equals(parsed.Scheme, "vbscript", StringComparison.OrdinalIgnoreCase)
                || string.Equals(parsed.Scheme, "data", StringComparison.OrdinalIgnoreCase)
                || string.Equals(parsed.Scheme, Uri.UriSchemeFile, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("链接格式无效。", nameof(url));
            }

            Url = url;
            Campaign = campaign ?? string.Empty;
            Source = source ?? string.Empty;
        }

        public string Url { get; }
        public string Campaign { get; }
        public string Source { get; }
    }

    public interface ILinkAttributionService
    {
        event Action<LinkAttribution>? LinkReceived;
        event Action<LinkAttribution>? AttributionReceived;
        ValueTask StartAsync(CancellationToken cancellationToken = default);
    }

    public sealed class SimulationLinkAttributionService : ILinkAttributionService
    {
        private readonly IConsentService _consent;
        private readonly IMainThreadDispatcher _dispatcher;
        private volatile bool _isStarted;

        public SimulationLinkAttributionService(IConsentService consent, IMainThreadDispatcher dispatcher)
        {
            _consent = consent ?? throw new ArgumentNullException(nameof(consent));
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        }

        public event Action<LinkAttribution>? LinkReceived;
        public event Action<LinkAttribution>? AttributionReceived;
        public bool IsStarted => _isStarted;

        public ValueTask StartAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return new ValueTask(_dispatcher.InvokeAsync(() => _isStarted = true, cancellationToken));
        }

        public void EmitLink(LinkAttribution link)
        {
            if (link == null)
            {
                throw new ArgumentNullException(nameof(link));
            }

            if (IsStarted)
            {
                bool attributionAllowedAtArrival = _consent.HasConsent(ConsentPurpose.Analytics);
                Dispatch(() =>
                {
                    if (!IsStarted)
                    {
                        return;
                    }

                    LinkReceived?.Invoke(link);
                    if (attributionAllowedAtArrival && _consent.HasConsent(ConsentPurpose.Analytics))
                    {
                        AttributionReceived?.Invoke(link);
                    }
                });
            }
        }

        private void Dispatch(Action action)
        {
            if (_dispatcher.IsMainThread)
            {
                action();
            }
            else
            {
                _dispatcher.Post(action);
            }
        }
    }

    public enum RemoteConfigApplyStatus
    {
        Applied,
        RejectedAndFallbackApplied,
    }

    public sealed class RemoteConfigApplyResult
    {
        public RemoteConfigApplyResult(RemoteConfigApplyStatus status)
        {
            Status = status;
        }

        public RemoteConfigApplyStatus Status { get; }
    }

    /// <summary>对远程快照执行全量校验，校验失败时整体回到经过校验的本地回退值。</summary>
    public sealed class RemoteConfigService
    {
        private readonly Dictionary<string, string> _fallback;
        private readonly Func<IReadOnlyDictionary<string, string>, bool> _validator;
        private IReadOnlyDictionary<string, string> _current;

        public RemoteConfigService(
            IReadOnlyDictionary<string, string> fallback,
            Func<IReadOnlyDictionary<string, string>, bool> validator)
        {
            if (fallback == null)
            {
                throw new ArgumentNullException(nameof(fallback));
            }

            _validator = validator ?? throw new ArgumentNullException(nameof(validator));
            _fallback = new Dictionary<string, string>(fallback, StringComparer.Ordinal);
            if (!_validator(new ReadOnlyDictionary<string, string>(_fallback)))
            {
                throw new ArgumentException("本地回退配置未通过校验。", nameof(fallback));
            }

            _current = Freeze(_fallback);
        }

        public IReadOnlyDictionary<string, string> Current => _current;

        public RemoteConfigApplyResult Apply(IReadOnlyDictionary<string, string> remoteValues)
        {
            if (remoteValues == null)
            {
                throw new ArgumentNullException(nameof(remoteValues));
            }

            var candidate = new Dictionary<string, string>(remoteValues, StringComparer.Ordinal);
            if (_validator(new ReadOnlyDictionary<string, string>(candidate)))
            {
                _current = Freeze(candidate);
                return new RemoteConfigApplyResult(RemoteConfigApplyStatus.Applied);
            }

            _current = Freeze(_fallback);
            return new RemoteConfigApplyResult(RemoteConfigApplyStatus.RejectedAndFallbackApplied);
        }

        private static IReadOnlyDictionary<string, string> Freeze(Dictionary<string, string> values) =>
            new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(values, StringComparer.Ordinal));
    }
}
