using System;
using System.Collections.Generic;

namespace Lyking.Frogger.Core
{
    /// <summary>滚动实体所属的行类别：马路车辆或河道木排。</summary>
    public enum FroggerLaneKind
    {
        Road,
        River,
    }

    /// <summary>
    /// 一条循环滚动的车道或河道：实体沿周长等于场宽的环形轨道匀速移动，
    /// 位置始终折算到 0..轨道周长（场宽）区间内；越出场界的部分由环绕距离与绘制补画处理。
    /// </summary>
    public sealed class FroggerLane
    {
        private readonly List<float> _positions = new List<float>();

        public FroggerLane(FroggerLaneKind kind, int row, float trackLength, int direction, float speed,
            float entityLength, IEnumerable<float> positions)
        {
            Kind = kind;
            Row = row;
            TrackLength = trackLength;
            Direction = direction;
            Speed = speed;
            EntityLength = entityLength;
            _positions.AddRange(positions);
        }

        public FroggerLaneKind Kind { get; }

        /// <summary>车道/河道对应的网格行。</summary>
        public int Row { get; }

        /// <summary>环形轨道的周长（格），等于场宽。</summary>
        public float TrackLength { get; }

        /// <summary>滚动方向：+1 向右、-1 向左。</summary>
        public int Direction { get; }

        /// <summary>基础滚动速度（格/秒），随轮次的提速由棋局统一乘算。</summary>
        public float Speed { get; }

        /// <summary>单个实体的长度（格）。</summary>
        public float EntityLength { get; }

        /// <summary>各实体中心的轨道位置，在轨道上等距排布。</summary>
        public IReadOnlyList<float> Positions => _positions;

        public int EntityCount => _positions.Count;

        /// <summary>推进所有实体并环绕回轨道区间，<paramref name="deltaTime"/> 为非负秒数。</summary>
        public void Advance(float deltaTime, float speedFactor)
        {
            float offset = Direction * Speed * speedFactor * deltaTime;
            for (int i = 0; i < _positions.Count; i++)
            {
                float next = _positions[i] + offset;
                next -= MathF.Floor(next / TrackLength) * TrackLength;
                _positions[i] = next >= TrackLength ? 0f : next;
            }
        }

        /// <summary>轨道上两点间的最短环绕距离。</summary>
        public float TrackDistance(float left, float right)
        {
            float direct = MathF.Abs(left - right);
            return MathF.Min(direct, TrackLength - direct);
        }
    }
}
