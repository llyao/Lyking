using System.Collections.Generic;

namespace Lyking.Reversi.Core
{
    /// <summary>
    /// 黑白棋规则常量与纯函数助手：8x8 棋盘、8 个落子方向、标准开局、
    /// 夹击翻转计算与角/边加权评估。全部确定性，无 Unity 依赖、无随机数。
    /// 行列索引均从 0 起（0 为左上格），坐标记法为列字母 + 行号（如 d3）。
    /// </summary>
    public static class ReversiRules
    {
        /// <summary>棋盘边长：每行每列各 8 格。</summary>
        public const int Size = 8;

        /// <summary>棋盘总格数。</summary>
        public const int CellCount = Size * Size;

        /// <summary>AI 极大极小搜索深度。</summary>
        public const int SearchDepth = 3;

        /// <summary>角点评估权重。</summary>
        public const int CornerWeight = 100;

        /// <summary>边线评估权重。</summary>
        public const int EdgeWeight = 10;

        /// <summary>内部格评估权重。</summary>
        public const int InnerWeight = 1;

        private static readonly (int RowOffset, int ColOffset)[] DirectionBuffer =
        {
            (0, 1), (1, 0), (0, -1), (-1, 0),
            (1, 1), (1, -1), (-1, 1), (-1, -1),
        };

        /// <summary>全部 8 个落子方向：正交四向与对角四向。</summary>
        public static IReadOnlyList<(int RowOffset, int ColOffset)> Directions { get; } = DirectionBuffer;

        private static readonly (int Row, int Col)[] CornerBuffer =
        {
            (0, 0), (0, Size - 1), (Size - 1, 0), (Size - 1, Size - 1),
        };

        /// <summary>四个角点坐标。</summary>
        public static IReadOnlyList<(int Row, int Col)> Corners { get; } = CornerBuffer;

        private static readonly int[,] WeightBuffer = BuildWeights();

        /// <summary>指定格的评估权重：角 100、边 10、其余 1。</summary>
        public static int WeightAt(int row, int col) => WeightBuffer[row, col];

        /// <summary>标准开局四子：d4/e5 白子、d5/e4 黑子，其余清空。</summary>
        public static void PlaceInitialStones(Cell[,] cells)
        {
            for (int row = 0; row < Size; row++)
            {
                for (int col = 0; col < Size; col++) cells[row, col] = Cell.None;
            }

            cells[3, 3] = Cell.White;   // d4
            cells[4, 4] = Cell.White;   // e5
            cells[3, 4] = Cell.Black;   // e4
            cells[4, 3] = Cell.Black;   // d5
        }

        /// <summary>行优先展平索引：index = 行 * Size + 列。</summary>
        public static int FlatIndex(int row, int col) => row * Size + col;

        /// <summary>行列坐标的标准记法（如 2,3 → d3）。</summary>
        public static string ToCoordinate(int row, int col) => $"{(char)('a' + col)}{row + 1}";

        /// <summary>返回 <paramref name="side"/> 的对方；空方返回空方。</summary>
        public static Cell OpponentOf(Cell side)
        {
            return side == Cell.Black ? Cell.White : side == Cell.White ? Cell.Black : Cell.None;
        }

        /// <summary>
        /// 计算 <paramref name="side"/> 在空格 (row, col) 落子将翻转的格子索引；
        /// 八个方向各自夹击到的对方棋子按方向顺序合并，不构成夹击时为空集合。
        /// </summary>
        public static List<int> FlipsFor(Cell[,] cells, int row, int col, Cell side)
        {
            var flipped = new List<int>();
            if (side == Cell.None || cells[row, col] != Cell.None) return flipped;

            var opponent = OpponentOf(side);
            for (int i = 0; i < DirectionBuffer.Length; i++)
            {
                var (rowOffset, colOffset) = DirectionBuffer[i];
                int runRow = row + rowOffset;
                int runCol = col + colOffset;
                var run = new List<int>();
                while (InBounds(runRow, runCol) && cells[runRow, runCol] == opponent)
                {
                    run.Add(FlatIndex(runRow, runCol));
                    runRow += rowOffset;
                    runCol += colOffset;
                }

                // 夹击成立要求该方向至少一枚对方子且末端是己方子。
                if (run.Count > 0 && InBounds(runRow, runCol) && cells[runRow, runCol] == side)
                {
                    flipped.AddRange(run);
                }
            }

            return flipped;
        }

