using System;
using System.Collections.Generic;

namespace Lyking.WhackAMole.Core
{
    /// <summary>对局阶段：未开始 → 倒计时进行中 → 已结束。</summary>
    public enum WhackAMolePhase
    {
        /// <summary>未开始：等待首次输入，倒计时不走。</summary>
        Ready,

        /// <summary>进行中：倒计时递减，地鼠按种子节奏冒头。</summary>
        Running,

        /// <summary>已结束：时间到，打击与推进均无效。</summary>
        GameOver,
    }

    /// <summary>
    /// 打地鼠核心对局：3x3 共 9 个地洞、30 秒限时计分，无 Unity 依赖、无内建时钟。
    /// 同屏最多 1 只地鼠，洞位、类型与冒头间隔由注入种子的 <see cref="Random"/> 依次决定，
    /// 同种子对局完全一致。Ready 阶段首次输入只负责开局，之后每次打击：命中普通地鼠得基础分
    /// 加连击加成，命中炸弹地鼠扣分且总分夹 0 下限，打空连击清零；命中即缩回，到时未打自动缩回。
    /// 时间由 <see cref="Tick"/> 按调用粒度推进，单次大步长不补偿中间错过的冒头周期。
    /// </summary>
    public sealed class WhackAMoleGame
    {
        private readonly Random _random;
        private readonly List<WhackAMoleEventLog> _events = new List<WhackAMoleEventLog>();

        private float _elapsed;
        private float _nextSpawnTime;
        private int _pendingHole = -1;
        private bool _pendingIsBomb;
        private int _moleHole = -1;
        private bool _moleIsBomb;
        private float _moleSpawnTime;
        private float _moleRetractTime;

        public WhackAMoleGame(int seed = 0)
        {
            _random = new Random(seed);
        }

        public WhackAMolePhase Phase { get; private set; } = WhackAMolePhase.Ready;

        /// <summary>当前总分，炸弹扣分后夹 0 下限。</summary>
        public int Score { get; private set; }

        /// <summary>当前连击数：命中普通地鼠加 1，打空或命中炸弹清零。</summary>
        public int Combo { get; private set; }

        /// <summary>对局内已流逝的秒数，Ready 阶段恒为 0。</summary>
        public float Elapsed => _elapsed;

        /// <summary>剩余秒数，夹 0 下限。</summary>
        public float TimeRemaining => Math.Max(0f, WhackAMoleRules.RoundSeconds - _elapsed);

        public bool IsFinished => Phase == WhackAMolePhase.GameOver;

        /// <summary>当前是否有地鼠冒头；同屏最多 1 只。</summary>
        public bool HasMole => _moleHole >= 0;

        /// <summary>冒头地鼠的洞位，无冒头时为 -1。</summary>
        public int MoleHole => _moleHole;

        /// <summary>冒头地鼠是否为炸弹。</summary>
        public bool MoleIsBomb => _moleIsBomb;

        /// <summary>冒头地鼠的冒头时刻（对局内秒数），仅 <see cref="HasMole"/> 时有效。</summary>
        public float MoleSpawnTime => _moleSpawnTime;

        /// <summary>冒头地鼠的计划缩回时刻（对局内秒数），仅 <see cref="HasMole"/> 时有效。</summary>
        public float MoleRetractTime => _moleRetractTime;

        public IReadOnlyList<WhackAMoleEventLog> Events => _events;

