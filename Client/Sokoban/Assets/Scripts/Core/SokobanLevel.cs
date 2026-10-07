using System;
using System.Collections.Generic;

namespace Lyking.Sokoban.Core
{
    /// <summary>
    /// 推箱子关卡：把字符串行解析为只读网格，首行为最上行，向上为 Y 正方向。
    /// '#' 墙、'.' 目标、'$' 箱子、'*' 箱子在目标上、'@' 玩家、'+' 玩家在目标上、空格地板。
    /// 行宽不一致、出现非法字符、玩家数不是 1、箱子数为零或箱子数与目标数不相等时抛出异常。
    /// </summary>
    public sealed class SokobanLevel
    {
        public const char Wall = '#';
        public const char Goal = '.';
        public const char Box = '$';
        public const char BoxOnGoal = '*';
        public const char Player = '@';
        public const char PlayerOnGoal = '+';
        public const char Floor = ' ';

        private readonly HashSet<GridPos> _walls = new HashSet<GridPos>();
        private readonly HashSet<GridPos> _goals = new HashSet<GridPos>();
        private readonly List<GridPos> _boxes = new List<GridPos>();
        private GridPos _player;

        public SokobanLevel(string name, IReadOnlyList<string> rows)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("关卡名称不能为空。", nameof(name));
            if (rows is null || rows.Count == 0) throw new ArgumentException("关卡至少需要一行。", nameof(rows));

            int width = rows[0].Length;
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i] is null) throw new ArgumentException($"第 {i + 1} 行不能为 null。", nameof(rows));
                if (rows[i].Length != width)
                    throw new ArgumentException($"第 {i + 1} 行宽度 {rows[i].Length} 与首行宽度 {width} 不一致。", nameof(rows));
            }

            Name = name;
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
                        case Goal:
                            _goals.Add(pos);
                            break;
                        case Box:
                            _boxes.Add(pos);
                            break;
                        case BoxOnGoal:
                            _boxes.Add(pos);
                            _goals.Add(pos);
                            break;
                        case Player:
                            _player = pos;
                            playerCount++;
                            break;
                        case PlayerOnGoal:
                            _player = pos;
                            playerCount++;
                            _goals.Add(pos);
                            break;
                        case Floor:
                            break;
                        default:
                            throw new ArgumentException($"第 {row + 1} 行第 {x + 1} 列字符 '{rows[row][x]}' 非法。", nameof(rows));
                    }
                }
            }

            if (playerCount != 1) throw new ArgumentException($"关卡必须恰好有 1 个玩家，实际 {playerCount} 个。", nameof(rows));
            if (_boxes.Count == 0) throw new ArgumentException("关卡至少需要 1 个箱子。", nameof(rows));
            if (_boxes.Count != _goals.Count)
                throw new ArgumentException($"箱子数 {_boxes.Count} 与目标数 {_goals.Count} 不相等。", nameof(rows));
        }

        public string Name { get; }

        public int Width { get; }

        public int Height { get; }

        /// <summary>玩家初始位置。</summary>
        public GridPos PlayerStart => _player;

        /// <summary>箱子初始位置，按解析顺序排列。</summary>
        public IReadOnlyList<GridPos> InitialBoxes => _boxes;

        public IReadOnlyCollection<GridPos> Walls => _walls;

        public IReadOnlyCollection<GridPos> Goals => _goals;

        /// <summary>是否为墙；网格之外的坐标一律视为墙，保证移动不会越界。</summary>
        public bool IsWall(GridPos pos) =>
            pos.X < 0 || pos.X >= Width || pos.Y < 0 || pos.Y >= Height || _walls.Contains(pos);

        /// <summary>是否为目标点（含 '*' 与 '+' 隐含的目标）。</summary>
        public bool IsGoal(GridPos pos) => _goals.Contains(pos);
    }
}
