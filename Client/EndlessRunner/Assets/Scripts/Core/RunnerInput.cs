namespace Lyking.EndlessRunner.Core
{
    /// <summary>
    /// 一帧玩家输入的纯数据快照：<see cref="JumpPressed"/> 为按下沿（只作用于一个子步），
    /// <see cref="FastFallHeld"/> 为持续按住（每个子步都生效）。
    /// </summary>
    public readonly struct RunnerInput
    {
        public RunnerInput(bool jumpPressed, bool fastFallHeld)
        {
            JumpPressed = jumpPressed;
            FastFallHeld = fastFallHeld;
        }

        /// <summary>本帧是否按下跳跃（按下沿触发地面跳或二段跳）。</summary>
        public bool JumpPressed { get; }

        /// <summary>本帧是否持续按住快落键（空中生效，地面忽略）。</summary>
        public bool FastFallHeld { get; }

        /// <summary>无输入的默认快照。</summary>
        public static RunnerInput None => default;
    }
}
