using System;

namespace Lyking.Bomberman.Core
{
    /// <summary>
    /// 炸弹人规则表：网格尺寸、地图布局参数、炸弹与火焰计时、敌人数量、生命与计分。
    /// <see cref="Default"/> 收录标准炸弹人的全部常量（13x11、引信 2.5 秒、火焰十字 2 格
    /// 燃烧 0.5 秒、同时最多 1 颗炸弹、敌人 3 名、生命 3 条、击杀 +100、摧墙 +10）；
    /// 测试可传入自定义参数验证多炸弹连锁、零敌人速通等扩展场景。
    /// </summary>
    public sealed class BombermanRules
    {
        /// <summary>默认规则表：标准炸弹人常量。</summary>
        public static readonly BombermanRules Default = new BombermanRules();

        public BombermanRules(
            int width = 13,
            int height = 11,
            int playerSpawnX = 1,
            int playerSpawnY = 1,
            float bombFuseSeconds = 2.5f,
            int flameLength = 2,
            float flameSeconds = 0.5f,
            int maxBombs = 1,
            int enemyCount = 3,
            int maxLives = 3,
            int scorePerKill = 100,
            int scorePerWall = 10,
            double softWallDensity = 0.6,
            double softWallDensityStep = 0.01,
            double maxSoftWallDensity = 0.8,
            float enemySpeed = 1.5f)
        {
            if (width < 5) throw new ArgumentOutOfRangeException(nameof(width), width, "网格宽度至少为 5。");
            if (height < 5) throw new ArgumentOutOfRangeException(nameof(height), height, "网格高度至少为 5。");
            if (playerSpawnX < 1 || playerSpawnX > width - 2)
                throw new ArgumentOutOfRangeException(nameof(playerSpawnX), playerSpawnX, "出生点必须留在边框之内。");
            if (playerSpawnY < 1 || playerSpawnY > height - 2)
                throw new ArgumentOutOfRangeException(nameof(playerSpawnY), playerSpawnY, "出生点必须留在边框之内。");
            if (IsPillarCell(playerSpawnX, playerSpawnY))
                throw new ArgumentOutOfRangeException(nameof(playerSpawnY), playerSpawnY, "出生点不能落在柱状硬墙上。");
            if (bombFuseSeconds <= 0f) throw new ArgumentOutOfRangeException(nameof(bombFuseSeconds), bombFuseSeconds, "炸弹引信必须为正。");
            if (flameLength < 1) throw new ArgumentOutOfRangeException(nameof(flameLength), flameLength, "火焰长度至少为 1 格。");
            if (flameSeconds <= 0f) throw new ArgumentOutOfRangeException(nameof(flameSeconds), flameSeconds, "火焰燃烧时间必须为正。");
            if (maxBombs < 1) throw new ArgumentOutOfRangeException(nameof(maxBombs), maxBombs, "同时放置的炸弹数量至少为 1。");
            if (enemyCount < 0) throw new ArgumentOutOfRangeException(nameof(enemyCount), enemyCount, "敌人数量不能为负。");
            if (maxLives < 1) throw new ArgumentOutOfRangeException(nameof(maxLives), maxLives, "最大生命至少为 1。");
            if (scorePerKill <= 0) throw new ArgumentOutOfRangeException(nameof(scorePerKill), scorePerKill, "击杀得分必须为正。");
            if (scorePerWall <= 0) throw new ArgumentOutOfRangeException(nameof(scorePerWall), scorePerWall, "摧墙得分必须为正。");
            if (softWallDensity <= 0d || softWallDensity >= 1d)
                throw new ArgumentOutOfRangeException(nameof(softWallDensity), softWallDensity, "软墙密度必须在 0 与 1 之间。");
            if (softWallDensityStep < 0d) throw new ArgumentOutOfRangeException(nameof(softWallDensityStep), softWallDensityStep, "每关密度增量不能为负。");
            if (maxSoftWallDensity <= 0d || maxSoftWallDensity > 1d)
                throw new ArgumentOutOfRangeException(nameof(maxSoftWallDensity), maxSoftWallDensity, "软墙密度上限必须在 0 与 1 之间。");
            if (enemySpeed <= 0f) throw new ArgumentOutOfRangeException(nameof(enemySpeed), enemySpeed, "敌人速度必须为正。");

            Width = width;
            Height = height;
            PlayerSpawnX = playerSpawnX;
            PlayerSpawnY = playerSpawnY;
            BombFuseSeconds = bombFuseSeconds;
            FlameLength = flameLength;
            FlameSeconds = flameSeconds;
            MaxBombs = maxBombs;
            EnemyCount = enemyCount;
            MaxLives = maxLives;
            ScorePerKill = scorePerKill;
            ScorePerWall = scorePerWall;
            SoftWallDensity = softWallDensity;
            SoftWallDensityStep = softWallDensityStep;
            MaxSoftWallDensity = maxSoftWallDensity;
            EnemySpeed = enemySpeed;
        }

        public int Width { get; }

        public int Height { get; }

        public int PlayerSpawnX { get; }

        public int PlayerSpawnY { get; }

        /// <summary>炸弹引信时长（秒）：放置后经过该时间即爆炸。</summary>
        public float BombFuseSeconds { get; }

        /// <summary>火焰十字沿每个方向延伸的格数，硬墙与软墙会挡住该方向。</summary>
        public int FlameLength { get; }

        /// <summary>火焰燃烧时长（秒）：燃烧期间仅作表现，伤害在爆炸瞬间结算。</summary>
        public float FlameSeconds { get; }

        /// <summary>同时可存在的炸弹数量上限。</summary>
        public int MaxBombs { get; }

        /// <summary>第一关的敌人数量，之后每关加一。</summary>
        public int EnemyCount { get; }

        public int MaxLives { get; }

        /// <summary>火焰炸死一名敌人的得分。</summary>
        public int ScorePerKill { get; }

        /// <summary>火焰摧毁一面软墙的得分。</summary>
        public int ScorePerWall { get; }

        /// <summary>第一关的软墙填充密度（占候选空格的比例）。</summary>
        public double SoftWallDensity { get; }

        /// <summary>每关软墙密度的增量。</summary>
        public double SoftWallDensityStep { get; }

        /// <summary>软墙密度的封顶值。</summary>
        public double MaxSoftWallDensity { get; }

        /// <summary>敌人游走速度（格/秒）。</summary>
        public float EnemySpeed { get; }

        /// <summary>玩家出生格。</summary>
        public GridPos PlayerSpawn => new GridPos(PlayerSpawnX, PlayerSpawnY);

        /// <summary>柱状硬墙格：横纵坐标均为偶数的格子（边框另行判定）。</summary>
        public bool IsPillarCell(int x, int y) => x % 2 == 0 && y % 2 == 0;

        /// <summary>第 <paramref name="level"/> 关的软墙填充密度：逐关递增并封顶，确定性纯函数。</summary>
        public double SoftWallDensityFor(int level)
        {
            if (level < 1) throw new ArgumentOutOfRangeException(nameof(level), level, "关卡从 1 开始。");
            return Math.Min(MaxSoftWallDensity, SoftWallDensity + SoftWallDensityStep * (level - 1));
        }

        /// <summary>第 <paramref name="level"/> 关的敌人数量：逐关加一，确定性纯函数。</summary>
        public int EnemyCountFor(int level)
        {
            if (level < 1) throw new ArgumentOutOfRangeException(nameof(level), level, "关卡从 1 开始。");
            return EnemyCount + level - 1;
        }
    }
}
