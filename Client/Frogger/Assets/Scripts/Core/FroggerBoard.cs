using System;
using System.Collections.Generic;

namespace Lyking.Frogger.Core
{
    /// <summary>
    /// 青蛙过河核心棋局：离散跳格与连续滚动混合的确定性模拟，无 Unity 依赖。
    /// 车辆与木排的初始相位、整体方向由注入种子的 <see cref="Random"/> 决定，相邻行方向交替；
    /// <see cref="Step"/> 先结算一次离散跳跃，再按固定 <see cref="SubStepSeconds"/> 子步长推进车流与浮排。
    /// 马路格与车辆相交即被撞，河道格未落上木排即溺水，站上木排随排平移、被带出左右边界即失命；
    /// 到达对岸未占用荷花位进位计分并回到起点，荷花位占满过关提速进入下一轮，生命用尽游戏结束。
    /// </summary>
    public sealed class FroggerBoard
    {
        /// <summary>固定物理子步长（秒）：车流与浮排推进按该步长的整数次执行，保证确定性。</summary>
        public const float SubStepSeconds = 1f / 120f;

        private const int MaxEventLogs = 128;

        private readonly FroggerRules _rules;
        private readonly Random _random;
        private readonly List<FroggerLane> _lanes = new List<FroggerLane>();
        private readonly List<FroggerEventLog> _events = new List<FroggerEventLog>();
        private readonly HashSet<int> _filledGoals = new HashSet<int>();
        private float _accumulator;
        private float _frogX;
        private int _row;
        private int _rideLaneIndex = -1;

        public FroggerBoard(FroggerRules? rules = null, int seed = 0)
        {
            _rules = rules ?? FroggerRules.Default;
            _random = new Random(seed);
            Lives = _rules.MaxLives;
            Round = 1;
            BuildLanes();
            ResetFrog();
            Log(FroggerEventType.Spawned);
        }

        public FroggerRules Rules => _rules;

        public int Width => _rules.Width;

        public int Height => _rules.Height;

        public int Score { get; private set; }

        public int Lives { get; private set; }

        public int Round { get; private set; }

        /// <summary>本轮已进位的荷花位数（进度）。</summary>
        public int GoalsFilled { get; private set; }

        /// <summary>棋局累计时间（秒），只被子步推进累加。</summary>
        public float Elapsed { get; private set; }

        public bool IsGameOver { get; private set; }

        public bool IsFinished => IsGameOver;

        /// <summary>青蛙所在行。</summary>
        public int FrogRow => _row;

        /// <summary>青蛙横坐标：在地面时为格中心整数，站上木排后为连续浮点。</summary>
        public float FrogX => _frogX;

        /// <summary>青蛙当前所在格列（连续横坐标就近取整）。</summary>
        public int FrogColumn => RoundColumn(_frogX);

        /// <summary>青蛙是否站在木排上随排移动。</summary>
        public bool IsRidingLog => _rideLaneIndex >= 0;

        public IReadOnlyList<FroggerLane> Lanes => _lanes;

        public IReadOnlyList<FroggerEventLog> Events => _events;

        /// <summary>荷花位 <paramref name="goalIndex"/>（自左向右 0 起）是否已被占用。</summary>
        public bool IsGoalFilled(int goalIndex) => _filledGoals.Contains(goalIndex);

        /// <summary>
        /// 推进棋局：先结算 <paramref name="hop"/> 的一次离散跳跃（null 表示本帧不跳），
        /// 再把 <paramref name="deltaTime"/> 按固定子步长切分推进车流、浮排与骑乘中的青蛙。
        /// 棋局结束后调用直接忽略。
        /// </summary>
        public void Step(float deltaTime, FroggerDir? hop)
        {
            if (IsFinished) return;
            if (deltaTime < 0f) throw new ArgumentOutOfRangeException(nameof(deltaTime), deltaTime, "推进时间不能为负。");

            if (hop.HasValue) TryHop(hop.GetValueOrDefault());

            _accumulator += deltaTime;
            while (_accumulator >= SubStepSeconds && !IsFinished)
            {
                _accumulator -= SubStepSeconds;
                AdvanceSubStep();
            }
        }

        private void BuildLanes()
        {
            // 方向逐行交替，整体起点方向由种子决定；每条车道的初始相位由种子在轨道周长上任取。
            bool flip = _random.Next(2) == 1;
            for (int lane = 0; lane < FroggerRules.RoadRowCount; lane++)
            {
                AddLane(FroggerLaneKind.Road, FroggerRules.StartRowCount + lane, lane, flip,
                    _rules.CarCountFor(lane), _rules.CarSpeedFor(lane), _rules.CarLengthFor(lane));
            }

            for (int lane = 0; lane < FroggerRules.RiverRowCount; lane++)
            {
                AddLane(FroggerLaneKind.River, _rules.RiverStartRow + lane, lane, flip,
                    _rules.LogCountFor(lane), _rules.LogSpeedFor(lane), _rules.LogLengthFor(lane));
            }
        }

        private void AddLane(FroggerLaneKind kind, int row, int lane, bool flip, int count, float speed, float length)
        {
            int direction = ((lane % 2 == 0) != flip) ? 1 : -1;
            float phase = (float)_random.NextDouble() * Width;
            float spacing = count > 0 ? Width / (float)count : 0f;
            var positions = new float[count];
            for (int i = 0; i < count; i++) positions[i] = Wrap(phase + i * spacing);

            _lanes.Add(new FroggerLane(kind, row, Width, direction, speed, length, positions));
        }

