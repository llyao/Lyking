using System;
using System.Collections.Generic;

namespace Lyking.GameOfLife.Core
{
    /// <summary>
    /// 生命游戏核心棋局：<see cref="Width"/>x<see cref="Height"/> 环形网格，B3/S23 规则，
    /// 双缓冲计算下一代。无 Unity 依赖、无内建时钟与随机数；随机撒子由注入种子的
    /// <see cref="Random"/> 按行优先逐格判定，同种子同密度布局完全一致。
    /// 细胞记录存活代数（<see cref="Age"/>）：新生记 1，每多存活一代加 1，死亡清零。
    /// 事件日志只记录关键操作（撒子、清空、载入图案、暂停/继续），逐代推进不记录。
    /// </summary>
    public sealed class LifeBoard
    {
        private const int MaxEventLogs = 128;

        private readonly int _width;
        private readonly int _height;
        private readonly List<LifeEventLog> _events = new List<LifeEventLog>();
        private bool[,] _cells;
        private bool[,] _nextCells;
        private int[,] _age;
        private int[,] _nextAge;

        public LifeBoard(int width = LifeRules.DefaultWidth, int height = LifeRules.DefaultHeight)
        {
            if (width < LifeRules.MinSize || width > LifeRules.MaxSize)
            {
                throw new ArgumentOutOfRangeException(nameof(width), width,
                    $"棋盘宽度须在 {LifeRules.MinSize} 到 {LifeRules.MaxSize} 之间。");
            }

            if (height < LifeRules.MinSize || height > LifeRules.MaxSize)
            {
                throw new ArgumentOutOfRangeException(nameof(height), height,
                    $"棋盘高度须在 {LifeRules.MinSize} 到 {LifeRules.MaxSize} 之间。");
            }

            _width = width;
            _height = height;
            _cells = new bool[width, height];
            _nextCells = new bool[width, height];
            _age = new int[width, height];
            _nextAge = new int[width, height];
        }

        /// <summary>网格宽度（x 方向格数）。</summary>
        public int Width => _width;

        /// <summary>网格高度（y 方向格数）。</summary>
        public int Height => _height;

        /// <summary>已推进的代数，从 0 计；撒子、清空与载入图案都会归零。</summary>
        public int Generation { get; private set; }

        /// <summary>当前活细胞数。</summary>
        public int AliveCount { get; private set; }

        /// <summary>是否暂停。只影响表现层的固定节拍推进，显式 <see cref="Step"/> 不受限。</summary>
        public bool IsPaused { get; private set; }

        public IReadOnlyList<LifeEventLog> Events => _events;

        /// <summary>当前布局的副本：true 为活细胞，修改副本不影响棋局。</summary>
        public bool[,] Cells => (bool[,])_cells.Clone();

        /// <summary>当前年龄分布的副本：活细胞至少为 1，死细胞为 0。</summary>
        public int[,] Age => (int[,])_age.Clone();

        /// <summary>查询格位是否有活细胞。</summary>
        public bool IsAlive(int x, int y)
        {
            EnsureCell(x, y);
            return _cells[x, y];
        }

        /// <summary>查询格位的存活代数，死细胞为 0。</summary>
        public int AgeAt(int x, int y)
        {
            EnsureCell(x, y);
            return _age[x, y];
        }

        /// <summary>推进一代：B3/S23 规则、环形邻居计数、双缓冲写入下一代；不写事件日志。</summary>
        public void Step()
        {
            int alive = 0;
            for (int y = 0; y < _height; y++)
            {
                for (int x = 0; x < _width; x++)
                {
                    int neighbors = CountNeighbors(x, y);
                    bool wasAlive = _cells[x, y];
                    bool nextAlive = wasAlive
                        ? neighbors >= LifeRules.MinSurviveNeighbors && neighbors <= LifeRules.MaxSurviveNeighbors
                        : neighbors == LifeRules.BirthNeighbors;
                    _nextCells[x, y] = nextAlive;
                    _nextAge[x, y] = nextAlive ? (wasAlive ? _age[x, y] + 1 : 1) : 0;
                    if (nextAlive) alive++;
                }
            }

            var swapCells = _cells;
            _cells = _nextCells;
            _nextCells = swapCells;
            var swapAge = _age;
            _age = _nextAge;
            _nextAge = swapAge;
            AliveCount = alive;
            Generation++;
        }

        /// <summary>清空棋盘：全部死亡、年龄清零、代数归零。</summary>
        public void Clear()
        {
            Array.Clear(_cells, 0, _cells.Length);
            Array.Clear(_age, 0, _age.Length);
            AliveCount = 0;
            Generation = 0;
            Log(LifeEventType.Cleared);
        }

