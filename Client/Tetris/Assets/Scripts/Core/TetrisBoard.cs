using System;
using System.Collections.Generic;

namespace Lyking.Tetris.Core
{
    /// <summary>
    /// 俄罗斯方块核心棋局：确定性回合制模拟，无 Unity 依赖、无时间概念。
    /// 井格保存 <see cref="TetrominoKind?"/>（null 为空格），方块形状取自 <see cref="TetrominoTable"/> 静态偏移表；
    /// 方块序列默认由注入种子的 <see cref="Random"/> 产生，也可注入固定序列供测试与回放。
    /// 重力 <see cref="Tick"/>、软降一格、硬降落底都会锁定并检查消行；新方块与堆叠重叠即游戏结束。
    /// </summary>
    public sealed class TetrisBoard
    {
        private const int MaxEventLogs = 128;

        /// <summary>旋转的简化踢墙顺序：原位、左 1、右 1、下 1。</summary>
        private static readonly GridPos[] RotateKicks =
        {
            new GridPos(0, 0), new GridPos(-1, 0), new GridPos(1, 0), new GridPos(0, -1),
        };

        private readonly TetrisRules _rules;
        private readonly Random _random;
        private readonly TetrominoKind?[] _grid;
        private readonly List<GridPos> _pieceCells = new List<GridPos>();
        private readonly List<TetrisEventLog> _events = new List<TetrisEventLog>();
        private readonly IReadOnlyList<TetrominoKind>? _sequence;
        private int _sequenceIndex;

        public TetrisBoard(TetrisRules? rules = null, int seed = 0, IReadOnlyList<TetrominoKind>? pieceSequence = null)
        {
            if (pieceSequence is { Count: 0 }) throw new ArgumentException("方块序列不能为空。", nameof(pieceSequence));

            _rules = rules ?? TetrisRules.Default;
            _random = new Random(seed);
            _sequence = pieceSequence;
            _grid = new TetrominoKind?[_rules.CellCount];

            NextKind = DrawKind();
            SpawnPiece();
        }

        public int Width => _rules.Width;

        public int Height => _rules.Height;

        public int Score { get; private set; }

        public int Level { get; private set; } = 1;

        public int LinesCleared { get; private set; }

        public TetrominoKind CurrentKind { get; private set; }

        /// <summary>预览队列中的下一个方块。</summary>
        public TetrominoKind NextKind { get; private set; }

        /// <summary>当前旋转态，0 为出场朝向，顺时针递增。</summary>
        public int Rotation { get; private set; }

        /// <summary>方块包围盒原点：盒内偏移与其相加得到井格坐标，贴底时可为负。</summary>
        public GridPos PieceOrigin { get; private set; }

        public bool IsGameOver { get; private set; }

        public bool IsFinished => IsGameOver;

        /// <summary>已完成的重力步数。</summary>
        public int TickIndex { get; private set; }

        /// <summary>当前重力步进间隔（秒）：随等级缩短，由驱动方按此节奏调用 <see cref="Tick"/>。</summary>
        public float StepSeconds => _rules.StepSecondsFor(Level);

        /// <summary>当前下落方块的 4 个井格坐标，供渲染与锁定。</summary>
        public IReadOnlyList<GridPos> PieceCells => _pieceCells;

        public IReadOnlyList<TetrisEventLog> Events => _events;

        /// <summary>读取井格内容：空格与井外都返回 null。</summary>
        public TetrominoKind? CellAt(int x, int y)
        {
            if (x < 0 || x >= Width || y < 0 || y >= Height) return null;
            return _grid[y * Width + x];
        }

        /// <summary>左移一格；撞井壁或堆叠时被拒绝，返回是否成功。</summary>
        public bool MoveLeft() => TryMove(new GridPos(-1, 0));

        /// <summary>右移一格；撞井壁或堆叠时被拒绝，返回是否成功。</summary>
        public bool MoveRight() => TryMove(new GridPos(1, 0));

        /// <summary>顺时针旋转 90 度；按原位、左 1、右 1、下 1 顺序尝试踢墙，全部失败则保持原朝向。</summary>
        public bool Rotate()
        {
            if (IsFinished) return false;
            int next = (Rotation + 1) % TetrominoTable.RotationCount;
            foreach (var kick in RotateKicks)
            {
                var target = PieceOrigin + kick;
                if (!Fits(CurrentKind, next, target)) continue;

                Rotation = next;
                PieceOrigin = target;
                RefreshPieceCells();
                return true;
            }

            return false;
        }

        /// <summary>软降一格；下不去时锁定当前方块并返回 false。</summary>
        public bool SoftDrop()
        {
            if (IsFinished) return false;
            if (TryMove(new GridPos(0, -1))) return true;

            LockPiece();
            return false;
        }

