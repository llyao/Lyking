using System;
using System.Collections.Generic;

namespace Lyking.MazeChase.Core
{
    /// <summary>幽灵运行时状态：位置、方向、巢格与复活计时，由核心棋局更新。</summary>
    public sealed class GhostState
    {
        public GhostState(int index, GridPos house)
        {
            Index = index;
            House = house;
            X = house.X;
            Y = house.Y;
        }

        /// <summary>幽灵序号，从 0 开始，决定出生巢格与表现配色。</summary>
        public int Index { get; }

        /// <summary>所属巢格：出生、复位与被吃送回的位置。</summary>
        public GridPos House { get; }

        public float X { get; internal set; }

        public float Y { get; internal set; }

        /// <summary>当前行进方向；<see cref="MazeDir.None"/> 表示停在格点。</summary>
        public MazeDir Direction { get; internal set; }

        /// <summary>是否在巢中等待复活：不移动、不与玩家碰撞。</summary>
        public bool IsRespawning { get; internal set; }

        /// <summary>复活剩余等待时间（秒），仅在 <see cref="IsRespawning"/> 时递减。</summary>
        public float RespawnRemaining { get; internal set; }

        /// <summary>是否处于惊惧状态：惊惧期且未在复活等待中。</summary>
        public bool IsFrightened { get; internal set; }

        /// <summary>是否可被玩家吃掉：惊惧且未在复活等待中。</summary>
        public bool IsEatable => IsFrightened && !IsRespawning;

        /// <summary>当前所在格：位置四舍五入到最近格点。</summary>
        public GridPos Cell => new GridPos((int)MathF.Round(X), (int)MathF.Round(Y));
    }

    /// <summary>
    /// 吃豆迷宫核心棋局：确定性连续移动模拟，无 Unity 依赖。
    /// 玩家与幽灵沿通道按格/秒连续移动，在格点（整数坐标中心）做转向决策：
    /// 玩家按意向方向在格点转向（转角缓冲在格点生效）、墙前停住，通道中可立即反向；
    /// 幽灵由注入种子的贪心决策函数在格点选向，惊惧期反向贪心逃离玩家。
    /// <see cref="Step"/> 内按固定 <see cref="SubStepSeconds"/> 子步长推进，运算顺序固定，
    /// 同种子同输入结果一致。吃豆计分，能量豆触发惊惧可反吃幽灵，清空全部豆子
    /// 过关并提速重来，被普通幽灵抓住失命回出生点，生命耗尽游戏结束。
    /// </summary>
    public sealed class MazeChaseGame
    {
        private const int MaxEventLogs = 128;

        /// <summary>格点对齐与到达判定的公共误差。</summary>
        private const float Epsilon = 1e-5f;

        /// <summary>固定模拟子步长（秒）：所有推进按该步长的整数次执行，保证确定性。</summary>
        public const float SubStepSeconds = 1f / 120f;

        /// <summary>幽灵决策的邻格枚举顺序：固定顺序保证平局候选列表稳定。</summary>
        private static readonly MazeDir[] DecisionOrder = { MazeDir.Up, MazeDir.Down, MazeDir.Left, MazeDir.Right };

        private readonly MazeChaseRules _rules;
        private readonly MazeMap _map;
        private readonly Random _random;
        private readonly HashSet<GridPos> _pellets = new HashSet<GridPos>();
        private readonly HashSet<GridPos> _powerPellets = new HashSet<GridPos>();
        private readonly List<GhostState> _ghosts = new List<GhostState>();
        private readonly List<MazeChaseEventLog> _events = new List<MazeChaseEventLog>();
        private float _accumulator;
        private float _frightenedRemaining;
        private MazeDir _intent;

        public MazeChaseGame(MazeChaseRules? rules = null, int seed = 0)
        {
            _rules = rules ?? MazeChaseRules.Default;
            _map = _rules.Map;
            _random = new Random(seed);
            Lives = _rules.Lives;
            Level = 1;
            for (int i = 0; i < _rules.GhostCount; i++)
                _ghosts.Add(new GhostState(i, _map.GhostHouses[i % _map.GhostHouses.Count]));
            ReloadPellets();
            ResetActors(ghostsWait: false);
            Log(MazeChaseEventType.Spawned);
        }

        public MazeChaseRules Rules => _rules;

        public MazeMap Map => _map;

        public int Width => _map.Width;

        public int Height => _map.Height;

        /// <summary>玩家中心横坐标（格）：格点为整数中心，出生点即 <see cref="MazeMap.PlayerStart"/>。</summary>
        public float PlayerX { get; private set; }

        /// <summary>玩家中心纵坐标（格）。</summary>
        public float PlayerY { get; private set; }

        /// <summary>玩家当前行进方向；<see cref="MazeDir.None"/> 表示停在格点。</summary>
        public MazeDir PlayerDirection { get; private set; }

