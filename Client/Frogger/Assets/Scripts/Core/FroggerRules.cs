using System;
using System.Collections.Generic;

namespace Lyking.Frogger.Core
{
    /// <summary>场地行的地形类型，供核心判定与表现层分色。</summary>
    public enum FroggerRowType
    {
        Grass,
        Road,
        River,
        Goal,
    }

    /// <summary>
    /// 青蛙过河规则常量：场地行列结构、生命、荷花位、计分与每条车道/河道的实体配置。
    /// 行结构固定为 1 行起点草地、4 行马路、3 行中间安全带、4 行河道与 1 行对岸目标；
    /// 车辆与木排的数量、速度、长度按车道序号查询，未注入配置时使用默认档位，均为确定性纯函数。
    /// </summary>
    public sealed class FroggerRules
    {
        public const int StartRowCount = 1;
        public const int RoadRowCount = 4;
        public const int MedianRowCount = 3;
        public const int RiverRowCount = 4;

        public static readonly FroggerRules Default = new FroggerRules(
            width: 13,
            maxLives: 3,
            goalCount: 5,
            scorePerGoal: 200,
            roundSpeedGain: 0.15f,
            frogHalfWidth: 0.35f);

        private readonly int[]? _carCounts;
        private readonly float[]? _carSpeeds;
        private readonly float[]? _carLengths;
        private readonly int[]? _logCounts;
        private readonly float[]? _logSpeeds;
        private readonly float[]? _logLengths;
        private readonly List<int> _goalColumns;

        public FroggerRules(
            int width,
            int maxLives,
            int goalCount,
            int scorePerGoal,
            float roundSpeedGain,
            float frogHalfWidth,
            int[]? carCounts = null,
            float[]? carSpeeds = null,
            float[]? carLengths = null,
            int[]? logCounts = null,
            float[]? logSpeeds = null,
            float[]? logLengths = null)
        {
            if (width < 5) throw new ArgumentOutOfRangeException(nameof(width), width, "场地宽度至少 5，需并排容纳荷花位。");
            if (maxLives < 1) throw new ArgumentOutOfRangeException(nameof(maxLives), maxLives, "最大生命至少为 1。");
            if (goalCount < 1) throw new ArgumentOutOfRangeException(nameof(goalCount), goalCount, "荷花位至少 1 个。");
            if (scorePerGoal <= 0) throw new ArgumentOutOfRangeException(nameof(scorePerGoal), scorePerGoal, "每次进位得分必须为正。");
            if (roundSpeedGain < 0f) throw new ArgumentOutOfRangeException(nameof(roundSpeedGain), roundSpeedGain, "每轮提速比例不能为负。");
            if (frogHalfWidth <= 0f || frogHalfWidth * 2f > width)
                throw new ArgumentOutOfRangeException(nameof(frogHalfWidth), frogHalfWidth, "青蛙半宽必须为正且不能超过场宽。");

            _carCounts = ValidateCounts(carCounts, RoadRowCount, nameof(carCounts));
            _carSpeeds = ValidateSpeeds(carSpeeds, RoadRowCount, nameof(carSpeeds));
            _carLengths = ValidateLengths(carLengths, RoadRowCount, width, nameof(carLengths));
            _logCounts = ValidateCounts(logCounts, RiverRowCount, nameof(logCounts));
            _logSpeeds = ValidateSpeeds(logSpeeds, RiverRowCount, nameof(logSpeeds));
            _logLengths = ValidateLengths(logLengths, RiverRowCount, width, nameof(logLengths));

            Width = width;
            MaxLives = maxLives;
            GoalCount = goalCount;
            ScorePerGoal = scorePerGoal;
            RoundSpeedGain = roundSpeedGain;
            FrogHalfWidth = frogHalfWidth;
            _goalColumns = ComputeGoalColumns(width, goalCount);
        }

        public int Width { get; }

        public int MaxLives { get; }

        public int GoalCount { get; }

        public int ScorePerGoal { get; }

        public float RoundSpeedGain { get; }

        public float FrogHalfWidth { get; }

        /// <summary>对岸荷花位所在的列，自左向右升序，宽 13、5 位时落在 1、4、6、8、11 列。</summary>
        public IReadOnlyList<int> GoalColumns => _goalColumns;

        /// <summary>目标列对应的荷花位次（自左向右 0 起），非荷花列返回 -1。</summary>
        public int GoalIndexFor(int column) => _goalColumns.IndexOf(column);

        public int Height => StartRowCount + RoadRowCount + MedianRowCount + RiverRowCount + 1;

        public int GoalRow => Height - 1;

        /// <summary>青蛙起点所在的列（底部中央）。</summary>
        public int StartColumn => Width / 2;

        /// <summary>最下方河道行：马路四行与中间安全带之上。</summary>
        public int RiverStartRow => StartRowCount + RoadRowCount + MedianRowCount;

