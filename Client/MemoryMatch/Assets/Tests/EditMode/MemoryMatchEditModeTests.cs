using System.Linq;
using Lyking.MemoryMatch.Core;
using NUnit.Framework;
using UnityEngine;

namespace Lyking.MemoryMatch.EditModeTests
{
    /// <summary>编辑模式冒烟：核心棋局在 Unity 程序集边界内可用，关键规则表现一致。</summary>
    public sealed class MemoryMatchEditModeTests
    {
        [Test]
        public void 发牌后十六张卡就绪且图案成对()
        {
            var board = new MemoryBoard(seed: 7);

            Assert.AreEqual(MemoryMatchRules.CardCount, board.CardCount);
            Assert.AreEqual(MemoryMatchPhase.Idle, board.Phase);
            Assert.AreEqual(0, board.Moves);
            Assert.IsFalse(board.IsWon);
            Assert.AreEqual(MemoryMatchEventType.Dealt, board.Events[0].Type);

            var patterns = Enumerable.Range(0, board.CardCount).Select(i => board.GetCard(i).Pattern).ToArray();
            Assert.AreEqual(MemoryMatchRules.PatternCount, patterns.Distinct().Count());
            foreach (var group in patterns.GroupBy(p => p))
            {
                Assert.AreEqual(2, group.Count(), $"图案 {group.Key} 应恰好两张。");
            }
        }

        [Test]
        public void 翻开同图案两张立即配对()
        {
            var board = new MemoryBoard(seed: 7);
            var pair = FindMatchingPair(board);

            Assert.IsTrue(board.Flip(pair.First));
            Assert.AreEqual(MemoryMatchPhase.OneOpen, board.Phase);
            Assert.IsTrue(board.Flip(pair.Second));

            Assert.AreEqual(MemoryCardState.Matched, board.GetCard(pair.First).State);
            Assert.AreEqual(MemoryCardState.Matched, board.GetCard(pair.Second).State);
            Assert.AreEqual(1, board.MatchedPairs);
            Assert.AreEqual(1, board.Moves);
            Assert.AreEqual(MemoryMatchPhase.Idle, board.Phase);
        }

        [Test]
        public void 不配对倒计时归零后翻回()
        {
            var board = new MemoryBoard(seed: 7);
            var pair = FindMismatchPair(board);
            board.Flip(pair.First);
            board.Flip(pair.Second);

            Assert.AreEqual(MemoryMatchPhase.TwoOpen, board.Phase);
            Assert.AreEqual(MemoryMatchRules.FlipBackSeconds, board.FlipBackRemaining, 1e-4f);
            Assert.IsFalse(board.Flip((pair.First + 2) % board.CardCount));

            board.Tick(MemoryMatchRules.FlipBackSeconds);
            Assert.AreEqual(MemoryMatchPhase.Idle, board.Phase);
            Assert.AreEqual(MemoryCardState.FaceDown, board.GetCard(pair.First).State);
            Assert.AreEqual(MemoryCardState.FaceDown, board.GetCard(pair.Second).State);

            var types = board.Events.Select(e => e.Type).ToList();
            Assert.Contains(MemoryMatchEventType.Mismatched, types);
            Assert.Contains(MemoryMatchEventType.FlippedBack, types);
        }

        [Test]
        public void 全部配对后写入胜利事件()
        {
            var board = new MemoryBoard(seed: 3);
            PlayToWin(board);

            Assert.IsTrue(board.IsWon);
            Assert.AreEqual(MemoryMatchRules.PatternCount, board.MatchedPairs);
            Assert.AreEqual(MemoryMatchRules.PatternCount, board.Moves);
            var types = board.Events.Select(e => e.Type).ToList();
            Assert.Contains(MemoryMatchEventType.Matched, types);
            Assert.AreEqual(MemoryMatchEventType.Won, types[types.Count - 1]);
        }

        private static (int First, int Second) FindMatchingPair(MemoryBoard board)
        {
            for (int i = 0; i < board.CardCount; i++)
            {
                for (int j = i + 1; j < board.CardCount; j++)
                {
                    if (board.GetCard(i).Pattern == board.GetCard(j).Pattern) return (i, j);
                }
            }

            Assert.Fail("16 张卡 8 种图案必有同图案对。");
            return (0, 1);
        }

        private static (int First, int Second) FindMismatchPair(MemoryBoard board)
        {
            for (int j = 1; j < board.CardCount; j++)
            {
                if (board.GetCard(0).Pattern != board.GetCard(j).Pattern) return (0, j);
            }

            Assert.Fail("4x4 至少两种图案，必能找到不配对组合。");
            return (0, 1);
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

                Assert.GreaterOrEqual(second, 0, "同图案伙伴必然仍在背面。");
                board.Flip(second);
                if (board.Phase == MemoryMatchPhase.TwoOpen) board.Tick(MemoryMatchRules.FlipBackSeconds);
            }

            Assert.IsTrue(board.IsWon, "确定性策略应在 8 次尝试内胜利。");
        }

        private static int FirstFaceDown(MemoryBoard board)
        {
            for (int i = 0; i < board.CardCount; i++)
            {
                if (board.GetCard(i).State == MemoryCardState.FaceDown) return i;
            }

            Assert.Fail("未全部配对时必有背面卡。");
            return 0;
        }
    }
}
