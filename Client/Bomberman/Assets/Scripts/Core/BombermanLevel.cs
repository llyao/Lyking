using System;
using System.Collections.Generic;

namespace Lyking.Bomberman.Core
{
    /// <summary>地形类型：地板、硬墙（不可摧毁）、软墙（可被火焰摧毁）。</summary>
    public enum BombermanTile
    {
        Floor,
        HardWall,
        SoftWall,
    }

    /// <summary>敌人出生信息：出生格与初始朝向。</summary>
    public readonly struct BombermanEnemySpawn
    {
        public BombermanEnemySpawn(GridPos position, BombermanDir direction)
        {
            Position = position;
            Direction = direction;
        }

        public GridPos Position { get; }

        public BombermanDir Direction { get; }
    }

    /// <summary>
    /// 炸弹人关卡：由 (关卡序号, 种子, 规则) 确定性生成。边框与内部柱状硬墙固定；
    /// 软墙按当前关密度从候选空格中不放回随机抽取，玩家出生点及其右、上邻格保持空置；
    /// 出口藏在其一软墙之下；敌人出生在剩余地板格上。
    /// 同一参数的生成结果完全一致，满足“软墙分布固定种子可手算”。
    /// </summary>
    public sealed class BombermanLevel
    {
        private readonly BombermanTile[,] _tiles;

        private BombermanLevel(
            int level,
            BombermanTile[,] tiles,
            GridPos exit,
            GridPos playerSpawn,
            List<GridPos> softWalls,
            List<BombermanEnemySpawn> enemySpawns)
        {
            Level = level;
            _tiles = tiles;
            Exit = exit;
            PlayerSpawn = playerSpawn;
            SoftWalls = softWalls;
            EnemySpawns = enemySpawns;
        }

        /// <summary>关卡序号（从 1 开始）。</summary>
        public int Level { get; }

        public int Width => _tiles.GetLength(1);

        public int Height => _tiles.GetLength(0);

        /// <summary>出口所在格；开局藏在软墙之下，软墙被摧毁后显露。</summary>
        public GridPos Exit { get; }

        /// <summary>玩家出生格。</summary>
        public GridPos PlayerSpawn { get; }

        /// <summary>软墙位置，按抽取顺序排列。</summary>
        public IReadOnlyList<GridPos> SoftWalls { get; }

        /// <summary>敌人出生信息，按抽取顺序排列。</summary>
        public IReadOnlyList<BombermanEnemySpawn> EnemySpawns { get; }

        /// <summary>是否为边框硬墙格。</summary>
        public bool IsBorder(int x, int y) => x == 0 || x == Width - 1 || y == 0 || y == Height - 1;

        /// <summary>查询地形；网格之外的坐标一律视为硬墙。</summary>
        public BombermanTile TileAt(GridPos pos)
        {
            if (pos.X < 0 || pos.X >= Width || pos.Y < 0 || pos.Y >= Height) return BombermanTile.HardWall;
            return _tiles[pos.Y, pos.X];
        }

        /// <summary>复制地形数组，供战局在其上做可变结算。</summary>
        public BombermanTile[,] CloneTiles() => (BombermanTile[,])_tiles.Clone();