        /// <summary><paramref name="side"/> 在给定盘面上是否存在至少一个合法步。</summary>
        public static bool HasAnyMove(Cell[,] cells, Cell side)
        {
            if (side == Cell.None) return false;

            for (int row = 0; row < Size; row++)
            {
                for (int col = 0; col < Size; col++)
                {
                    if (cells[row, col] == Cell.None && CanFlip(cells, row, col, side)) return true;
                }
            }

            return false;
        }

        /// <summary>收集 <paramref name="side"/> 的全部合法步（行优先顺序，含各自翻转集合）。</summary>
        public static IReadOnlyList<ReversiMove> CollectMoves(Cell[,] cells, Cell side)
        {
            var moves = new List<ReversiMove>();
            if (side == Cell.None) return moves;

            for (int row = 0; row < Size; row++)
            {
                for (int col = 0; col < Size; col++)
                {
                    if (cells[row, col] != Cell.None) continue;

                    var flipped = FlipsFor(cells, row, col, side);
                    if (flipped.Count > 0) moves.Add(new ReversiMove(row, col, flipped));
                }
            }

            return moves;
        }

        /// <summary>在盘面上执行落步：写入落子并把翻转集合中的格子改为己方。</summary>
        public static void ApplyMove(Cell[,] cells, ReversiMove move, Cell side)
        {
            cells[move.Row, move.Col] = side;
            for (int i = 0; i < move.Flipped.Count; i++)
            {
                int index = move.Flipped[i];
                cells[index / Size, index % Size] = side;
            }
        }

        /// <summary>按角/边权重计算黑白子差，正值利于黑方。</summary>
        public static int Evaluate(Cell[,] cells)
        {
            int score = 0;
            for (int row = 0; row < Size; row++)
            {
                for (int col = 0; col < Size; col++)
                {
                    var cell = cells[row, col];
                    if (cell == Cell.None) continue;

                    score += cell == Cell.Black ? WeightAt(row, col) : -WeightAt(row, col);
                }
            }

            return score;
        }

        private static int[,] BuildWeights()
        {
            var weights = new int[Size, Size];
            for (int row = 0; row < Size; row++)
            {
                for (int col = 0; col < Size; col++)
                {
                    bool isEdge = row == 0 || row == Size - 1 || col == 0 || col == Size - 1;
                    bool isCorner = isEdge && (row == 0 || row == Size - 1) && (col == 0 || col == Size - 1);
                    weights[row, col] = isCorner ? CornerWeight : isEdge ? EdgeWeight : InnerWeight;
                }
            }

            return weights;
        }

        /// <summary>空格 (row, col) 落子是否存在任一方向的夹击（无分配版本，供热路径使用）。</summary>
        private static bool CanFlip(Cell[,] cells, int row, int col, Cell side)
        {
            var opponent = OpponentOf(side);
            for (int i = 0; i < DirectionBuffer.Length; i++)
            {
                var (rowOffset, colOffset) = DirectionBuffer[i];
                int runRow = row + rowOffset;
                int runCol = col + colOffset;
                int runLength = 0;
                while (InBounds(runRow, runCol) && cells[runRow, runCol] == opponent)
                {
                    runLength++;
                    runRow += rowOffset;
                    runCol += colOffset;
                }

                if (runLength > 0 && InBounds(runRow, runCol) && cells[runRow, runCol] == side) return true;
            }

            return false;
        }

        private static bool InBounds(int row, int col)
        {
            return row >= 0 && row < Size && col >= 0 && col < Size;
        }
    }
}
