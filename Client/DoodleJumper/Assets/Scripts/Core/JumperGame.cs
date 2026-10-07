using System;
using System.Collections.Generic;

namespace Lyking.DoodleJumper.Core
{
    /// <summary>
    /// 涂鸦跳跃核心棋局：确定性连续模拟，无 Unity 依赖、无输入轮询。
    /// 玩家在有限宽度、向上无上限的场地内自动弹跳：只有下落时才与平台顶碰撞，
    /// 上升直接穿透；普通平台踩上即弹起、易碎平台踩上即碎不弹起、移动平台踩上照常弹起并按
    /// 生成序号相位水平往返；平台由注入种子的 <see cref="Random"/> 在镜头上方向上生成，
    /// 低于镜头下缘的移除；玩家底部跌出历史最高高度以下的判定窗口即结束，
    /// 分数等于历史最大爬升高度换算。调用方按固定子步长调用 <see cref="Advance"/>。
    /// </summary>
    public sealed class JumperGame
    {
        private const int MaxEventLogs = 128;

        private readonly JumperRules _rules;
        private readonly List<JumperPlatform> _platforms = new List<JumperPlatform>();
        private readonly List<JumperEventLog> _events = new List<JumperEventLog>();
        private Random _random;
        private int _nextPlatformIndex;
        private float _nextSpawnY;

        public JumperGame(JumperRules? rules = null, int seed = 0)
        {
            _rules = rules ?? JumperRules.Default;
            _random = new Random(seed);
            ResetState();
            Log(JumperEventType.Spawned);
        }

        public JumperRules Rules => _rules;

        public float FieldWidth => _rules.FieldWidth;

        /// <summary>玩家中心位置（X 受场界处理约束，Y 随竖直速度变化）。</summary>
        public Vec2 PlayerPosition { get; private set; }

        /// <summary>玩家水平速度（向目标速度按加速度逼近）。</summary>
        public float VelocityX { get; private set; }

        /// <summary>玩家竖直速度（上升为正、下落为负，被下落上限夹住）。</summary>
        public float VelocityY { get; private set; }

        /// <summary>历史最大爬升高度（玩家底部相对起始平台顶的最高值，米）。</summary>
        public float MaxHeight { get; private set; }

        /// <summary>镜头中心高度，只升不降。</summary>
        public float CameraCenterY { get; private set; }

        /// <summary>当前分数：历史最大爬升高度乘每米得分，向下取整。</summary>
        public int Score { get; private set; }

        /// <summary>已累计的模拟时间（秒），驱动移动平台往返。</summary>
        public float Time { get; private set; }

        public bool IsGameOver { get; private set; }

        public bool IsFinished => IsGameOver;

        /// <summary>已完成的物理子步数。</summary>
        public int StepIndex { get; private set; }

        public IReadOnlyList<JumperPlatform> Platforms => _platforms;

        public IReadOnlyList<JumperEventLog> Events => _events;

        /// <summary>镜头视野下缘的世界高度。</summary>
        public float CameraBottomY => CameraCenterY - _rules.ViewHeight * 0.5f;

        /// <summary>镜头视野上缘的世界高度。</summary>
        public float CameraTopY => CameraCenterY + _rules.ViewHeight * 0.5f;

        /// <summary>重开一局：清空全部状态并以新种子重建随机序列，等价于重新构造核心。</summary>
        public void Reset(int seed)
        {
            _random = new Random(seed);
            ResetState();
            Log(JumperEventType.Reset);
        }

        /// <summary>
        /// 推进一个物理子步：<paramref name="input.MoveX"/> 与 <paramref name="input.FastFallHeld"/>
        /// 均为持续输入，在本子步内生效。返回 false 表示棋局已结束。
        /// </summary>
        public bool Advance(float deltaTime, in JumperInput input)
        {
            if (deltaTime < 0f) throw new ArgumentOutOfRangeException(nameof(deltaTime), deltaTime, "推进时长不能为负。");
            if (IsFinished) return false;

            StepIndex++;
            Time += deltaTime;
            RepositionMovingPlatforms();
            MoveHorizontally(deltaTime, input.MoveX);
            float previousBottom = PlayerPosition.Y - _rules.PlayerHalfHeight;
            IntegrateVertically(deltaTime, input.FastFallHeld);
            HandlePlatformCollision(previousBottom);
            UpdateMaxHeightAndScore();
            UpdateCamera();
            CullPlatforms();
            GeneratePlatforms();
            return !CheckDeath();
        }