        /// <summary>把轨道坐标折算回 0..轨道周长。</summary>
        private float Wrap(float x)
        {
            float wrapped = x - MathF.Floor(x / Width) * Width;
            return wrapped >= Width ? 0f : wrapped;
        }

        private static int RoundColumn(float x) => (int)MathF.Floor(x + 0.5f);

        private void TryHop(FroggerDir dir)
        {
            var delta = dir.ToDelta();
            int targetRow = _row + delta.Y;
            int targetColumn = RoundColumn(_frogX) + delta.X;

            if (targetRow < 0 || targetRow >= Height || targetColumn < 0 || targetColumn >= Width)
            {
                Log(FroggerEventType.HopRejected);
                return;
            }

            var targetType = _rules.RowTypeFor(targetRow);
            if (targetType == FroggerRowType.Goal)
            {
                ReachGoal(targetColumn);
                return;
            }

            Log(FroggerEventType.Hopped);
            _row = targetRow;
            _frogX = targetColumn;
            _rideLaneIndex = -1;
            if (targetType == FroggerRowType.River) LandOnRiver(targetRow);
            else if (targetType == FroggerRowType.Road) CheckRoadCollision();
        }

        /// <summary>落到河道行：与该行任一木排相交即站上随排移动，否则立即溺水。</summary>
        private void LandOnRiver(int row)
        {
            int laneIndex = LaneIndexForRow(row);
            if (FindPlatform(_lanes[laneIndex], _frogX) < 0)
            {
                Log(FroggerEventType.Drowned);
                LoseLife();
                return;
            }

            _rideLaneIndex = laneIndex;
            Log(FroggerEventType.RodeLog);
        }

        /// <summary>落到对岸：必须落在未占用荷花位才算进位，其余列原地拒绝。</summary>
        private void ReachGoal(int targetColumn)
        {
            int goalIndex = _rules.GoalIndexFor(targetColumn);
            if (goalIndex < 0 || _filledGoals.Contains(goalIndex))
            {
                Log(FroggerEventType.HopRejected);
                return;
            }

            Log(FroggerEventType.Hopped);
            Score += _rules.ScorePerGoal;
            GoalsFilled++;
            _filledGoals.Add(goalIndex);
            Log(FroggerEventType.GoalReached, goalIndex);

            if (GoalsFilled >= _rules.GoalCount)
            {
                Log(FroggerEventType.RoundCleared);
                Round++;
                GoalsFilled = 0;
                _filledGoals.Clear();
            }

            ResetFrog();
        }

        private int FindPlatform(FroggerLane lane, float x)
        {
            float reach = lane.EntityLength * 0.5f + _rules.FrogHalfWidth;
            for (int i = 0; i < lane.EntityCount; i++)
            {
                if (lane.TrackDistance(lane.Positions[i], x) < reach) return i;
            }

            return -1;
        }

        /// <summary>检查青蛙所在马路行是否与车辆相交，相交即被撞失命。</summary>
        private void CheckRoadCollision()
        {
            if (_rideLaneIndex >= 0 || _rules.RowTypeFor(_row) != FroggerRowType.Road) return;

            var lane = _lanes[LaneIndexForRow(_row)];
            float reach = lane.EntityLength * 0.5f + _rules.FrogHalfWidth;
            for (int i = 0; i < lane.EntityCount; i++)
            {
                if (lane.TrackDistance(lane.Positions[i], _frogX) >= reach) continue;

                Log(FroggerEventType.HitByCar);
                LoseLife();
                return;
            }
        }

        /// <summary>推进一个固定子步：车流浮排滚动、骑乘平移、撞车判定依次结算，顺序固定。</summary>
        private void AdvanceSubStep()
        {
            float dt = SubStepSeconds;
            Elapsed += dt;
            float speedFactor = _rules.SpeedFactorFor(Round);

            for (int i = 0; i < _lanes.Count; i++) _lanes[i].Advance(dt, speedFactor);

            if (_rideLaneIndex >= 0)
            {
                var lane = _lanes[_rideLaneIndex];
                _frogX += lane.Direction * lane.Speed * speedFactor * dt;
                if (_frogX < -0.5f || _frogX > Width - 0.5f)
                {
                    Log(FroggerEventType.CarriedOut);
                    LoseLife();
                }

                return;
            }

            CheckRoadCollision();
        }

        private void LoseLife()
        {
            Log(FroggerEventType.LifeLost);
            Lives--;
            if (Lives <= 0)
            {
                IsGameOver = true;
                Log(FroggerEventType.GameOver);
                return;
            }

            ResetFrog();
        }

        /// <summary>青蛙回到起点：底部中央草地整格、脱离木排。</summary>
        private void ResetFrog()
        {
            _row = 0;
            _frogX = _rules.StartColumn;
            _rideLaneIndex = -1;
        }

        /// <summary>网格行对应的滚动车道下标：马路 4 条在前、河道 4 条在后。</summary>
        private int LaneIndexForRow(int row)
        {
            if (row < _rules.RiverStartRow) return row - FroggerRules.StartRowCount;
            return FroggerRules.RoadRowCount + (row - _rules.RiverStartRow);
        }

        private void Log(FroggerEventType type, int index = -1)
        {
            _events.Add(new FroggerEventLog(Elapsed, type, index));
            if (_events.Count > MaxEventLogs) _events.RemoveRange(0, _events.Count - MaxEventLogs);
        }
    }
}
