using System;
using System.Collections.Generic;
using Lyking.GameFramework.Client.Core.Logging;
using Lyking.GameFramework.Client.Core.Modules;

namespace Lyking.GameFramework.Client.Core.Scheduling
{
    /// <summary>计时器句柄：取消与查询状态。</summary>
    public interface ITimerHandle
    {
        bool IsCancelled { get; }

        bool IsDone { get; }

        /// <summary>取消计时器；幂等。</summary>
        void Cancel();
    }

    /// <summary>
    /// 计时服务：以帧时间推进的延迟与重复回调调度，随帧循环统一驱动。
    /// 回调在主线程执行；回调内新增计时器下一帧生效，回调内取消自身安全。
    /// 回调异常不在此隔离，交由 ModuleHost 的帧异常隔离处理。
    /// </summary>
    public sealed class TimerService : ClientModule
    {
        private readonly List<TimerRecord> _timers = new List<TimerRecord>();
        private readonly List<TimerRecord> _firing = new List<TimerRecord>();
        private double _elapsed;

        public TimerService() : base("Timer", priority: -1000)
        {
        }

        /// <summary>当前累计帧时间（秒）。</summary>
        public double Elapsed => _elapsed;

        /// <summary>调度一次性回调，delay ≤ 0 表示下一帧触发。</summary>
        public ITimerHandle Schedule(TimeSpan delay, Action callback)
        {
            if (callback is null)
            {
                throw new ArgumentNullException(nameof(callback));
            }

            var record = new TimerRecord(this, _elapsed + Math.Max(0, delay.TotalSeconds), interval: null, callback);
            _timers.Add(record);
            return record;
        }

        /// <summary>调度重复回调，interval 必须 > 0；首次在 interval 后触发。</summary>
        public ITimerHandle ScheduleRepeating(TimeSpan interval, Action callback)
        {
            if (callback is null)
            {
                throw new ArgumentNullException(nameof(callback));
            }

            double seconds = interval.TotalSeconds;
            if (seconds <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(interval), "重复间隔必须大于零。");
            }

            var record = new TimerRecord(this, _elapsed + seconds, seconds, callback);
            _timers.Add(record);
            return record;
        }

        public override void Update(double deltaTime)
        {
            _elapsed += deltaTime;

            // 快照到期项再执行：回调内新增计时器下一帧生效，回调内取消安全。
            _firing.Clear();
            for (int i = _timers.Count - 1; i >= 0; i--)
            {
                TimerRecord record = _timers[i];
                if (record.IsCancelled)
                {
                    _timers.RemoveAt(i);
                    continue;
                }

                if (record.NextFireAt > _elapsed)
                {
                    continue;
                }

                if (record.Interval is double interval)
                {
                    record.NextFireAt += interval;
                    if (record.NextFireAt <= _elapsed)
                    {
                        // 帧率低于间隔时按当前帧补齐时刻，避免连发堆积。
                        record.NextFireAt = _elapsed + interval;
                    }

                    _firing.Add(record);
                }
                else
                {
                    record.IsDone = true;
                    _timers.RemoveAt(i);
                    _firing.Add(record);
                }
            }

            for (int i = _firing.Count - 1; i >= 0; i--)
            {
                TimerRecord record = _firing[i];
                if (!record.IsCancelled)
                {
                    record.Callback();
                }
            }
        }

        private sealed class TimerRecord : ITimerHandle
        {
            private readonly TimerService _owner;

            public TimerRecord(TimerService owner, double nextFireAt, double? interval, Action callback)
            {
                _owner = owner;
                NextFireAt = nextFireAt;
                Interval = interval;
                Callback = callback;
            }

            public double NextFireAt { get; set; }

            public double? Interval { get; }

            public Action Callback { get; }

            public bool IsCancelled { get; private set; }

            public bool IsDone { get; internal set; }

            public void Cancel() => IsCancelled = true;
        }
    }
}
