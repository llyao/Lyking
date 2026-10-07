using System;
using System.Linq;
using Lyking.MemoryMatch.Core;
using Xunit;

namespace Lyking.MemoryMatch.Core.Tests
{
    /// <summary>记忆翻牌核心规则测试：洗牌确定性、翻牌流程、配对判定、翻回倒计时、步数与胜利。</summary>
    public class MemoryBoardTests
    {
        [Fact]
        public void 构造后发牌成对且全部背面朝下()
        {
            var board = new MemoryBoard(seed: 42);

            Assert.Equal(MemoryMatchRules.CardCount, board.CardCount);
            Assert.Equal(MemoryMatchPhase.Idle, board.Phase);
            Assert.Equal(0, board.Moves);
            Assert.Equal(0, board.MatchedPairs);
            Assert.Equal(0f, board.FlipBackRemaining);
            Assert.Equal(-1, board.FirstOpenIndex);
            Assert.False(board.IsWon);
            Assert.All(Enumerable.Range(0, board.CardCount),
                i => Assert.Equal(MemoryCardState.FaceDown, board.GetCard(i).State));

            var patterns = PatternsOf(board);
            Assert.Equal(MemoryMatchRules.PatternCount, patterns.Distinct().Count());
            foreach (var group in patterns.GroupBy(p => p))
            {
                Assert.Equal(2, group.Count());
            }

            Assert.Equal(MemoryMatchEventType.Dealt, board.Events[0].Type);
        }

        [Fact]
        public void 相同种子产生相同布局()
        {
            Assert.Equal(PatternsOf(new MemoryBoard(42)), PatternsOf(new MemoryBoard(42)));
            Assert.NotEqual(PatternsOf(new MemoryBoard(1)), PatternsOf(new MemoryBoard(2)));
        }

        [Fact]
        public void 翻开第一张进入等待状态且不计步数()
        {
            var board = new MemoryBoard(seed: 42);
            var pair = FindMatchingPair(board);

            Assert.True(board.Flip(pair.First));
            Assert.Equal(MemoryMatchPhase.OneOpen, board.Phase);
            Assert.Equal(pair.First, board.FirstOpenIndex);
            Assert.Equal(MemoryCardState.FaceUp, board.GetCard(pair.First).State);
            Assert.Equal(0, board.Moves);
            Assert.Equal(MemoryMatchEventType.Flipped, board.Events[^1].Type);
            Assert.Equal(pair.First, board.Events[^1].CardIndex);
        }

        [Fact]
        public void 翻开第二张同图案立即配对()
        {
            var board = new MemoryBoard(seed: 42);
            var pair = FindMatchingPair(board);
            board.Flip(pair.First);

            Assert.True(board.Flip(pair.Second));
            Assert.Equal(MemoryCardState.Matched, board.GetCard(pair.First).State);
            Assert.Equal(MemoryCardState.Matched, board.GetCard(pair.Second).State);
            Assert.Equal(1, board.MatchedPairs);
            Assert.Equal(1, board.Moves);
            Assert.Equal(MemoryMatchPhase.Idle, board.Phase);
            Assert.Equal(-1, board.FirstOpenIndex);
            Assert.Contains(MemoryMatchEventType.Matched, board.Events.Select(e => e.Type).ToList());
        }

        [Fact]
        public void 翻开第二张异图案进入翻回倒计时()
        {
            var board = new MemoryBoard(seed: 42);
            var pair = FindMismatchPair(board);
            board.Flip(pair.First);

            Assert.True(board.Flip(pair.Second));
            Assert.Equal(MemoryMatchPhase.TwoOpen, board.Phase);
            Assert.Equal(MemoryMatchRules.FlipBackSeconds, board.FlipBackRemaining, 4);
            Assert.Equal(pair.First, board.FirstOpenIndex);
            Assert.Equal(pair.Second, board.SecondOpenIndex);
            Assert.Equal(1, board.Moves);
            Assert.Equal(MemoryCardState.FaceUp, board.GetCard(pair.First).State);
            Assert.Equal(MemoryCardState.FaceUp, board.GetCard(pair.Second).State);
            Assert.Contains(MemoryMatchEventType.Mismatched, board.Events.Select(e => e.Type).ToList());
        }