        /// <summary>当前缓冲的意向方向，由 <see cref="SetIntent"/> 更新。</summary>
        public MazeDir Intent => _intent;

        /// <summary>玩家当前所在格：位置四舍五入到最近格点。</summary>
        public GridPos PlayerCell => new GridPos((int)MathF.Round(PlayerX), (int)MathF.Round(PlayerY));

        public int Score { get; private set; }

        public int Lives { get; private set; }

        /// <summary>当前关卡序号，从 1 开始，每清空一次豆子加 1。</summary>
        public int Level { get; private set; }

        /// <summary>剩余豆子数（含能量豆），清零即过关。</summary>
        public int PelletsRemaining => _pellets.Count + _powerPellets.Count;

        /// <summary>是否处于惊惧期。</summary>
        public bool IsFrightened => _frightenedRemaining > 0f;

        /// <summary>惊惧剩余时间（秒），非惊惧期为 0。</summary>
        public float FrightenedRemaining => _frightenedRemaining;

        /// <summary>棋局累计时间（秒），只被子步推进累加。</summary>
        public float Elapsed { get; private set; }

        public bool IsGameOver { get; private set; }

        public bool IsFinished => IsGameOver;

        public IReadOnlyList<GhostState> Ghosts => _ghosts;

        public IReadOnlyCollection<GridPos> PelletCells => _pellets;

        public IReadOnlyCollection<GridPos> PowerPelletCells => _powerPellets;

        public IReadOnlyList<MazeChaseEventLog> Events => _events;

        /// <summary>设置玩家意向方向：在下一个可转向的格点生效（转角缓冲），重复设置以最后一次为准。</summary>
        public void SetIntent(MazeDir dir)
        {
            if (IsFinished) return;
            _intent = dir;
        }

        /// <summary>推进棋局：把 <paramref name="deltaTime"/> 按固定子步长切分推进；游戏结束后调用直接忽略。</summary>
        public void Step(float deltaTime)
        {
            if (IsFinished) return;
            if (deltaTime < 0f) throw new ArgumentOutOfRangeException(nameof(deltaTime), deltaTime, "推进时间不能为负。");

            _accumulator += deltaTime;
            while (_accumulator >= SubStepSeconds && !IsFinished)
            {
                _accumulator -= SubStepSeconds;
                AdvanceSubStep();
            }
        }

        /// <summary>
        /// 幽灵在格点的方向决策（纯函数）：在可走且非反向的邻格中，
        /// 普通期选距玩家曼哈顿距离最小者，惊惧期选最大者，距离并列时由 <paramref name="random"/> 决定；
        /// 无可走邻格时允许反向折返，仍无路可走时返回 <see cref="MazeDir.None"/> 原地等待。
        /// </summary>
        public static MazeDir DecideGhostDirection(MazeMap map, GridPos cell, MazeDir incoming, GridPos playerCell, bool isFrightened, Random random)
        {
            if (map is null) throw new ArgumentNullException(nameof(map));
            if (random is null) throw new ArgumentNullException(nameof(random));

            var candidates = new List<MazeDir>(4);
            foreach (var dir in DecisionOrder)
            {
                if (dir == incoming.Opposite()) continue;
                if (map.IsWalkable(cell + dir.ToDelta())) candidates.Add(dir);
            }

            if (candidates.Count == 0)
            {
                // 死路：唯一选择是折返；完全被围死则原地等待。
                var back = incoming.Opposite();
                if (back != MazeDir.None && map.IsWalkable(cell + back.ToDelta())) return back;
                return MazeDir.None;
            }

            var ties = new List<MazeDir>(candidates.Count);
            int bestDistance = 0;
            foreach (var dir in candidates)
            {
                var target = cell + dir.ToDelta();
                int distance = Math.Abs(target.X - playerCell.X) + Math.Abs(target.Y - playerCell.Y);
                bool better = ties.Count == 0 || (isFrightened ? distance > bestDistance : distance < bestDistance);
                if (better)
                {
                    bestDistance = distance;
                    ties.Clear();
                    ties.Add(dir);
                }
                else if (distance == bestDistance)
                {
                    ties.Add(dir);
                }
            }

            return ties[random.Next(ties.Count)];
        }

        /// <summary>推进一个固定子步：玩家、豆子、幽灵、惊惧计时与碰撞依次结算，顺序固定。</summary>
        private void AdvanceSubStep()
        {
            float dt = SubStepSeconds;
            Elapsed += dt;

            AdvancePlayer(dt);
            EatAtPlayerCell();

            for (int i = 0; i < _ghosts.Count; i++) UpdateGhostTimer(_ghosts[i], dt);
            for (int i = 0; i < _ghosts.Count; i++) _ghosts[i].IsFrightened = IsFrightened && !_ghosts[i].IsRespawning;
            for (int i = 0; i < _ghosts.Count; i++) AdvanceGhost(_ghosts[i], dt);

            if (_frightenedRemaining > 0f)
            {
                _frightenedRemaining = MathF.Max(0f, _frightenedRemaining - dt);
                if (_frightenedRemaining == 0f) Log(MazeChaseEventType.FrightenedEnded);
            }

            ResolveCollisions();
        }

