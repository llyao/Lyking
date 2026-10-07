using System;
using System.Collections.Generic;

namespace Lyking.MemoryMatch.Core
{
    /// <summary>单张卡牌的状态：背面朝下、正面翻开或已配对。</summary>
    public enum MemoryCardState
    {
        FaceDown,
        FaceUp,
        Matched,
    }

    /// <summary>棋局阶段：无翻开 → 一张翻开 → 两张等待判定，全部配对后胜利。</summary>
    public enum MemoryMatchPhase
    {
        Idle,
        OneOpen,
        TwoOpen,
        Won,
    }

    /// <summary>一张卡牌的只读快照：格位、图案与状态。</summary>
    public readonly struct MemoryCard
    {
        public MemoryCard(int index, int pattern, MemoryCardState state)
        {
            Index = index;
            Pattern = pattern;
            State = state;
        }

        public int Index { get; }

        public int Pattern { get; }

        public MemoryCardState State { get; }
    }

    /// <summary>
    /// 记忆翻牌核心棋局：4x4 共 16 张卡、8 种图案各两张，无 Unity 依赖、无内建时钟。
    /// 构造时以注入种子的 <see cref="Random"/> 做 Fisher-Yates 洗牌，同种子布局完全一致。
    /// 翻第二张立即判定：同图案双双配对，异图案进入翻回倒计时，由 <see cref="Tick"/> 按秒推进翻回。
    /// </summary>
    public sealed class MemoryBoard
    {
        private const int MaxEventLogs = 128;

        private readonly Random _random;
        private readonly int[] _patterns = new int[MemoryMatchRules.CardCount];
        private readonly MemoryCardState[] _states = new MemoryCardState[MemoryMatchRules.CardCount];
        private readonly List<MemoryMatchEventLog> _events = new List<MemoryMatchEventLog>();

        public MemoryBoard(int seed = 0)
        {
            _random = new Random(seed);

            var deck = new List<int>(MemoryMatchRules.CardCount);
            for (int pattern = 0; pattern < MemoryMatchRules.PatternCount; pattern++)
            {
                deck.Add(pattern);
                deck.Add(pattern);
            }

            // Fisher-Yates 洗牌：从尾向前，每个位置与剩余前缀内随机一格交换，保证确定性。
            for (int i = deck.Count - 1; i > 0; i--)
            {
                int j = _random.Next(i + 1);
                (deck[i], deck[j]) = (deck[j], deck[i]);
            }

            for (int i = 0; i < MemoryMatchRules.CardCount; i++) _patterns[i] = deck[i];
            Log(MemoryMatchEventType.Dealt);
        }

        public int CardCount => MemoryMatchRules.CardCount;

        public int PatternCount => MemoryMatchRules.PatternCount;

        public MemoryMatchPhase Phase { get; private set; } = MemoryMatchPhase.Idle;

        /// <summary>已用步数：每翻开第二张记 1 步。</summary>
        public int Moves { get; private set; }

        /// <summary>已配对的对数，全部配对即胜利。</summary>
        public int MatchedPairs { get; private set; }

        /// <summary>不配对翻回剩余秒数，仅在 <see cref="MemoryMatchPhase.TwoOpen"/> 阶段为正。</summary>
        public float FlipBackRemaining { get; private set; }

        public bool IsWon => Phase == MemoryMatchPhase.Won;

        /// <summary>第一张翻开卡的格位，无翻开时为 -1。</summary>
        public int FirstOpenIndex { get; private set; } = -1;

        /// <summary>第二张翻开卡的格位，仅 <see cref="MemoryMatchPhase.TwoOpen"/> 阶段有效。</summary>
        public int SecondOpenIndex { get; private set; } = -1;

        public IReadOnlyList<MemoryMatchEventLog> Events => _events;

        public MemoryCard GetCard(int index)
        {
            if (index < 0 || index >= CardCount) throw new ArgumentOutOfRangeException(nameof(index), index, "格位越界。");
            return new MemoryCard(index, _patterns[index], _states[index]);
        }

        /// <summary>翻开一张卡。TwoOpen 阶段、已翻开或已配对的格被拒绝，返回是否成功。</summary>
        public bool Flip(int index)
        {
            if (index < 0 || index >= CardCount) throw new ArgumentOutOfRangeException(nameof(index), index, "格位越界。");
            if (IsWon || Phase == MemoryMatchPhase.TwoOpen) return false;
            if (_states[index] != MemoryCardState.FaceDown) return false;

            _states[index] = MemoryCardState.FaceUp;
            bool isSecond = Phase == MemoryMatchPhase.OneOpen;
            if (isSecond) Moves++;
            Log(MemoryMatchEventType.Flipped, index);

            if (!isSecond)
            {
                FirstOpenIndex = index;
                Phase = MemoryMatchPhase.OneOpen;
                return true;
            }

            SecondOpenIndex = index;
            if (_patterns[FirstOpenIndex] == _patterns[index])
            {
                _states[FirstOpenIndex] = MemoryCardState.Matched;
                _states[index] = MemoryCardState.Matched;
                MatchedPairs++;
                Log(MemoryMatchEventType.Matched, index);
                FirstOpenIndex = -1;
                SecondOpenIndex = -1;
                Phase = MatchedPairs == PatternCount ? MemoryMatchPhase.Won : MemoryMatchPhase.Idle;
                if (IsWon) Log(MemoryMatchEventType.Won, index);
                return true;
            }

            Phase = MemoryMatchPhase.TwoOpen;
            FlipBackRemaining = MemoryMatchRules.FlipBackSeconds;
            Log(MemoryMatchEventType.Mismatched, index);
            return true;
        }

        /// <summary>推进翻回倒计时。仅 TwoOpen 阶段有效：剩余归零即把两张卡翻回并回到 Idle。</summary>
        public void Tick(float deltaTime)
        {
            if (Phase != MemoryMatchPhase.TwoOpen) return;

            FlipBackRemaining -= deltaTime;
            if (FlipBackRemaining > 0f) return;

            FlipBackRemaining = 0f;
            _states[FirstOpenIndex] = MemoryCardState.FaceDown;
            _states[SecondOpenIndex] = MemoryCardState.FaceDown;
            Log(MemoryMatchEventType.FlippedBack, SecondOpenIndex);
            FirstOpenIndex = -1;
            SecondOpenIndex = -1;
            Phase = MemoryMatchPhase.Idle;
        }

        private void Log(MemoryMatchEventType type, int cardIndex = -1)
        {
            _events.Add(new MemoryMatchEventLog(Moves, type, cardIndex));
            if (_events.Count > MaxEventLogs) _events.RemoveRange(0, _events.Count - MaxEventLogs);
        }
    }
}
