#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace Lyking.GameplayAbilities.Core
{
    public sealed class ManualGameplayClock : IGameplayClock
    {
        private sealed class Timer
        {
            public int Id;
            public double DueAt;
            public Action Callback = null!;
            public bool Cancelled;
        }

        private readonly List<Timer> _timers = new List<Timer>();
        private int _nextTimerId;

        public double Now { get; private set; }

        public int Schedule(double delaySeconds, Action callback)
        {
            if (delaySeconds < 0 || double.IsNaN(delaySeconds) || double.IsInfinity(delaySeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(delaySeconds), "定时延迟必须是非负有限数值。");
            }

            if (callback is null)
            {
                throw new ArgumentNullException(nameof(callback));
            }

            double dueAt = Now + delaySeconds;
            if (!IsFinite(dueAt))
            {
                throw new ArgumentOutOfRangeException(nameof(delaySeconds), "定时点必须是有限数值。");
            }

            int id = ++_nextTimerId;
            _timers.Add(new Timer { Id = id, DueAt = dueAt, Callback = callback });
            return id;
        }

        public void Cancel(int timerId)
        {
            Timer? timer = _timers.FirstOrDefault(item => item.Id == timerId);
            if (timer is not null)
            {
                timer.Cancelled = true;
            }
        }

        public void Advance(double seconds)
        {
            if (seconds < 0 || double.IsNaN(seconds) || double.IsInfinity(seconds))
            {
                throw new ArgumentOutOfRangeException(nameof(seconds), "时钟推进时间必须是非负有限数值。");
            }

            double nextNow = Now + seconds;
            if (!IsFinite(nextNow))
            {
                throw new ArgumentOutOfRangeException(nameof(seconds), "时钟推进结果必须是有限数值。");
            }

            Now = nextNow;
            while (true)
            {
                Timer? next = _timers
                    .Where(timer => !timer.Cancelled && timer.DueAt <= Now)
                    .OrderBy(timer => timer.DueAt)
                    .ThenBy(timer => timer.Id)
                    .FirstOrDefault();
                if (next is null)
                {
                    break;
                }

                next.Cancelled = true;
                next.Callback();
            }

            _timers.RemoveAll(timer => timer.Cancelled);
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }
}