        private void ResetState()
        {
            // 起始平台顶面定为高度 0：平台中心在其厚度一半之下，玩家底部恰好落在平台顶。
            float centerX = _rules.FieldWidth * 0.5f;
            PlayerPosition = new Vec2(centerX, _rules.PlayerHalfHeight);
            VelocityX = 0f;
            VelocityY = _rules.JumpSpeed;
            MaxHeight = 0f;
            CameraCenterY = _rules.CameraTargetY(PlayerPosition.Y);
            Score = 0;
            Time = 0f;
            StepIndex = 0;
            IsGameOver = false;
            _nextPlatformIndex = 0;
            _nextSpawnY = -_rules.PlatformThickness * 0.5f;
            _platforms.Clear();
            _platforms.Add(new JumperPlatform(_nextPlatformIndex++, JumperPlatformType.Normal,
                new Vec2(centerX, _nextSpawnY), centerX));
            _events.Clear();
            GeneratePlatforms();
        }

        /// <summary>水平速度按加速度向目标逼近，再按场界模式夹边或环绕。</summary>
        private void MoveHorizontally(float deltaTime, float moveX)
        {
            float intent = Math.Clamp(moveX, -1f, 1f);
            float target = intent * _rules.MaxHorizontalSpeed;
            float maxDelta = _rules.HorizontalAccel * deltaTime;
            float diff = target - VelocityX;
            if (diff > maxDelta) diff = maxDelta;
            else if (diff < -maxDelta) diff = -maxDelta;
            VelocityX += diff;

            float x = PlayerPosition.X + VelocityX * deltaTime;
            if (_rules.WallMode == JumperWallMode.Wrap)
            {
                x = ((x % _rules.FieldWidth) + _rules.FieldWidth) % _rules.FieldWidth;
            }
            else
            {
                x = Math.Clamp(x, _rules.PlayerHalfWidth, _rules.FieldWidth - _rules.PlayerHalfWidth);
            }

            PlayerPosition = PlayerPosition.WithX(x);
        }

        /// <summary>重力积分：按住快落键时重力放大，下落速度夹在上限内。</summary>
        private void IntegrateVertically(float deltaTime, bool fastFallHeld)
        {
            float gravity = _rules.Gravity * (fastFallHeld ? _rules.FastFallMultiplier : 1f);
            float vy = VelocityY - gravity * deltaTime;
            if (vy < -_rules.MaxFallSpeed) vy = -_rules.MaxFallSpeed;
            VelocityY = vy;
            PlayerPosition = PlayerPosition.WithY(PlayerPosition.Y + vy * deltaTime);
        }

        /// <summary>
        /// 仅在下落时检测平台碰撞：按扫掠区间（本步前后玩家底部）找被穿过平台顶中最高的一块；
        /// 易碎平台踩上即碎不弹起，其余踩上即把速度置顶为弹跳速度并把底部贴回平台顶。
        /// </summary>
        private void HandlePlatformCollision(float previousBottom)
        {
            if (VelocityY >= 0f) return;

            float playerLeft = PlayerPosition.X - _rules.PlayerHalfWidth;
            float playerRight = PlayerPosition.X + _rules.PlayerHalfWidth;
            float currentBottom = PlayerPosition.Y - _rules.PlayerHalfHeight;
            int bestIndex = -1;
            float bestTop = float.NegativeInfinity;
            for (int i = 0; i < _platforms.Count; i++)
            {
                var platform = _platforms[i];
                float left = platform.Center.X - _rules.PlatformHalfWidth;
                float right = platform.Center.X + _rules.PlatformHalfWidth;
                if (playerLeft >= right || playerRight <= left) continue;
                float top = platform.Center.Y + _rules.PlatformThickness * 0.5f;
                if (previousBottom < top || currentBottom > top) continue;
                if (top > bestTop)
                {
                    bestTop = top;
                    bestIndex = i;
                }
            }

            if (bestIndex < 0) return;

            var hit = _platforms[bestIndex];
            if (hit.Type == JumperPlatformType.Fragile)
            {
                _platforms.RemoveAt(bestIndex);
                Log(JumperEventType.FragileBroken, hit.Index);
                return;
            }

            VelocityY = _rules.JumpSpeed;
            PlayerPosition = PlayerPosition.WithY(bestTop + _rules.PlayerHalfHeight);
            Log(hit.Type == JumperPlatformType.Moving ? JumperEventType.BouncedOnMoving : JumperEventType.Bounced, hit.Index);
        }