        /// <summary>硬降：直接落到最低可行位置并锁定，返回棋局是否继续。</summary>
        public bool HardDrop()
        {
            if (IsFinished) return false;
            while (TryMove(new GridPos(0, -1)))
            {
            }

            LockPiece();
            return !IsGameOver;
        }

        /// <summary>重力推进一格。返回 false 表示棋局已结束；触底时锁定并补入新方块。</summary>
        public bool Tick()
        {
            if (IsFinished) return false;
            TickIndex++;
            if (TryMove(new GridPos(0, -1))) return true;

            LockPiece();
            return !IsGameOver;
        }

        private bool TryMove(GridPos delta)
        {
            if (IsFinished) return false;
            var target = PieceOrigin + delta;
            if (!Fits(CurrentKind, Rotation, target)) return false;

            PieceOrigin = target;
            RefreshPieceCells();
            return true;
        }

        /// <summary>方块 4 个格子全部落在井内且不与堆叠重叠时返回 true。</summary>
        private bool Fits(TetrominoKind kind, int rotation, GridPos origin)
        {
            foreach (var offset in TetrominoTable.Cells(kind, rotation))
            {
                int x = origin.X + offset.X;
                int y = origin.Y + offset.Y;
                if (x < 0 || x >= Width || y < 0 || y >= Height) return false;
                if (_grid[y * Width + x] is not null) return false;
            }

            return true;
        }

        private void LockPiece()
        {
            foreach (var cell in _pieceCells)
            {
                _grid[cell.Y * Width + cell.X] = CurrentKind;
            }

            Log(TetrisEventType.Locked);

            int cleared = ClearFullRows();
            if (cleared > 0)
            {
                // 计分使用消行发生时的等级，随后再判断是否升级。
                Score += _rules.ScoreForLines(cleared, Level);
                LinesCleared += cleared;
                Log(TetrisEventType.LineCleared, cleared);
                int newLevel = LinesCleared / _rules.LinesPerLevel + 1;
                if (newLevel > Level)
                {
                    Level = newLevel;
                    Log(TetrisEventType.LevelUp);
                }
            }

            SpawnPiece();
        }

        /// <summary>自底向上摘除整行；上方行整体下移一格，同格重查以处理相邻多行。</summary>
        private int ClearFullRows()
        {
            int cleared = 0;
            int y = 0;
            while (y < Height)
            {
                if (!RowFull(y))
                {
                    y++;
                    continue;
                }

                cleared++;
                for (int row = y; row < Height - 1; row++)
                {
                    for (int x = 0; x < Width; x++)
                    {
                        _grid[row * Width + x] = _grid[(row + 1) * Width + x];
                    }
                }

                for (int x = 0; x < Width; x++)
                {
                    _grid[(Height - 1) * Width + x] = null;
                }
            }

            return cleared;
        }

        private bool RowFull(int y)
        {
            for (int x = 0; x < Width; x++)
            {
                if (_grid[y * Width + x] is null) return false;
            }

            return true;
        }

        private void SpawnPiece()
        {
            CurrentKind = NextKind;
            NextKind = DrawKind();
            Rotation = 0;
            PieceOrigin = new GridPos((Width - TetrominoTable.BoxWidth(CurrentKind)) / 2, Height - TetrominoTable.BoxHeight(CurrentKind));
            RefreshPieceCells();

            if (Fits(CurrentKind, Rotation, PieceOrigin))
            {
                Log(TetrisEventType.Spawned);
                return;
            }

            // 出场即重叠：游戏结束，保留重叠形状供展示。
            IsGameOver = true;
            Log(TetrisEventType.GameOver);
        }

        /// <summary>注入序列按顺序循环取用；未注入时由种子随机产生。</summary>
        private TetrominoKind DrawKind()
        {
            if (_sequence is { Count: > 0 })
            {
                var kind = _sequence[_sequenceIndex % _sequence.Count];
                _sequenceIndex++;
                return kind;
            }

            return (TetrominoKind)_random.Next(TetrominoTable.KindCount);
        }

        private void RefreshPieceCells()
        {
            _pieceCells.Clear();
            foreach (var offset in TetrominoTable.Cells(CurrentKind, Rotation))
            {
                _pieceCells.Add(PieceOrigin + offset);
            }
        }

        private void Log(TetrisEventType type, int lines = 0)
        {
            _events.Add(new TetrisEventLog(TickIndex, type, lines));
            if (_events.Count > MaxEventLogs) _events.RemoveRange(0, _events.Count - MaxEventLogs);
        }
    }
}
