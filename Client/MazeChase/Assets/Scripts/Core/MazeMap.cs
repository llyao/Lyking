using System;
using System.Collections.Generic;

namespace Lyking.MazeChase.Core
{
    /// <summary>
    /// 吃豆迷宫地图：把字符串行解析为只读网格，首行为最上行，向上为 Y 正方向。
    /// '#' 墙、'.' 豆子、'o' 能量豆、'P' 玩家出生点、'G' 幽灵巢、空格为无豆通道。
    /// 行宽不一致、出现非法字符、玩家出生点不是 1 个、没有幽灵巢或没有任何豆子时抛出异常。
    /// </summary>
    public sealed class MazeMap
    {
        public const char Wall = '#';
        public const char Pellet = '.';
        public const char PowerPellet = 'o';
        public const char PlayerSpawn = 'P';
        public const char GhostHouse = 'G';
        public const char Empty = ' ';

        private readonly HashSet<GridPos> _walls = new HashSet<GridPos>();
        private readonly HashSet<GridPos> _houseSet = new HashSet<GridPos>();
        private readonly List<GridPos> _ghostHouses = new List<GridPos>();
        private readonly List<GridPos> _pellets = new List<GridPos>();
        private readonly List<GridPos> _powerPellets = new List<GridPos>();
        private GridPos _playerStart;

        public MazeMap(IReadOnlyList<string> rows)
        {
            if (rows is null || rows.Count == 0) throw new ArgumentException("地图至少需要一行。", nameof(rows));

            int width = rows[0].Length;
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i] is null) throw new ArgumentException($"第 {i + 1} 行不能为 null。", nameof(rows));
                if (rows[i].Length != width)
                    throw new ArgumentException($"第 {i + 1} 行宽度 {rows[i].Length} 与首行宽度 {width} 不一致。", nameof(rows));
            }

            Width = width;
            Height = rows.Count;

            int playerCount = 0;
            for (int row = 0; row < rows.Count; row++)
            {
                // 首行是视觉上的最上行：网格坐标 Y 向上，故行号需要翻转。
                int y = Height - 1 - row;
                for (int x = 0; x < rows[row].Length; x++)
                {
                    var pos = new GridPos(x, y);
                    switch (rows[row][x])
                    {
                        case Wall:
                            _walls.Add(pos);
                            break;
                        case Pellet:
                            _pellets.Add(pos);
                            break;
                        case PowerPellet:
                            _powerPellets.Add(pos);
                            break;
                        case PlayerSpawn:
                            _playerStart = pos;
                            playerCount++;
                            break;
                        case GhostHouse:
                            _ghostHouses.Add(pos);
                            _houseSet.Add(pos);
                            break;
                        case Empty:
                            break;
                        default:
                            throw new ArgumentException($"第 {row + 1} 行第 {x + 1} 列字符 '{rows[row][x]}' 非法。", nameof(rows));
                    }
                }
            }

            if (playerCount != 1) throw new ArgumentException($"地图必须恰好有 1 个玩家出生点，实际 {playerCount} 个。", nameof(rows));
            if (_ghostHouses.Count == 0) throw new ArgumentException("地图至少需要 1 个幽灵巢。", nameof(rows));
            if (_pellets.Count + _powerPellets.Count == 0) throw new ArgumentException("地图至少需要 1 颗豆子。", nameof(rows));
        }

        public int Width { get; }

        public int Height { get; }

        /// <summary>玩家出生点。</summary>
        public GridPos PlayerStart => _playerStart;

        /// <summary>幽灵巢格列表，按解析顺序排列；幽灵按序号取模复用巢格。</summary>
        public IReadOnlyList<GridPos> GhostHouses => _ghostHouses;

        /// <summary>初始豆子格，按解析顺序排列。</summary>
        public IReadOnlyList<GridPos> InitialPellets => _pellets;

        /// <summary>初始能量豆格，按解析顺序排列。</summary>
        public IReadOnlyList<GridPos> InitialPowerPellets => _powerPellets;

        public IReadOnlyCollection<GridPos> Walls => _walls;

        /// <summary>初始豆子总数（含能量豆），即过关需要清空的数目。</summary>
        public int TotalPelletCount => _pellets.Count + _powerPellets.Count;

        /// <summary>是否为墙；网格之外的坐标一律视为墙，保证移动不会越界。</summary>
        public bool IsWall(GridPos pos) =>
            pos.X < 0 || pos.X >= Width || pos.Y < 0 || pos.Y >= Height || _walls.Contains(pos);

        /// <summary>是否可通行：非墙即为通道，玩家与幽灵共用同一通路。</summary>
        public bool IsWalkable(GridPos pos) => !IsWall(pos);

        /// <summary>是否为幽灵巢格。</summary>
        public bool IsGhostHouse(GridPos pos) => _houseSet.Contains(pos);
    }
}
