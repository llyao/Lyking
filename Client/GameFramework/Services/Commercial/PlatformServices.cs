using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Lyking.GameFramework.Client.Core.Dispatching;

namespace Lyking.GameFramework.Client.Services.Commercial
{
    public sealed class PlatformIdentity
    {
        public PlatformIdentity(string provider, string userId, string displayName)
        {
            Provider = RequireText(provider, nameof(provider));
            UserId = RequireText(userId, nameof(userId));
            DisplayName = displayName ?? string.Empty;
        }

        public string Provider { get; }
        public string UserId { get; }
        public string DisplayName { get; }

        private static string RequireText(string value, string parameterName) =>
            string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("值不能为空。", parameterName) : value;
    }

    public interface IPlatformAccountService
    {
        PlatformIdentity? CurrentIdentity { get; }
        ValueTask<PlatformIdentity> SignInAsync(CancellationToken cancellationToken = default);
        ValueTask SignOutAsync(CancellationToken cancellationToken = default);
    }

    public sealed class SimulationPlatformAccountService : IPlatformAccountService
    {
        private readonly PlatformIdentity _identity;

        public SimulationPlatformAccountService(PlatformIdentity? identity = null)
        {
            _identity = identity ?? new PlatformIdentity("simulation", "simulation-user", "模拟玩家");
        }

        public PlatformIdentity? CurrentIdentity { get; private set; }

        public ValueTask<PlatformIdentity> SignInAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CurrentIdentity = _identity;
            return new ValueTask<PlatformIdentity>(_identity);
        }

        public ValueTask SignOutAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CurrentIdentity = null;
            return default;
        }
    }

    public sealed class AchievementProgress
    {
        public AchievementProgress(string achievementId, double progress)
        {
            AchievementId = string.IsNullOrWhiteSpace(achievementId)
                ? throw new ArgumentException("成就 ID 不能为空。", nameof(achievementId))
                : achievementId;
            if (double.IsNaN(progress) || double.IsInfinity(progress))
            {
                throw new ArgumentOutOfRangeException(nameof(progress));
            }

            Progress = Math.Max(0d, Math.Min(1d, progress));
        }

        public string AchievementId { get; }
        public double Progress { get; }
    }

    public sealed class LeaderboardEntry
    {
        public LeaderboardEntry(string userId, string displayName, long score, int rank)
        {
            UserId = userId ?? throw new ArgumentNullException(nameof(userId));
            DisplayName = displayName ?? string.Empty;
            Score = score;
            Rank = rank;
        }

        public string UserId { get; }
        public string DisplayName { get; }
        public long Score { get; }
        public int Rank { get; }
    }

    public interface IPlatformProgressService
    {
        IReadOnlyList<AchievementProgress> Achievements { get; }
        ValueTask SubmitAchievementAsync(string achievementId, double progress, CancellationToken cancellationToken = default);
        ValueTask SubmitScoreAsync(string leaderboardId, long score, CancellationToken cancellationToken = default);
        ValueTask<IReadOnlyList<LeaderboardEntry>> GetLeaderboardAsync(string leaderboardId, int limit, CancellationToken cancellationToken = default);
    }

    public sealed class SimulationPlatformProgressService : IPlatformProgressService
    {
        private readonly Dictionary<string, AchievementProgress> _achievements = new Dictionary<string, AchievementProgress>(StringComparer.Ordinal);
        private readonly Dictionary<string, long> _scores = new Dictionary<string, long>(StringComparer.Ordinal);

        public IReadOnlyList<AchievementProgress> Achievements => _achievements.Values.ToArray();

        public ValueTask SubmitAchievementAsync(string achievementId, double progress, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var next = new AchievementProgress(achievementId, progress);
            if (!_achievements.TryGetValue(achievementId, out AchievementProgress? current) || next.Progress > current.Progress)
            {
                _achievements[achievementId] = next;
            }

            return default;
        }

        public ValueTask SubmitScoreAsync(string leaderboardId, long score, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RequireId(leaderboardId, nameof(leaderboardId));
            if (!_scores.TryGetValue(leaderboardId, out long best) || score > best)
            {
                _scores[leaderboardId] = score;
            }

            return default;
        }

        public ValueTask<IReadOnlyList<LeaderboardEntry>> GetLeaderboardAsync(
            string leaderboardId,
            int limit,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RequireId(leaderboardId, nameof(leaderboardId));
            if (limit < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(limit));
            }

            IReadOnlyList<LeaderboardEntry> entries = _scores.TryGetValue(leaderboardId, out long score)
                ? new[] { new LeaderboardEntry("simulation-user", "模拟玩家", score, 1) }
                : Array.Empty<LeaderboardEntry>();
            return new ValueTask<IReadOnlyList<LeaderboardEntry>>(entries.Take(limit).ToArray());
        }

        private static void RequireId(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("ID 不能为空。", parameterName);
            }
        }
    }

    public enum ConsentPurpose
    {
        Analytics,
        CrashReporting,
        Performance,
        PersonalizedAds,
        PushNotifications,
    }

    public sealed class ConsentChanged : EventArgs
    {
        public ConsentChanged(ConsentPurpose purpose, bool granted)
        {
            Purpose = purpose;
            Granted = granted;
        }

        public ConsentPurpose Purpose { get; }
        public bool Granted { get; }
    }

    public interface IConsentService
    {
        event Action<ConsentChanged>? Changed;
        bool HasConsent(ConsentPurpose purpose);
        bool SetConsent(ConsentPurpose purpose, bool granted);
    }

    public sealed class SimulationConsentService : IConsentService
    {
        private readonly IMainThreadDispatcher _dispatcher;
        private readonly object _gate = new object();
        private readonly HashSet<ConsentPurpose> _granted = new HashSet<ConsentPurpose>();

        public SimulationConsentService(IMainThreadDispatcher dispatcher)
        {
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        }

        public event Action<ConsentChanged>? Changed;

        public bool HasConsent(ConsentPurpose purpose)
        {
            lock (_gate)
            {
                return _granted.Contains(purpose);
            }
        }

        public bool SetConsent(ConsentPurpose purpose, bool granted)
        {
            if (!_dispatcher.IsMainThread)
            {
                throw new InvalidOperationException("隐私同意必须在框架主线程变更。");
            }

            bool changed;
            lock (_gate)
            {
                changed = granted ? _granted.Add(purpose) : _granted.Remove(purpose);
            }

            if (changed)
            {
                NotifyObservers(new ConsentChanged(purpose, granted));
            }

            return changed;
        }

        private void NotifyObservers(ConsentChanged change)
        {
            Action<ConsentChanged>? observers = Changed;
            if (observers == null)
            {
                return;
            }

            List<Exception>? errors = null;
            foreach (Delegate observer in observers.GetInvocationList())
            {
                try
                {
                    ((Action<ConsentChanged>)observer)(change);
                }
                catch (Exception exception)
                {
                    if (errors == null)
                    {
                        errors = new List<Exception>();
                    }

                    errors.Add(exception);
                }
            }

            if (errors != null)
            {
                throw new AggregateException("一个或多个隐私同意观察者执行失败。", errors);
            }
        }
    }
}
