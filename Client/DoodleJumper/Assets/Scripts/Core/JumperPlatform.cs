namespace Lyking.DoodleJumper.Core
{
    /// <summary>平台种类：普通平台永久存在，易碎平台踩上即碎，移动平台水平往返。</summary>
    public enum JumperPlatformType
    {
        /// <summary>普通平台：踩上即弹起，永久存在。</summary>
        Normal,

        /// <summary>易碎平台：下落踩上即碎裂消失，不弹起。</summary>
        Fragile,

        /// <summary>移动平台：水平往返，踩上照常弹起。</summary>
        Moving,
    }

    /// <summary>
    /// 一个平台的只读快照：<see cref="Index"/> 为生成序号（起始平台为 0，向上递增），
    /// <see cref="Center"/> 为当前中心（移动平台的 X 随时间变化），<see cref="BaseX"/> 为生成时基准横坐标，
    /// 宽厚尺寸由 <see cref="JumperRules"/> 统一提供。
    /// </summary>
    public readonly struct JumperPlatform
    {
        public JumperPlatform(int index, JumperPlatformType type, Vec2 center, float baseX)
        {
            Index = index;
            Type = type;
            Center = center;
            BaseX = baseX;
        }

        /// <summary>生成序号，从 0（起始平台）开始递增，同时决定移动平台的往返相位。</summary>
        public int Index { get; }

        public JumperPlatformType Type { get; }

        /// <summary>当前中心位置（Y 固定，X 仅移动平台随时间变化）。</summary>
        public Vec2 Center { get; }

        /// <summary>生成时的基准横坐标，移动平台绕它往返。</summary>
        public float BaseX { get; }

        public override string ToString()
        {
            string type = Type switch
            {
                JumperPlatformType.Fragile => "易碎",
                JumperPlatformType.Moving => "移动",
                _ => "普通",
            };
            return $"平台#{Index} {type} X={Center.X:0.00} Y={Center.Y:0.00}";
        }
    }
}