        /// <summary>
        /// 随机撒子：以注入种子按行优先逐格判定，概率 <paramref name="density"/> 决定初始活细胞，
        /// 同种子同密度布局完全一致；撒子后代数归零并记录事件。
        /// </summary>
        public void Randomize(int seed, double density)
        {
            if (density < 0.0 || density > 1.0)
            {
                throw new ArgumentOutOfRangeException(nameof(density), density, "密度须在 0 到 1 之间。");
            }

            var random = new Random(seed);
            Array.Clear(_cells, 0, _cells.Length);
            Array.Clear(_age, 0, _age.Length);
            int alive = 0;
            for (int y = 0; y < _height; y++)
            {
                for (int x = 0; x < _width; x++)
                {
                    if (random.NextDouble() >= density) continue;

                    _cells[x, y] = true;
                    _age[x, y] = 1;
                    alive++;
                }
            }

            AliveCount = alive;
            Generation = 0;
            Log(LifeEventType.Randomized, alive);
        }

        /// <summary>
        /// 清空后载入图案：按图案包围盒在棋盘上居中放置，格位环形取模（越界环绕到对侧），
        /// 活细胞年龄记 1，代数归零并记录事件。
        /// </summary>
        public void LoadPattern(IReadOnlyList<(int X, int Y)> cells, string name = "自定义图案")
        {
            if (cells == null) throw new ArgumentNullException(nameof(cells));
            if (cells.Count == 0) throw new ArgumentException("图案格位不能为空。", nameof(cells));

            Array.Clear(_cells, 0, _cells.Length);
            Array.Clear(_age, 0, _age.Length);

            int minX = int.MaxValue, maxX = int.MinValue, minY = int.MaxValue, maxY = int.MinValue;
            foreach (var (x, y) in cells)
            {
                if (x < minX) minX = x;
                if (x > maxX) maxX = x;
                if (y < minY) minY = y;
                if (y > maxY) maxY = y;
            }

            int offsetX = (_width - (maxX - minX + 1)) / 2 - minX;
            int offsetY = (_height - (maxY - minY + 1)) / 2 - minY;

            int alive = 0;
            foreach (var (x, y) in cells)
            {
                int px = ((x + offsetX) % _width + _width) % _width;
                int py = ((y + offsetY) % _height + _height) % _height;
                if (!_cells[px, py]) alive++;
                _cells[px, py] = true;
                _age[px, py] = 1;
            }

            AliveCount = alive;
            Generation = 0;
            Log(LifeEventType.PatternLoaded, alive, name);
        }

        /// <summary>翻转单格（人工绘制用）：活细胞年龄记 1，死亡清零；维护活细胞计数，不写事件日志。</summary>
        public void ToggleCell(int x, int y)
        {
            EnsureCell(x, y);
            if (_cells[x, y])
            {
                _cells[x, y] = false;
                _age[x, y] = 0;
                AliveCount--;
            }
            else
            {
                _cells[x, y] = true;
                _age[x, y] = 1;
                AliveCount++;
            }
        }

        /// <summary>切换暂停状态：状态变化时写入暂停/继续事件；是否据此停拍由表现层决定。</summary>
        public void SetPaused(bool paused)
        {
            if (IsPaused == paused) return;

            IsPaused = paused;
            Log(paused ? LifeEventType.Paused : LifeEventType.Resumed);
        }

        /// <summary>环形邻居计数：上下左右与四角共 8 格，跨界按对侧取模。</summary>
        private int CountNeighbors(int x, int y)
        {
            int count = 0;
            for (int dy = -1; dy <= 1; dy++)
            {
                int ny = (y + dy + _height) % _height;
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0) continue;

                    int nx = (x + dx + _width) % _width;
                    if (_cells[nx, ny]) count++;
                }
            }

            return count;
        }

        private void EnsureCell(int x, int y)
        {
            if (x < 0 || x >= _width) throw new ArgumentOutOfRangeException(nameof(x), x, "x 越界。");
            if (y < 0 || y >= _height) throw new ArgumentOutOfRangeException(nameof(y), y, "y 越界。");
        }

        private void Log(LifeEventType type, int cellCount = 0, string patternName = "")
        {
            _events.Add(new LifeEventLog(Generation, type, cellCount, patternName));
            if (_events.Count > MaxEventLogs) _events.RemoveRange(0, _events.Count - MaxEventLogs);
        }
    }
}
