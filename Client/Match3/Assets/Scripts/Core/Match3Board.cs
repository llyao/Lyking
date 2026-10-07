using System;
using System.Collections.Generic;

namespace Lyking.Match3.Core
{
    /// <summary>
    /// 三消核心棋局：8x8 网格 6 种宝石，无 Unity 依赖、无内建时钟，交换结算离散完成。
    /// 构造时以注入种子的 <see cref="Random"/> 逐格生成并回填避免现成三连，
    /// 也可注入预置棋盘做确定性测试。交换后自动扫描消除、重力下落、顶部填充，
    /// 连锁自动续算（第 n 波得分 ×n）直至稳定；步数用尽即结束。
    /// </summary>
    public sealed class Match3Board
    {
        private const int MaxEventLogs = 128;
        private const int ShuffleRetryLimit = 200;

        /// <summary>被消除格位的临时标记值，仅存在于结算过程内部。</summary>
        private const int EmptyCell = -1;

        private readonly Random _random;
        private readonly int[,] _gems = new int[Match3Rules.Rows, Match3Rules.Columns];
        private readonly List<Match3EventLog> _events = new List<Match3EventLog>();

        /// <summary>以注入种子生成初始棋盘：逐格取随机种类，回填避免现成三连。</summary>
        public Match3Board(int seed = 0)
        {
            _random = new Random(seed);
            for (int row = 0; row < Match3Rules.Rows; row++)
            {
                for (int column = 0; column < Match3Rules.Columns; column++)
                {
                    // 回填：与左侧两格或上方两格同种类则重取，保证初始棋盘无现成三连。
                    int kind = _random.Next(Match3Rules.GemKindCount);
                    while (WouldExtendRun(row, column, kind)) kind = _random.Next(Match3Rules.GemKindCount);
                    _gems[row, column] = kind;
                }
            }

            Log(new Match3EventLog(Moves, Match3EventType.Dealt));
        }

        /// <summary>注入预置棋盘（拷贝保存），供测试确定性构造消除、下落与连锁局面。</summary>
        public Match3Board(int[,] initialGems, int seed = 0)
        {
            if (initialGems is null) throw new ArgumentNullException(nameof(initialGems));
            if (initialGems.GetLength(0) != Match3Rules.Rows || initialGems.GetLength(1) != Match3Rules.Columns)
            {
                throw new ArgumentException($"初始棋盘必须为 {Match3Rules.Rows}x{Match3Rules.Columns}。", nameof(initialGems));
            }

            foreach (int kind in initialGems)
            {
                if (kind < 0 || kind >= Match3Rules.GemKindCount)
                {
                    throw new ArgumentException("初始棋盘含越界的宝石种类。", nameof(initialGems));
                }
            }

            _random = new Random(seed);
            Array.Copy(initialGems, _gems, _gems.Length);
            Log(new Match3EventLog(Moves, Match3EventType.Dealt));
        }

        public int CellCount => Match3Rules.CellCount;

        /// <summary>累计分数：每波消除按计分表取和后乘连锁倍率。</summary>
        public int Score { get; private set; }

        /// <summary>已用步数：每次有效交换记 1 步，无效交换不计。</summary>
        public int Moves { get; private set; }

        /// <summary>剩余步数，归零即对局结束。</summary>
        public int MovesLeft => Match3Rules.TotalMoves - Moves;

        public bool IsFinished => MovesLeft <= 0;

        /// <summary>最近一波结算达到的连锁波次，未发生过消除时为 0。</summary>
        public int ChainLevel { get; private set; }

        /// <summary>当前棋盘快照（副本），外部修改不影响核心状态。</summary>
        public int[,] Gems => (int[,])_gems.Clone();

        public IReadOnlyList<Match3EventLog> Events => _events;

        /// <summary>读取一格宝石种类，越界抛异常。</summary>
        public int GetGem(int row, int column)
        {
            if (row < 0 || row >= Match3Rules.Rows) throw new ArgumentOutOfRangeException(nameof(row), row, "行越界。");
            if (column < 0 || column >= Match3Rules.Columns) throw new ArgumentOutOfRangeException(nameof(column), column, "列越界。");
            return _gems[row, column];
        }

