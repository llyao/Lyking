using System;

namespace Lyking.WhackAMole.Core
{
    /// <summary>
    /// 打地鼠规则常量：3x3 共 9 个地洞、30 秒限时计分、连击加成与炸弹惩罚。
    /// 地鼠停留时长随剩余时间线性缩短，由 Core 提供给表现层与测试共同引用，保证数值一致。
    /// </summary>
    public static class WhackAMoleRules
    {
        /// <summary>地洞行数。</summary>
        public const int HoleRows = 3;

        /// <summary>地洞列数。</summary>
        public const int HoleColumns = 3;

        /// <summary>地洞总数：行数 × 列数。</summary>
        public const int HoleCount = HoleRows * HoleColumns;

        /// <summary>一局时长（秒），倒计时归零即结束。</summary>
        public const float RoundSeconds = 30f;

        /// <summary>普通地鼠基础分。</summary>
        public const int NormalMoleScore = 100;

        /// <summary>连击加成步长：每次命中按当前连击数加 20 分。</summary>
        public const int ComboBonusStep = 20;

        /// <summary>连击加成上限。</summary>
        public const int MaxComboBonus = 100;

        /// <summary>炸弹地鼠扣分，总分夹 0 下限。</summary>
        public const int BombMolePenalty = 200;

        /// <summary>地鼠冒头停留基础时长（秒），对局开始时最长。</summary>
        public const float BaseUpSeconds = 1.2f;

        /// <summary>地鼠冒头停留时长下限（秒），接近终局时最短。</summary>
        public const float MinUpSeconds = 0.5f;

        /// <summary>地鼠冒头最小间隔（秒）。</summary>
        public const float MinSpawnInterval = 0.4f;

        /// <summary>地鼠冒头最大间隔（秒）。</summary>
        public const float MaxSpawnInterval = 1.0f;

        /// <summary>冒头地鼠为炸弹的概率。</summary>
        public const float BombChance = 0.2f;

        /// <summary>事件日志封顶条数，超出后从最旧开始裁剪。</summary>
        public const int MaxEventLogs = 128;

        /// <summary>
        /// 连击 <paramref name="combo"/> 次的加成分：+20×连击数，上限 +100。
        /// 连击数从首次命中记 1，纯确定性函数。
        /// </summary>
        public static int ComboBonusFor(int combo) => Math.Min(MaxComboBonus, ComboBonusStep * combo);

        /// <summary>
        /// 剩余 <paramref name="remainingSeconds"/> 秒时地鼠的停留时长：随剩余时间从 1.2 秒线性缩短至下限 0.5 秒。
        /// 纯确定性函数。
        /// </summary>
        public static float StaySecondsFor(float remainingSeconds) =>
            MinUpSeconds + (BaseUpSeconds - MinUpSeconds) * Math.Clamp(remainingSeconds / RoundSeconds, 0f, 1f);
    }
}