        [Fact]
        public void 倒计时未归零禁止翻第三张()
        {
            var board = new MemoryBoard(seed: 42);
            var pair = FindMismatchPair(board);
            board.Flip(pair.First);
            board.Flip(pair.Second);

            // 倒计时内任何格位都翻不开，包括已翻开的两张。
            Assert.All(Enumerable.Range(0, board.CardCount), i => Assert.False(board.Flip(i)));

            board.Tick(MemoryMatchRules.FlipBackSeconds * 0.5f);
            Assert.Equal(MemoryMatchPhase.TwoOpen, board.Phase);
            Assert.All(Enumerable.Range(0, board.CardCount), i => Assert.False(board.Flip(i)));
        }

        [Fact]
        public void 倒计时归零后两张翻回并回到待机()
        {
            var board = new MemoryBoard(seed: 42);
            var pair = FindMismatchPair(board);
            board.Flip(pair.First);
            board.Flip(pair.Second);

            board.Tick(MemoryMatchRules.FlipBackSeconds);

            Assert.Equal(MemoryMatchPhase.Idle, board.Phase);
            Assert.Equal(MemoryCardState.FaceDown, board.GetCard(pair.First).State);
            Assert.Equal(MemoryCardState.FaceDown, board.GetCard(pair.Second).State);
            Assert.Equal(-1, board.FirstOpenIndex);
            Assert.Equal(-1, board.SecondOpenIndex);
            Assert.Equal(0f, board.FlipBackRemaining);
            Assert.Equal(1, board.Moves);
            Assert.Contains(MemoryMatchEventType.FlippedBack, board.Events.Select(e => e.Type).ToList());
        }

        [Fact]
        public void 翻回后可以继续翻牌()
        {
            var board = new MemoryBoard(seed: 42);
            var mismatch = FindMismatchPair(board);
            board.Flip(mismatch.First);
            board.Flip(mismatch.Second);
            board.Tick(MemoryMatchRules.FlipBackSeconds);

            var pair = FindMatchingPair(board);
            Assert.True(board.Flip(pair.First));
            Assert.Equal(MemoryMatchPhase.OneOpen, board.Phase);
            Assert.True(board.Flip(pair.Second));
            Assert.Equal(MemoryMatchPhase.Idle, board.Phase);
            Assert.Equal(1, board.MatchedPairs);
            Assert.Equal(2, board.Moves);
        }

        [Fact]
        public void 同一格重复翻被拒绝()
        {
            var board = new MemoryBoard(seed: 42);
            var pair = FindMatchingPair(board);

            Assert.True(board.Flip(pair.First));
            Assert.False(board.Flip(pair.First));
            Assert.Equal(MemoryMatchPhase.OneOpen, board.Phase);
            Assert.Equal(0, board.Moves);
        }

        [Fact]
        public void 已配对的格不能再次翻开()
        {
            var board = new MemoryBoard(seed: 42);
            var pair = FindMatchingPair(board);
            board.Flip(pair.First);
            board.Flip(pair.Second);

            Assert.False(board.Flip(pair.First));
            Assert.False(board.Flip(pair.Second));
            Assert.Equal(MemoryCardState.Matched, board.GetCard(pair.First).State);
            Assert.Equal(1, board.MatchedPairs);
        }

        [Fact]
        public void 步数按翻第二张次数统计()
        {
            var board = new MemoryBoard(seed: 42);
            for (int round = 0; round < 2; round++)
            {
                var mismatch = FindMismatchPair(board);
                board.Flip(mismatch.First);
                board.Flip(mismatch.Second);
                Assert.Equal(MemoryMatchPhase.TwoOpen, board.Phase);
                board.Tick(MemoryMatchRules.FlipBackSeconds);
            }

            var pair = FindMatchingPair(board);
            board.Flip(pair.First);
            board.Flip(pair.Second);

            Assert.Equal(3, board.Moves);
            Assert.Equal(1, board.MatchedPairs);
        }