        /// <summary>
        /// 交换两个相邻格。交换后无消除则自动换回并拒绝（记无效交换事件、不计步），
        /// 有消除则计一步并连续结算至稳定，返回是否发生有效交换。
        /// </summary>
        public bool Swap(int indexA, int indexB)
        {
            ValidateIndex(indexA);
            ValidateIndex(indexB);
            if (IsFinished || !AreAdjacent(indexA, indexB)) return false;

            SwapCells(indexA, indexB);
            if (FindRuns().Count == 0)
            {
                // 无效交换：换回原状并记录，不消耗步数。
                SwapCells(indexA, indexB);
                Log(new Match3EventLog(Moves, Match3EventType.SwapRejected, indexA, indexB));
                return false;
            }

            Moves++;
            Log(new Match3EventLog(Moves, Match3EventType.Swapped, indexA, indexB));
            Resolve();
            return true;
        }

        /// <summary>
        /// 重排现有宝石（Fisher-Yates，注入种子确定）：要求重排后无现成三连且有可行交换，
        /// 超过重试上限仍未满足则返回 false，用于死局自救。
        /// </summary>
        public bool Shuffle()
        {
            if (IsFinished) return false;

            var flat = new int[Match3Rules.CellCount];
            for (int index = 0; index < flat.Length; index++)
            {
                flat[index] = _gems[index / Match3Rules.Columns, index % Match3Rules.Columns];
            }

            for (int attempt = 0; attempt < ShuffleRetryLimit; attempt++)
            {
                // Fisher-Yates 洗牌：从尾向前，每个位置与剩余前缀内随机一格交换，保证确定性。
                for (int i = flat.Length - 1; i > 0; i--)
                {
                    int j = _random.Next(i + 1);
                    (flat[i], flat[j]) = (flat[j], flat[i]);
                }

                for (int index = 0; index < flat.Length; index++)
                {
                    _gems[index / Match3Rules.Columns, index % Match3Rules.Columns] = flat[index];
                }

                if (FindRuns().Count == 0 && HasValidMove)
                {
                    Log(new Match3EventLog(Moves, Match3EventType.Shuffled));
                    return true;
                }
            }

            return false;
        }

        /// <summary>穷举全部相邻交换试扫，判断是否存在能形成消除的交换。</summary>
        public bool HasValidMove
        {
            get
            {
                for (int row = 0; row < Match3Rules.Rows; row++)
                {
                    for (int column = 0; column < Match3Rules.Columns; column++)
                    {
                        int index = row * Match3Rules.Columns + column;
                        if (column + 1 < Match3Rules.Columns && SwapCreatesRun(index, index + 1)) return true;
                        if (row + 1 < Match3Rules.Rows && SwapCreatesRun(index, index + Match3Rules.Columns)) return true;
                    }
                }

                return false;
            }
        }

        /// <summary>扫描全部横向与纵向极大 ≥3 连续段，每组返回行优先格位列表；横纵交叠各自成组。</summary>
        private List<List<int>> FindRuns()
        {
            var runs = new List<List<int>>();
            for (int row = 0; row < Match3Rules.Rows; row++)
            {
                int column = 0;
                while (column < Match3Rules.Columns)
                {
                    int end = column + 1;
                    while (end < Match3Rules.Columns && _gems[row, end] == _gems[row, column]) end++;
                    if (end - column >= 3)
                    {
                        var run = new List<int>(end - column);
                        for (int cell = column; cell < end; cell++) run.Add(row * Match3Rules.Columns + cell);
                        runs.Add(run);
                    }

                    column = end;
                }
            }

            for (int column = 0; column < Match3Rules.Columns; column++)
            {
                int row = 0;
                while (row < Match3Rules.Rows)
                {
                    int end = row + 1;
                    while (end < Match3Rules.Rows && _gems[end, column] == _gems[row, column]) end++;
                    if (end - row >= 3)
                    {
                        var run = new List<int>(end - row);
                        for (int cell = row; cell < end; cell++) run.Add(cell * Match3Rules.Columns + column);
                        runs.Add(run);
                    }

                    row = end;
                }
            }

            return runs;
        }

