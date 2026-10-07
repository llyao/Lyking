#nullable enable
using System;

namespace Lyking.Minesweeper.Core
{
    /// <summary>
    /// 随机源抽象。生产环境用随机种子，测试用固定种子获得确定性雷区。
    /// </summary>
    public interface IMinesweeperRandom
    {
        int Next(int minInclusive, int maxExclusive);
    }

    /// <summary>基于 <see cref="Random"/> 的默认随机源。</summary>
    public sealed class SystemRandomWrapper : IMinesweeperRandom
    {
        private readonly Random _random;

        public SystemRandomWrapper()
            : this(new Random())
        {
        }

        public SystemRandomWrapper(int seed)
            : this(new Random(seed))
        {
        }

        public SystemRandomWrapper(Random random)
        {
            _random = random ?? throw new ArgumentNullException(nameof(random));
        }

        public int Next(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive)
            {
                throw new ArgumentOutOfRangeException(nameof(maxExclusive), maxExclusive, "上界必须大于下界。");
            }

            return _random.Next(minInclusive, maxExclusive);
        }
    }
}
