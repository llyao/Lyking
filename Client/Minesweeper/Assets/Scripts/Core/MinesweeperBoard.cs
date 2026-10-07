#nullable enable
using System;
using System.Collections.Generic;

namespace Lyking.Minesweeper.Core
{
    /// <summary>
    /// 扫雷棋盘规则核心，不依赖 UnityEngine。
    /// 首次有效翻开时才布雷，排除首格及其邻域（格数不足时保证首格安全并尽量多排除邻居）；
    /// 翻开邻雷数为 0 的格子时洪水展开；翻开全部安全格判胜，翻开地雷判负。
    /// 对已结束对局的操作返回结果枚举且不改变状态；观察者异常不会中断状态推进。
    /// </summary>
    public sealed class MinesweeperBoard
    {
        private readonly BoardSpec _spec;
        private readonly IMinesweeperRandom _random;
        private readonly bool[] _mines;
        private readonly int[] _adjacentMines;
        private readonly CellState[] _states;
        private bool _minesPlaced;
        private int _flagCount;
        private int _revealedSafeCount;

        public MinesweeperBoard(BoardSpec spec)
            : this(spec, new SystemRandomWrapper())
        {
        }

        public MinesweeperBoard(BoardSpec spec, IMinesweeperRandom random)
        {
            _spec = spec ?? throw new ArgumentNullException(nameof(spec));
            _random = random ?? throw new ArgumentNullException(nameof(random));
            _mines = new bool[spec.CellCount];
            _adjacentMines = new int[spec.CellCount];
            _states = new CellState[spec.CellCount];
        }

        public event Action<MinesweeperEvent>? EventPublished;

        public BoardSpec Spec => _spec;

        public int Width => _spec.Width;

        public int Height => _spec.Height;

        public GameState State { get; private set; } = GameState.Ready;

        public bool AreMinesPlaced => _minesPlaced;

        public int FlagCount => _flagCount;

        /// <summary>剩余雷数 = 地雷总数 - 已插旗数，旗数超过雷数时为负。</summary>
        public int RemainingMineCount => _spec.MineCount - _flagCount;

        public int RevealedSafeCount => _revealedSafeCount;

        /// <summary>读取格子快照。坐标越界属于调用方编程错误，直接快速失败。</summary>
        public MinesweeperCell CellAt(int x, int y)
        {
            int index = GetIndexOrThrow(x, y);
            return new MinesweeperCell(_states[index], _mines[index], _adjacentMines[index]);
        }

        /// <summary>失败对局中，被旗住但下面没有雷的格子（渲染层用来画错误旗标记）。</summary>
        public bool IsWrongFlag(int x, int y)
        {
            int index = GetIndexOrThrow(x, y);
            return State == GameState.Lost && _states[index] == CellState.Flagged && !_mines[index];
        }

        public RevealOutcome Reveal(int x, int y)
        {
            if (IsOutOfBounds(x, y))
            {
                return RevealOutcome.InvalidCell;
            }

            if (State is GameState.Won or GameState.Lost)
            {
                return RevealOutcome.GameOver;
            }

            int index = IndexOf(x, y);
            if (_states[index] == CellState.Flagged)
            {
                return RevealOutcome.BlockedByFlag;
            }

            if (_states[index] == CellState.Revealed)
            {
                return RevealOutcome.AlreadyRevealed;
            }

            if (!_minesPlaced)
            {
                PlaceMines(x, y);
                State = GameState.Playing;
            }

            if (_mines[index])
            {
                State = GameState.Lost;
                RevealAllMines();
                Publish(new MinesweeperEvent(MinesweeperEventType.MineHit, x, y));
                Publish(new MinesweeperEvent(MinesweeperEventType.Lost, -1, -1, _spec.MineCount));
                return RevealOutcome.HitMine;
            }

            int opened = FloodReveal(x, y);
            Publish(new MinesweeperEvent(MinesweeperEventType.Revealed, x, y));
            if (opened > 1)
            {
                Publish(new MinesweeperEvent(MinesweeperEventType.FloodRevealed, x, y, opened));
            }

            CheckWin();
            return RevealOutcome.Revealed;
        }

        public FlagOutcome ToggleFlag(int x, int y)
        {
            if (IsOutOfBounds(x, y))
            {
                return FlagOutcome.InvalidCell;
            }

            if (State is GameState.Won or GameState.Lost)
            {
                return FlagOutcome.GameOver;
            }

            int index = IndexOf(x, y);
            if (_states[index] == CellState.Hidden)
            {
                _states[index] = CellState.Flagged;
                _flagCount++;
                Publish(new MinesweeperEvent(MinesweeperEventType.Flagged, x, y));
                return FlagOutcome.Flagged;
            }

            if (_states[index] == CellState.Flagged)
            {
                _states[index] = CellState.Hidden;
                _flagCount--;
                Publish(new MinesweeperEvent(MinesweeperEventType.Unflagged, x, y));
                return FlagOutcome.Unflagged;
            }

            return FlagOutcome.Rejected;
        }