        /// <summary>
        /// 打击一个地洞。Ready 阶段的首次输入只负责开局、不判定命中；
        /// 打空与打错洞记打空并清零连击；命中普通地鼠或炸弹地鼠立即缩回并调度下一只。
        /// 返回是否打中了地鼠。
        /// </summary>
        public bool Whack(int holeIndex)
        {
            if (holeIndex < 0 || holeIndex >= WhackAMoleRules.HoleCount)
                throw new ArgumentOutOfRangeException(nameof(holeIndex), holeIndex, "地洞编号越界。");

            if (Phase == WhackAMolePhase.Ready)
            {
                StartRound();
                return false;
            }

            if (Phase != WhackAMolePhase.Running) return false;

            if (_moleHole < 0 || holeIndex != _moleHole)
            {
                // 打空：空洞与打错洞同样处理，总分不变、连击清零。
                Combo = 0;
                Log(WhackAMoleEventType.Missed, holeIndex);
                return false;
            }

            int hole = _moleHole;
            bool isBomb = _moleIsBomb;
            HideMole();
            if (isBomb)
            {
                Score = Math.Max(0, Score - WhackAMoleRules.BombMolePenalty);
                Combo = 0;
                Log(WhackAMoleEventType.BombHit, hole);
            }
            else
            {
                Combo++;
                Score += WhackAMoleRules.NormalMoleScore + WhackAMoleRules.ComboBonusFor(Combo);
                Log(WhackAMoleEventType.Hit, hole);
            }

            ScheduleNextSpawn();
            return true;
        }

        /// <summary>推进对局：已冒头地鼠到时自动缩回、倒计时归零结束、到点冒出新地鼠。Ready 与结束阶段无效。</summary>
        public void Tick(float deltaTime)
        {
            if (Phase != WhackAMolePhase.Running) return;

            _elapsed += deltaTime;

            // 1) 已冒头地鼠到时自动缩回，随即调度下一只。
            if (_moleHole >= 0 && _elapsed >= _moleRetractTime)
            {
                int hole = _moleHole;
                HideMole();
                Log(WhackAMoleEventType.Retreated, hole);
                ScheduleNextSpawn();
            }

            // 2) 倒计时归零：时间到并结束，未被打的地鼠随对局直接消失，不再单独记缩回。
            if (_elapsed >= WhackAMoleRules.RoundSeconds)
            {
                FinishRound();
                return;
            }

            // 3) 无冒头地鼠且到达冒头时刻，冒出已调度好的下一只。
            if (_moleHole < 0 && _elapsed >= _nextSpawnTime)
            {
                SpawnMole();
            }
        }

        /// <summary>首次输入开局：倒计时归零重计，写入开始事件并调度第一只地鼠。</summary>
        private void StartRound()
        {
            _elapsed = 0f;
            Phase = WhackAMolePhase.Running;
            Log(WhackAMoleEventType.Started);
            ScheduleNextSpawn();
        }

        /// <summary>调度下一只地鼠：依次抽取冒头间隔、洞位与类型，三次抽签顺序固定保证确定性。</summary>
        private void ScheduleNextSpawn()
        {
            _nextSpawnTime = _elapsed + NextInterval();
            _pendingHole = _random.Next(WhackAMoleRules.HoleCount);
            _pendingIsBomb = _random.NextDouble() < WhackAMoleRules.BombChance;
        }

        private void SpawnMole()
        {
            _moleHole = _pendingHole;
            _moleIsBomb = _pendingIsBomb;
            _moleSpawnTime = _elapsed;
            float remaining = Math.Max(0f, WhackAMoleRules.RoundSeconds - _elapsed);
            _moleRetractTime = _elapsed + WhackAMoleRules.StaySecondsFor(remaining);
            Log(WhackAMoleEventType.Spawned, _moleHole);
        }

        private void HideMole()
        {
            _moleHole = -1;
        }

        private void FinishRound()
        {
            _elapsed = WhackAMoleRules.RoundSeconds;
            HideMole();
            Phase = WhackAMolePhase.GameOver;
            Log(WhackAMoleEventType.TimeUp);
            Log(WhackAMoleEventType.Finished);
        }

        private float NextInterval() =>
            (float)(WhackAMoleRules.MinSpawnInterval
                + (WhackAMoleRules.MaxSpawnInterval - WhackAMoleRules.MinSpawnInterval) * _random.NextDouble());

        private void Log(WhackAMoleEventType type, int holeIndex = -1)
        {
            _events.Add(new WhackAMoleEventLog(_elapsed, type, holeIndex, Score, Combo));
            if (_events.Count > WhackAMoleRules.MaxEventLogs)
                _events.RemoveRange(0, _events.Count - WhackAMoleRules.MaxEventLogs);
        }
    }
}