        /// <summary>推进玩家：格点处做转向决策，通道中可立即反向，撞墙前在格点停住。</summary>
        private void AdvancePlayer(float dt)
        {
            float speed = _rules.PlayerSpeedFor(Level);
            float remaining = dt;
            while (remaining > Epsilon)
            {
                if (IsAtCenter(PlayerX, PlayerY))
                {
                    DecidePlayerAtCenter();
                    if (PlayerDirection == MazeDir.None) return;
                }
                else if (_intent != MazeDir.None && _intent == PlayerDirection.Opposite())
                {
                    // 通道中允许立即反向折返，无需等到格点。
                    PlayerDirection = _intent;
                }

                float distance = DistanceToNextCenter(PlayerX, PlayerY, PlayerDirection);
                float step = speed * remaining;
                if (step >= distance - Epsilon)
                {
                    var center = NextCenter(PlayerX, PlayerY, PlayerDirection);
                    PlayerX = center.X;
                    PlayerY = center.Y;
                    remaining -= distance / speed;
                }
                else
                {
                    var delta = PlayerDirection.ToDelta();
                    PlayerX += delta.X * step;
                    PlayerY += delta.Y * step;
                    remaining = 0f;
                }
            }
        }

        /// <summary>玩家格点转向决策：意向可走则转向（转角缓冲在此生效），否则原方向可走则维持，再则在墙前停住。</summary>
        private void DecidePlayerAtCenter()
        {
            var cell = PlayerCell;
            if (_intent != MazeDir.None && _map.IsWalkable(cell + _intent.ToDelta()))
            {
                PlayerDirection = _intent;
                return;
            }

            if (PlayerDirection != MazeDir.None && _map.IsWalkable(cell + PlayerDirection.ToDelta())) return;
            PlayerDirection = MazeDir.None;
        }

        /// <summary>吃掉玩家当前所在格的豆子；清空全部豆子即过关并重整棋局。</summary>
        private void EatAtPlayerCell()
        {
            var cell = PlayerCell;
            if (_pellets.Remove(cell))
            {
                Score += _rules.ScorePerPellet;
                Log(MazeChaseEventType.AtePellet);
            }
            else if (_powerPellets.Remove(cell))
            {
                Score += _rules.ScorePerPowerPellet;
                Log(MazeChaseEventType.AtePowerPellet);
                if (_frightenedRemaining <= 0f) Log(MazeChaseEventType.FrightenedStarted);
                _frightenedRemaining = _rules.PowerDurationSeconds;
            }

            if (_pellets.Count == 0 && _powerPellets.Count == 0)
            {
                Level++;
                Log(MazeChaseEventType.LevelCleared);
                ReloadPellets();
                ResetActors(ghostsWait: true);
            }
        }

        /// <summary>推进幽灵复活计时；计时归零后幽灵在巢格重新出发。</summary>
        private static void UpdateGhostTimer(GhostState ghost, float dt)
        {
            if (!ghost.IsRespawning) return;
            ghost.RespawnRemaining = MathF.Max(0f, ghost.RespawnRemaining - dt);
            if (ghost.RespawnRemaining == 0f) ghost.IsRespawning = false;
        }

        /// <summary>推进幽灵：每个格点用决策纯函数选向，只在可走邻格间移动；复活等待中的幽灵不动。</summary>
        private void AdvanceGhost(GhostState ghost, float dt)
        {
            if (ghost.IsRespawning) return;

            float speed = _rules.GhostSpeedFor(Level);
            float remaining = dt;
            while (remaining > Epsilon)
            {
                if (IsAtCenter(ghost.X, ghost.Y))
                {
                    ghost.Direction = DecideGhostDirection(_map, ghost.Cell, ghost.Direction, PlayerCell, ghost.IsFrightened, _random);
                    if (ghost.Direction == MazeDir.None) return;
                }

                float distance = DistanceToNextCenter(ghost.X, ghost.Y, ghost.Direction);
                float step = speed * remaining;
                if (step >= distance - Epsilon)
                {
                    var center = NextCenter(ghost.X, ghost.Y, ghost.Direction);
                    ghost.X = center.X;
                    ghost.Y = center.Y;
                    remaining -= distance / speed;
                }
                else
                {
                    var delta = ghost.Direction.ToDelta();
                    ghost.X += delta.X * step;
                    ghost.Y += delta.Y * step;
                    remaining = 0f;
                }
            }
        }