        [Fact]
        public void 全部配对后胜利且后续翻牌被拒绝()
        {
            var board = new MemoryBoard(seed: 42);
            PlayToWin(board);

            Assert.True(board.IsWon);
            Assert.Equal(MemoryMatchPhase.Won, board.Phase);
            Assert.Equal(MemoryMatchRules.PatternCount, board.MatchedPairs);
            Assert.Equal(MemoryMatchRules.PatternCount, board.Moves);
            Assert.Equal(MemoryMatchEventType.Won, board.Events[^1].Type);

            Assert.All(Enumerable.Range(0, board.CardCount), i => Assert.False(board.Flip(i)));
            board.Tick(MemoryMatchRules.FlipBackSeconds);
            Assert.Equal(0f, board.FlipBackRemaining);
        }

        [Fact]
        public void 同种子完整对局逐步一致()
        {
            static string Simulate(int seed)
            {
                var board = new MemoryBoard(seed);
                PlayToWin(board);
                var patterns = string.Join(';', PatternsOf(board));
                var eventTypes = string.Join(';', board.Events.Select(e => e.Type));
                return HashCode.Combine(board.Moves, board.MatchedPairs, patterns, eventTypes).ToString();
            }

            Assert.Equal(Simulate(777), Simulate(777));
            Assert.NotEqual(Simulate(777), Simulate(778));
        }

        private static int[] PatternsOf(MemoryBoard board) =>
            Enumerable.Range(0, board.CardCount).Select(i => board.GetCard(i).Pattern).ToArray();

        /// <summary>找一组同图案的格位；牌局中同图案必成对，找不到即布局非法。</summary>
        private static (int First, int Second) FindMatchingPair(MemoryBoard board)
        {
            for (int i = 0; i < board.CardCount; i++)
            {
                for (int j = i + 1; j < board.CardCount; j++)
                {
                    if (board.GetCard(i).Pattern == board.GetCard(j).Pattern) return (i, j);
                }
            }

            throw new InvalidOperationException("16 张卡 8 种图案必有同图案对。");
        }

        /// <summary>找一组不同图案的格位，用于触发不配对流程。</summary>
        private static (int First, int Second) FindMismatchPair(MemoryBoard board)
        {
            for (int j = 1; j < board.CardCount; j++)
            {
                if (board.GetCard(0).Pattern != board.GetCard(j).Pattern) return (0, j);
            }

            throw new InvalidOperationException("4x4 至少两种图案，必能找到不配对组合。");
        }

        /// <summary>以确定性策略打完整局：每次翻第一张背面卡，再翻它的同图案伙伴。</summary>
        private static void PlayToWin(MemoryBoard board)
        {
            for (int guard = 0; guard < 64 && !board.IsWon; guard++)
            {
                int first = FirstFaceDown(board);
                board.Flip(first);

                int second = -1;
                for (int i = 0; i < board.CardCount; i++)
                {
                    if (i != first && board.GetCard(i).Pattern == board.GetCard(first).Pattern
                        && board.GetCard(i).State == MemoryCardState.FaceDown)
                    {
                        second = i;
                        break;
                    }
                }

                if (second < 0) throw new InvalidOperationException("同图案伙伴必然仍在背面。");
                board.Flip(second);
                if (board.Phase == MemoryMatchPhase.TwoOpen) board.Tick(MemoryMatchRules.FlipBackSeconds);
            }

            if (!board.IsWon) throw new InvalidOperationException("确定性策略应在 8 次尝试内胜利。");
        }

        private static int FirstFaceDown(MemoryBoard board)
        {
            for (int i = 0; i < board.CardCount; i++)
            {
                if (board.GetCard(i).State == MemoryCardState.FaceDown) return i;
            }

            throw new InvalidOperationException("未全部配对时必有背面卡。");
        }
    }
}
