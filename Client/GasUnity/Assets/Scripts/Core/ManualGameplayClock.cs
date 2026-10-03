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
            if (delaySeconds < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(delaySeconds));
            }

            if (callback is null)
            {
                throw new ArgumentNullException(nameof(callback));
            }

            int id = ++_nextTimerId;
            _timers.Add(new Timer { Id = id, DueAt = Now + delaySeconds, Callback = callback });
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
            if (seconds < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(seconds));
            }

            Now += seconds;
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
    }
}
