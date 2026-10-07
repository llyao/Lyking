using System;

namespace Lyking.SlidingPuzzle.Core
{
    /// <summary>
    /// 数字华容道规则常量：支持 3x3（编号 1-8）与 4x4（编号 1-15）两种棋盘，
    /// 打乱采用"从已解状态反向随机行走固定步数"，核心与表现层共同引用，保证数值一致。
    /// </summary>
    public static class PuzzleRules
    {
        /// <summary>支持的最小棋盘边长：3x3，编号 1-8。</summary>
        public const int MinSize = 3;

        /// <summary>支持的最大棋盘边长：4x4，编号 1-15。</summary>
        public const int MaxSize = 4;

        /// <summary>默认棋盘边长（4x4）。</summary>
        public const int DefaultSize = MaxSize;

        /// <summary>3x3 打乱步数：从已解状态反向随机行走的步数。</summary>
        public const int ShuffleSteps3x3 = 60;

        /// <summary>4x4 打乱步数：步数充足以保证布局充分混合。</summary>
        public const int ShuffleSteps4x4 = 120;

        /// <summary>指定边长的打乱步数。</summary>
        public static int ShuffleStepsFor(int size) => size <= MinSize ? ShuffleSteps3x3 : ShuffleSteps4x4;

        /// <summary>校验棋盘边长，超出支持范围即抛出异常。</summary>
        public static int ValidateSize(int size)
        {
            if (size < MinSize || size > MaxSize)
            {
                throw new ArgumentOutOfRangeException(nameof(size), size,
                    $"仅支持 {MinSize}x{MinSize} 至 {MaxSize}x{MaxSize} 的棋盘。");
            }

            return size;
        }
    }
}