        private void PlaceMines(int safeX, int safeY)
        {
            var excluded = new HashSet<int> { IndexOf(safeX, safeY) };
            foreach ((int neighborX, int neighborY) in Neighbors(safeX, safeY))
            {
                excluded.Add(IndexOf(neighborX, neighborY));
            }

            // 规格只保证留一格安全；排除集过大时逐个放回邻居，首格始终排除。
            while (_spec.CellCount - excluded.Count < _spec.MineCount && excluded.Count > 1)
            {
                excluded.Remove(GetAnyNeighbor(safeX, safeY, excluded));
            }

            var pool = new List<int>(_spec.CellCount - excluded.Count);
            for (int i = 0; i < _spec.CellCount; i++)
            {
                if (!excluded.Contains(i))
                {
                    pool.Add(i);
                }
            }

            // 部分洗牌：前 MineCount 个位置即中雷格子。
            int remaining = pool.Count;
            for (int i = 0; i < _spec.MineCount; i++, remaining--)
            {
                int pick = i + _random.Next(0, remaining);
                (pool[i], pool[pick]) = (pool[pick], pool[i]);
                _mines[pool[i]] = true;
            }

            for (int y = 0; y < _spec.Height; y++)
            {
                for (int x = 0; x < _spec.Width; x++)
                {
                    int index = IndexOf(x, y);
                    if (_mines[index])
                    {
                        continue;
                    }

                    int count = 0;
                    foreach ((int neighborX, int neighborY) in Neighbors(x, y))
                    {
                        if (_mines[IndexOf(neighborX, neighborY)])
                        {
                            count++;
                        }
                    }

                    _adjacentMines[index] = count;
                }
            }

            _minesPlaced = true;
        }

        private int GetAnyNeighbor(int x, int y, HashSet<int> excluded)
        {
            foreach ((int neighborX, int neighborY) in Neighbors(x, y))
            {
                int index = IndexOf(neighborX, neighborY);
                if (excluded.Remove(index))
                {
                    return index;
                }
            }

            throw new InvalidOperationException("邻域排除集收缩时未找到可放回的邻居。");
        }

        private int FloodReveal(int startX, int startY)
        {
            var queue = new Queue<(int X, int Y)>();
            queue.Enqueue((startX, startY));
            int opened = 0;
            while (queue.Count > 0)
            {
                (int x, int y) = queue.Dequeue();
                int index = IndexOf(x, y);
                if (_states[index] != CellState.Hidden)
                {
                    continue;
                }

                _states[index] = CellState.Revealed;
                opened++;
                if (_adjacentMines[index] != 0)
                {
                    continue;
                }

                foreach ((int neighborX, int neighborY) in Neighbors(x, y))
                {
                    if (_states[IndexOf(neighborX, neighborY)] == CellState.Hidden)
                    {
                        queue.Enqueue((neighborX, neighborY));
                    }
                }
            }

            _revealedSafeCount += opened;
            return opened;
        }

        private void RevealAllMines()
        {
            for (int i = 0; i < _mines.Length; i++)
            {
                if (_mines[i])
                {
                    _states[i] = CellState.Revealed;
                }
            }
        }

        private void CheckWin()
        {
            if (State == GameState.Playing && _revealedSafeCount >= _spec.SafeCellCount)
            {
                State = GameState.Won;
                Publish(new MinesweeperEvent(MinesweeperEventType.Won, -1, -1, _spec.MineCount));
            }
        }

        private IEnumerable<(int X, int Y)> Neighbors(int x, int y)
        {
            for (int offsetY = -1; offsetY <= 1; offsetY++)
            {
                for (int offsetX = -1; offsetX <= 1; offsetX++)
                {
                    if (offsetX == 0 && offsetY == 0)
                    {
                        continue;
                    }

                    int neighborX = x + offsetX;
                    int neighborY = y + offsetY;
                    if (!IsOutOfBounds(neighborX, neighborY))
                    {
                        yield return (neighborX, neighborY);
                    }
                }
            }
        }

        private bool IsOutOfBounds(int x, int y)
        {
            return x < 0 || y < 0 || x >= _spec.Width || y >= _spec.Height;
        }

        private int IndexOf(int x, int y)
        {
            return y * _spec.Width + x;
        }

        private int GetIndexOrThrow(int x, int y)
        {
            if (IsOutOfBounds(x, y))
            {
                throw new ArgumentOutOfRangeException(nameof(x), x, $"格子坐标 ({x},{y}) 越界，棋盘为 {_spec.Width}x{_spec.Height}。");
            }

            return IndexOf(x, y);
        }

        private void Publish(MinesweeperEvent minesweeperEvent)
        {
            Action<MinesweeperEvent>? handlers = EventPublished;
            if (handlers is null)
            {
                return;
            }

            foreach (Action<MinesweeperEvent> handler in handlers.GetInvocationList())
            {
                try
                {
                    handler(minesweeperEvent);
                }
                catch
                {
                    // 单个观察者异常不得中断棋盘状态推进，也不向其他观察者传播。
                }
            }
        }
    }
}