        private void UpdateMaxHeightAndScore()
        {
            float bottom = PlayerPosition.Y - _rules.PlayerHalfHeight;
            if (bottom > MaxHeight) MaxHeight = bottom;
            Score = (int)(MaxHeight * _rules.ScorePerMeter);
        }

        /// <summary>镜头只升不降：目标为玩家处于屏幕下缘起 <see cref="JumperRules.CameraAnchor"/> 比例处。</summary>
        private void UpdateCamera()
        {
            float target = _rules.CameraTargetY(PlayerPosition.Y);
            if (target > CameraCenterY) CameraCenterY = target;
        }

        /// <summary>先按当前时间重排移动平台的横坐标（相位由生成序号确定），再处理玩家运动。</summary>
        private void RepositionMovingPlatforms()
        {
            for (int i = 0; i < _platforms.Count; i++)
            {
                var platform = _platforms[i];
                if (platform.Type != JumperPlatformType.Moving) continue;
                float x = _rules.MovingPlatformX(platform.BaseX, platform.Index, Time);
                _platforms[i] = new JumperPlatform(platform.Index, platform.Type, platform.Center.WithX(x), platform.BaseX);
            }
        }

        /// <summary>移除完全落到镜头下缘再低一个平台厚度处的平台。</summary>
        private void CullPlatforms()
        {
            float cullLine = CameraBottomY - _rules.PlatformThickness;
            for (int i = _platforms.Count - 1; i >= 0; i--)
            {
                float top = _platforms[i].Center.Y + _rules.PlatformThickness * 0.5f;
                if (top < cullLine) _platforms.RemoveAt(i);
            }
        }

        /// <summary>向上按间隔生成平台直到镜头上缘加前视余量；随机顺序固定为间距、种类、横坐标。</summary>
        private void GeneratePlatforms()
        {
            float ceiling = CameraTopY + _rules.LookAheadMargin;
            while (_nextSpawnY < ceiling)
            {
                // 先掷垂直间距、再掷种类、最后掷横坐标，随机序列顺序固定保证可复现。
                double gapSample = _random.NextDouble();
                float minGap = _rules.MinGapFor(_nextSpawnY);
                float gapRange = _rules.MaxGapFor(_nextSpawnY) - minGap;
                _nextSpawnY += minGap + (float)gapSample * gapRange;

                double typeSample = _random.NextDouble();
                var type = typeSample < _rules.FragileChance ? JumperPlatformType.Fragile
                    : typeSample < _rules.FragileChance + _rules.MovingChance ? JumperPlatformType.Moving
                    : JumperPlatformType.Normal;

                float xMin = type == JumperPlatformType.Moving ? _rules.MovingSpawnXMin : _rules.StaticSpawnXMin;
                float xMax = type == JumperPlatformType.Moving ? _rules.MovingSpawnXMax : _rules.StaticSpawnXMax;
                double xSample = _random.NextDouble();
                float baseX = xMin + (float)xSample * (xMax - xMin);
                // 移动平台出生即按相位公式定位，避免下一步才跳到往返轨迹上。
                float x = type == JumperPlatformType.Moving ? _rules.MovingPlatformX(baseX, _nextPlatformIndex, Time) : baseX;

                _platforms.Add(new JumperPlatform(_nextPlatformIndex++, type, new Vec2(x, _nextSpawnY), baseX));
            }
        }

        /// <summary>玩家底部低于历史最高高度减坠落窗口即结束。</summary>
        private bool CheckDeath()
        {
            float bottom = PlayerPosition.Y - _rules.PlayerHalfHeight;
            if (bottom >= MaxHeight - _rules.FallDeathWindow) return false;
            IsGameOver = true;
            Log(JumperEventType.FellOut);
            return true;
        }

        private void Log(JumperEventType type, int platformIndex = -1)
        {
            _events.Add(new JumperEventLog(StepIndex, type, platformIndex));
            if (_events.Count > MaxEventLogs) _events.RemoveRange(0, _events.Count - MaxEventLogs);
        }
    }
}