        /// <summary>结算玩家与幽灵的碰撞：惊惧期吃幽灵得分，普通期被抓失命。</summary>
        private void ResolveCollisions()
        {
            float squared = _rules.CollisionDistance * _rules.CollisionDistance;
            for (int i = 0; i < _ghosts.Count; i++)
            {
                var ghost = _ghosts[i];
                if (ghost.IsRespawning) continue;

                float dx = ghost.X - PlayerX;
                float dy = ghost.Y - PlayerY;
                if (dx * dx + dy * dy > squared) continue;

                if (ghost.IsEatable) EatGhost(ghost);
                else CatchPlayer();

                if (IsFinished) return;
            }
        }

        /// <summary>惊惧期吃掉幽灵：加分并把幽灵送回巢格延迟复活。</summary>
        private void EatGhost(GhostState ghost)
        {
            Score += _rules.ScorePerGhost;
            Log(MazeChaseEventType.GhostEaten);
            ghost.X = ghost.House.X;
            ghost.Y = ghost.House.Y;
            ghost.Direction = MazeDir.None;
            ghost.IsRespawning = true;
            ghost.RespawnRemaining = _rules.RespawnDelaySeconds;
            ghost.IsFrightened = false;
        }

        /// <summary>被普通幽灵抓住：扣命；仍有生命则玩家回出生点、幽灵回巢，生命耗尽即结束。</summary>
        private void CatchPlayer()
        {
            Lives--;
            Log(MazeChaseEventType.PlayerCaught);
            if (Lives <= 0)
            {
                IsGameOver = true;
                Log(MazeChaseEventType.GameOver);
                return;
            }

            Log(MazeChaseEventType.LifeLost);
            _frightenedRemaining = 0f;
            ResetActors(ghostsWait: true);
        }

        /// <summary>复位角色：玩家回出生点并清空意向，幽灵回巢；ghostsWait 决定幽灵是否延迟复活。</summary>
        private void ResetActors(bool ghostsWait)
        {
            _intent = MazeDir.None;
            PlayerX = _map.PlayerStart.X;
            PlayerY = _map.PlayerStart.Y;
            PlayerDirection = MazeDir.None;
            for (int i = 0; i < _ghosts.Count; i++)
            {
                var ghost = _ghosts[i];
                ghost.X = ghost.House.X;
                ghost.Y = ghost.House.Y;
                ghost.Direction = MazeDir.None;
                ghost.IsRespawning = ghostsWait;
                ghost.RespawnRemaining = ghostsWait ? _rules.RespawnDelaySeconds : 0f;
                ghost.IsFrightened = false;
            }
        }

        /// <summary>按地图初始布局重铺全部豆子。</summary>
        private void ReloadPellets()
        {
            _pellets.Clear();
            foreach (var pos in _map.InitialPellets) _pellets.Add(pos);
            _powerPellets.Clear();
            foreach (var pos in _map.InitialPowerPellets) _powerPellets.Add(pos);
        }

        private static bool IsAtCenter(float x, float y) => IsIntegral(x) && IsIntegral(y);

        private static bool IsIntegral(float value) => MathF.Abs(value - MathF.Round(value)) < Epsilon;

        /// <summary>沿行进方向到下一个格点的距离（格）；已在格点时为 1，即下一个决策点。</summary>
        private static float DistanceToNextCenter(float x, float y, MazeDir dir) => dir switch
        {
            MazeDir.Right => IsIntegral(x) ? 1f : MathF.Ceiling(x) - x,
            MazeDir.Left => IsIntegral(x) ? 1f : x - MathF.Floor(x),
            MazeDir.Up => IsIntegral(y) ? 1f : MathF.Ceiling(y) - y,
            MazeDir.Down => IsIntegral(y) ? 1f : y - MathF.Floor(y),
            _ => 0f,
        };

        /// <summary>沿行进方向前进一个格距应到达的下一个格点。</summary>
        private static GridPos NextCenter(float x, float y, MazeDir dir) => dir switch
        {
            MazeDir.Right => new GridPos((int)MathF.Floor(x) + 1, (int)y),
            MazeDir.Left => new GridPos((int)MathF.Ceiling(x) - 1, (int)y),
            MazeDir.Up => new GridPos((int)x, (int)MathF.Floor(y) + 1),
            MazeDir.Down => new GridPos((int)x, (int)MathF.Ceiling(y) - 1),
            _ => new GridPos((int)MathF.Round(x), (int)MathF.Round(y)),
        };

        private void Log(MazeChaseEventType type)
        {
            _events.Add(new MazeChaseEventLog(Elapsed, type));
            if (_events.Count > MaxEventLogs) _events.RemoveRange(0, _events.Count - MaxEventLogs);
        }
    }
}
