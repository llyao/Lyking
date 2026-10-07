using System;
using System.Collections.Generic;

namespace Lyking.BattleCity.Core
{
    /// <summary>地图格类型：空地、钢墙、砖墙与基地。</summary>
    public enum BattleCityCell
    {
        Empty,
        Steel,
        Brick,
        Base,
    }

    /// <summary>
    /// 坦克大战网格地图：由字符串行解析，行从上到下书写。
    /// '#' 钢墙、'B' 砖墙、'.' 空地、'E' 基地、'P' 玩家出生格；
    /// 砖墙可被炮火摧毁并在过关后整体重置，钢墙与基地不可摧毁。
    /// </summary>
    public sealed class BattleCityMap
    {
        private static readonly TankDir[] PlanDirs = { TankDir.Up, TankDir.Down, TankDir.Left, TankDir.Right };

        private readonly BattleCityCell[] _cells;
        private readonly BattleCityCell[] _initialCells;

        public BattleCityMap(IReadOnlyList<string> rows)
        {
            if (rows is null || rows.Count == 0) throw new ArgumentException("地图行不能为空。", nameof(rows));
            int width = rows[0].Length;
            if (width == 0) throw new ArgumentException("地图行不能为空。", nameof(rows));
            for (int i = 1; i < rows.Count; i++)
            {
                if (rows[i].Length != width) throw new ArgumentException($"第 {i + 1} 行长度与首行不一致。", nameof(rows));
            }

            Width = width;
            Height = rows.Count;
            _cells = new BattleCityCell[Width * Height];
            _initialCells = new BattleCityCell[Width * Height];
            int playerSpawns = 0;
            int bases = 0;
            for (int row = 0; row < Height; row++)
            {
                int y = Height - 1 - row;
                for (int x = 0; x < Width; x++)
                {
                    char symbol = rows[row][x];
                    var cell = symbol switch
                    {
                        '.' => BattleCityCell.Empty,
                        '#' => BattleCityCell.Steel,
                        'B' => BattleCityCell.Brick,
                        'E' => BattleCityCell.Base,
                        'P' => BattleCityCell.Empty,
                        _ => throw new ArgumentException($"地图含非法字符 '{symbol}'（第 {row + 1} 行第 {x + 1} 列）。", nameof(rows)),
                    };
                    if (symbol == 'P')
                    {
                        playerSpawns++;
                        PlayerSpawn = new GridPos(x, y);
                    }

                    if (symbol == 'E')
                    {
                        bases++;
                        BaseCell = new GridPos(x, y);
                    }

                    _initialCells[y * Width + x] = cell;
                }
            }

            if (playerSpawns != 1) throw new ArgumentException("地图必须恰好有一个玩家出生格 'P'。", nameof(rows));
            if (bases != 1) throw new ArgumentException("地图必须恰好有一个基地格 'E'。", nameof(rows));
            Array.Copy(_initialCells, _cells, _cells.Length);
        }

        public int Width { get; }

        public int Height { get; }

        /// <summary>玩家出生格（'P'），格子本身为空地。</summary>
        public GridPos PlayerSpawn { get; }

        /// <summary>基地格（'E'），被炮火击中即游戏结束。</summary>
        public GridPos BaseCell { get; }

        public bool IsInside(GridPos pos) => pos.X >= 0 && pos.X < Width && pos.Y >= 0 && pos.Y < Height;

        /// <summary>查询格类型；越界抛出异常。</summary>
        public BattleCityCell CellAt(GridPos pos)
        {
            if (!IsInside(pos)) throw new ArgumentOutOfRangeException(nameof(pos), pos, "格子坐标越界。");
            return _cells[pos.Y * Width + pos.X];
        }

        /// <summary>坦克不可通行：场外、钢墙、砖墙与基地。</summary>
        public bool IsBlockedForTank(GridPos pos) => !IsInside(pos) || _cells[pos.Y * Width + pos.X] != BattleCityCell.Empty;

        /// <summary>摧毁砖墙：仅砖墙可被摧毁，返回是否成功。</summary>
        public bool TryDestroyBrick(GridPos pos)
        {
            if (!IsInside(pos) || _cells[pos.Y * Width + pos.X] != BattleCityCell.Brick) return false;
            _cells[pos.Y * Width + pos.X] = BattleCityCell.Empty;
            return true;
        }

        /// <summary>重置地图：过关时恢复全部砖墙。</summary>
        public void Reset() => Array.Copy(_initialCells, _cells, _cells.Length);

        /// <summary>顶部三个敌人出生格：左上、顶中、右上。</summary>
        public GridPos EnemySpawnCell(int slot) => slot switch
        {
            0 => new GridPos(0, Height - 1),
            1 => new GridPos(Width / 2, Height - 1),
            2 => new GridPos(Width - 1, Height - 1),
            _ => throw new ArgumentOutOfRangeException(nameof(slot), slot, "出生槽位必须在 0..2 之间。"),
        };

        /// <summary>
        /// 地图合法性：玩家出生格能否规划到基地邻格。
        /// 砖墙可被炮火摧毁，规划可达性时视为可通行；钢墙与基地不可通过。
        /// </summary>
        public bool IsPlayerSpawnConnectedToBase()
        {
            var visited = new bool[Width * Height];
            var queue = new Queue<GridPos>();
            visited[PlayerSpawn.Y * Width + PlayerSpawn.X] = true;
            queue.Enqueue(PlayerSpawn);
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (IsBaseAdjacent(current)) return true;
                foreach (var dir in PlanDirs)
                {
                    var next = current + dir.ToDelta();
                    if (!IsInside(next)) continue;
                    int index = next.Y * Width + next.X;
                    if (visited[index] || !IsPlannable(next)) continue;
                    visited[index] = true;
                    queue.Enqueue(next);
                }
            }

            return false;
        }

        /// <summary>规划可通行：空地或砖墙（砖墙可被摧毁）。</summary>
        private bool IsPlannable(GridPos pos)
        {
            var cell = _cells[pos.Y * Width + pos.X];
            return cell == BattleCityCell.Empty || cell == BattleCityCell.Brick;
        }

        private bool IsBaseAdjacent(GridPos pos)
            => pos == BaseCell + new GridPos(1, 0) || pos == BaseCell + new GridPos(-1, 0)
            || pos == BaseCell + new GridPos(0, 1) || pos == BaseCell + new GridPos(0, -1);
    }
}