        /// <summary>
        /// 以给定种子确定性生成第 <paramref name="level"/> 关。
        /// 抽样顺序固定：先按行序收集候选空格，再部分洗牌不放回抽取软墙，
        /// 随后抽出藏出口的软墙，最后从未被软墙占据的空格中不放回抽出敌人出生点。
        /// </summary>
        public static BombermanLevel Generate(int level, int seed, BombermanRules rules)
        {
            if (level < 1) throw new ArgumentOutOfRangeException(nameof(level), level, "关卡从 1 开始。");
            if (rules is null) throw new ArgumentNullException(nameof(rules));

            var random = new Random(LevelSeed(seed, level));
            var tiles = new BombermanTile[rules.Height, rules.Width];
            for (int y = 0; y < rules.Height; y++)
            {
                for (int x = 0; x < rules.Width; x++)
                {
                    bool border = x == 0 || x == rules.Width - 1 || y == 0 || y == rules.Height - 1;
                    tiles[y, x] = border || rules.IsPillarCell(x, y) ? BombermanTile.HardWall : BombermanTile.Floor;
                }
            }

            var spawn = rules.PlayerSpawn;
            var spawnRight = spawn + new GridPos(1, 0);
            var spawnUp = spawn + new GridPos(0, 1);

            // 候选空格按行序收集，排除出生点及其右、上邻格，保证开局不被软墙困住。
            var free = new List<GridPos>();
            for (int y = 0; y < rules.Height; y++)
            {
                for (int x = 0; x < rules.Width; x++)
                {
                    var pos = new GridPos(x, y);
                    if (tiles[y, x] == BombermanTile.Floor && !pos.Equals(spawn) && !pos.Equals(spawnRight) && !pos.Equals(spawnUp))
                        free.Add(pos);
                }
            }

            int enemyCount = rules.EnemyCountFor(level);
            if (free.Count <= enemyCount)
                throw new ArgumentException($"候选空格 {free.Count} 个不足以填充软墙并放置 {enemyCount} 名敌人。", nameof(rules));

            // 软墙数量按密度取整，并保证剩余地板格足够放置全部敌人。
            int softCount = Math.Min((int)(free.Count * rules.SoftWallDensityFor(level)), free.Count - enemyCount);
            if (softCount <= 0)
                throw new ArgumentException("候选空格不足，无法藏匿出口软墙。", nameof(rules));

            var softWalls = new List<GridPos>(softCount);
            for (int i = 0; i < softCount; i++)
            {
                // 部分洗牌：把第 i 个候选与随机后方位置交换，等价于不放回抽样。
                int j = random.Next(i, free.Count);
                (free[i], free[j]) = (free[j], free[i]);
                var pos = free[i];
                tiles[pos.Y, pos.X] = BombermanTile.SoftWall;
                softWalls.Add(pos);
            }

            var exit = softWalls[random.Next(softWalls.Count)];

            // 敌人从未被软墙占据的剩余空格中不放回抽取，初始朝向从当前可走方向随机挑选。
            var floorCandidates = free.GetRange(softCount, free.Count - softCount);
            var enemySpawns = new List<BombermanEnemySpawn>(enemyCount);
            for (int i = 0; i < enemyCount; i++)
            {
                int j = random.Next(i, floorCandidates.Count);
                (floorCandidates[i], floorCandidates[j]) = (floorCandidates[j], floorCandidates[i]);
                var pos = floorCandidates[i];
                enemySpawns.Add(new BombermanEnemySpawn(pos, PickSpawnDirection(tiles, rules, pos, random)));
            }

            return new BombermanLevel(level, tiles, exit, spawn, softWalls, enemySpawns);
        }

        /// <summary>
        /// 关卡种子：同一局内每关由初始种子确定性派生（seed 换代）。
        /// 使用固定整数算式而非 <see cref="HashCode"/>，保证跨进程结果一致。
        /// </summary>
        public static int LevelSeed(int seed, int level) => unchecked(seed * 1013904223 + level * 1664525);

        private static BombermanDir PickSpawnDirection(BombermanTile[,] tiles, BombermanRules rules, GridPos pos, Random random)
        {
            var passable = new List<BombermanDir>(4);
            foreach (var dir in new[] { BombermanDir.Up, BombermanDir.Down, BombermanDir.Left, BombermanDir.Right })
            {
                var next = pos + dir.ToDelta();
                if (next.X >= 0 && next.X < rules.Width && next.Y >= 0 && next.Y < rules.Height
                    && tiles[next.Y, next.X] == BombermanTile.Floor)
                {
                    passable.Add(dir);
                }
            }

            return passable.Count == 0 ? BombermanDir.None : passable[random.Next(passable.Count)];
        }
    }
}