        /// <summary>结算循环：按组计分并乘连锁倍率、移除、重力下落、顶部填充，直至无三连。</summary>
        private void Resolve()
        {
            int chain = 0;
            List<List<int>> runs = FindRuns();
            while (runs.Count > 0)
            {
                chain++;
                var cleared = new bool[Match3Rules.CellCount];
                int waveScore = 0;
                foreach (var run in runs)
                {
                    // 计分表按组取值；横纵交叠的 L/T 形各自成组，得分取和后再乘连锁倍率。
                    waveScore += Match3Rules.ScoreForGroup(run.Count);
                    foreach (int index in run) cleared[index] = true;
                }

                // 消除格数按去重后的标记统计，交叠格只计一次。
                int cellCount = 0;
                foreach (bool clear in cleared)
                {
                    if (clear) cellCount++;
                }

                waveScore *= chain;
                Score += waveScore;
                ChainLevel = chain;
                for (int index = 0; index < cleared.Length; index++)
                {
                    if (cleared[index]) _gems[index / Match3Rules.Columns, index % Match3Rules.Columns] = EmptyCell;
                }

                Log(new Match3EventLog(Moves, Match3EventType.Matched,
                    chainIndex: chain, groupCount: runs.Count, cellCount: cellCount, score: waveScore));
                Refill();
                Log(new Match3EventLog(Moves, Match3EventType.Refilled));
                runs = FindRuns();
            }

            if (IsFinished) Log(new Match3EventLog(Moves, Match3EventType.Finished));
        }

        /// <summary>重力下落：每列非空块压实到底部，顶部空位按注入种子随机填充（列序自左向右，每列自底向上）。</summary>
        private void Refill()
        {
            for (int column = 0; column < Match3Rules.Columns; column++)
            {
                int writeRow = Match3Rules.Rows - 1;
                for (int row = Match3Rules.Rows - 1; row >= 0; row--)
                {
                    if (_gems[row, column] == EmptyCell) continue;
                    _gems[writeRow, column] = _gems[row, column];
                    writeRow--;
                }

                for (int row = writeRow; row >= 0; row--)
                {
                    _gems[row, column] = _random.Next(Match3Rules.GemKindCount);
                }
            }
        }

        /// <summary>试交换两格扫描消除后立即换回，返回交换是否产生三连。</summary>
        private bool SwapCreatesRun(int indexA, int indexB)
        {
            SwapCells(indexA, indexB);
            bool creates = FindRuns().Count > 0;
            SwapCells(indexA, indexB);
            return creates;
        }

        /// <summary>判断 (row, column) 放 kind 是否会与已生成的左/上方格构成三连（用于生成回填）。</summary>
        private bool WouldExtendRun(int row, int column, int kind)
        {
            return column >= 2 && _gems[row, column - 1] == kind && _gems[row, column - 2] == kind
                || row >= 2 && _gems[row - 1, column] == kind && _gems[row - 2, column] == kind;
        }

        private void SwapCells(int indexA, int indexB)
        {
            int rowA = indexA / Match3Rules.Columns;
            int columnA = indexA % Match3Rules.Columns;
            int rowB = indexB / Match3Rules.Columns;
            int columnB = indexB % Match3Rules.Columns;
            (_gems[rowA, columnA], _gems[rowB, columnB]) = (_gems[rowB, columnB], _gems[rowA, columnA]);
        }

        private static void ValidateIndex(int index)
        {
            if (index < 0 || index >= Match3Rules.CellCount)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, "格位越界。");
            }
        }

        /// <summary>两格是否上下左右相邻（同行或同列且距离 1）。</summary>
        private static bool AreAdjacent(int indexA, int indexB)
        {
            int rowDistance = Math.Abs(indexA / Match3Rules.Columns - indexB / Match3Rules.Columns);
            int columnDistance = Math.Abs(indexA % Match3Rules.Columns - indexB % Match3Rules.Columns);
            return rowDistance + columnDistance == 1;
        }

        private void Log(Match3EventLog entry)
        {
            _events.Add(entry);
            if (_events.Count > MaxEventLogs) _events.RemoveRange(0, _events.Count - MaxEventLogs);
        }
    }
}