        /// <summary>场地行 <paramref name="row"/> 的地形类型，行号越界抛出异常。</summary>
        public FroggerRowType RowTypeFor(int row)
        {
            if (row < 0 || row >= Height) throw new ArgumentOutOfRangeException(nameof(row), row, "行号超出场地。");
            if (row < StartRowCount) return FroggerRowType.Grass;
            if (row < StartRowCount + RoadRowCount) return FroggerRowType.Road;
            if (row < RiverStartRow) return FroggerRowType.Grass;
            if (row < GoalRow) return FroggerRowType.River;
            return FroggerRowType.Goal;
        }

        /// <summary>第 <paramref name="lane"/> 条车道（0 起、自下而上）的车辆数量。</summary>
        public int CarCountFor(int lane)
        {
            CheckLane(lane, RoadRowCount);
            return _carCounts != null ? _carCounts[lane] : lane switch { 0 => 3, 1 => 2, 2 => 3, _ => 4 };
        }

        /// <summary>第 <paramref name="lane"/> 条车道的基础车速（格/秒）。</summary>
        public float CarSpeedFor(int lane)
        {
            CheckLane(lane, RoadRowCount);
            return _carSpeeds != null ? _carSpeeds[lane] : lane switch { 0 => 2.4f, 1 => 1.6f, 2 => 3.2f, _ => 2.0f };
        }

        /// <summary>第 <paramref name="lane"/> 条车道的车长（格）。</summary>
        public float CarLengthFor(int lane)
        {
            CheckLane(lane, RoadRowCount);
            return _carLengths != null ? _carLengths[lane] : lane switch { 0 => 1.2f, 1 => 2.2f, 2 => 1.2f, _ => 1.0f };
        }

        /// <summary>第 <paramref name="lane"/> 条河道（0 起、自下而上）的木排数量。</summary>
        public int LogCountFor(int lane)
        {
            CheckLane(lane, RiverRowCount);
            return _logCounts != null ? _logCounts[lane] : lane switch { 0 => 3, 1 => 4, 2 => 2, _ => 3 };
        }

        /// <summary>第 <paramref name="lane"/> 条河道的基础木排速度（格/秒）。</summary>
        public float LogSpeedFor(int lane)
        {
            CheckLane(lane, RiverRowCount);
            return _logSpeeds != null ? _logSpeeds[lane] : lane switch { 0 => 1.2f, 1 => 1.6f, 2 => 0.9f, _ => 1.4f };
        }

        /// <summary>第 <paramref name="lane"/> 条河道的木排长度（格）。</summary>
        public float LogLengthFor(int lane)
        {
            CheckLane(lane, RiverRowCount);
            return _logLengths != null ? _logLengths[lane] : lane switch { 0 => 3f, 1 => 2f, 2 => 4f, _ => 3f };
        }

        /// <summary>第 <paramref name="round"/> 轮的整体速度倍率：每轮线性提升，确定性纯函数。</summary>
        public float SpeedFactorFor(int round)
        {
            if (round < 1) throw new ArgumentOutOfRangeException(nameof(round), round, "轮次从 1 开始。");
            return 1f + (round - 1) * RoundSpeedGain;
        }

        private static int CheckLane(int lane, int laneCount)
        {
            if (lane < 0 || lane >= laneCount)
                throw new ArgumentOutOfRangeException(nameof(lane), lane, $"车道序号必须在 0..{laneCount - 1}。");
            return lane;
        }

        private static List<int> ComputeGoalColumns(int width, int goalCount)
        {
            var columns = new List<int>(goalCount);
            for (int i = 0; i < goalCount; i++)
            {
                int column = (int)MathF.Floor((width - 1) * (2 * i + 1) / (2f * goalCount) + 0.5f);
                if (i > 0 && column <= columns[i - 1])
                    throw new ArgumentException("场宽不足以并排排布全部荷花位。", nameof(goalCount));
                columns.Add(column);
            }

            return columns;
        }

        private static int[]? ValidateCounts(int[]? counts, int laneCount, string paramName)
        {
            if (counts == null) return null;
            if (counts.Length != laneCount) throw new ArgumentException($"配置长度必须为 {laneCount}。", paramName);
            foreach (int count in counts)
            {
                if (count < 0) throw new ArgumentOutOfRangeException(paramName, count, "实体数量不能为负。");
            }

            return counts;
        }

        private static float[]? ValidateSpeeds(float[]? speeds, int laneCount, string paramName)
        {
            if (speeds == null) return null;
            if (speeds.Length != laneCount) throw new ArgumentException($"配置长度必须为 {laneCount}。", paramName);
            foreach (float speed in speeds)
            {
                if (speed < 0f) throw new ArgumentOutOfRangeException(paramName, speed, "滚动速度不能为负。");
            }

            return speeds;
        }

        private static float[]? ValidateLengths(float[]? lengths, int laneCount, int width, string paramName)
        {
            if (lengths == null) return null;
            if (lengths.Length != laneCount) throw new ArgumentException($"配置长度必须为 {laneCount}。", paramName);
            foreach (float length in lengths)
            {
                if (length <= 0f || length > width)
                    throw new ArgumentOutOfRangeException(paramName, length, "实体长度必须为正且不能超过场宽。");
            }

            return lengths;
        }
    }
}
