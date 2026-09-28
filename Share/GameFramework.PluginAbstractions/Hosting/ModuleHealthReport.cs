namespace Lyking.GameFramework.PluginAbstractions.Hosting
{
    /// <summary>模块生命周期健康状态。Degraded 是显式降级，不是隐藏失败。</summary>
    public enum ModuleHealthState
    {
        NotStarted = 0,
        Starting = 1,
        Ready = 2,
        Degraded = 3,
        Draining = 4,
        Stopped = 5,
        Failed = 6,
    }

    /// <summary>单个模块的健康快照。</summary>
    public sealed class ModuleHealthReport
    {
        public ModuleHealthReport(
            string moduleId,
            ModuleHealthState state,
            string? message,
            System.TimeSpan? startElapsed)
        {
            ModuleId = moduleId;
            State = state;
            Message = message;
            StartElapsed = startElapsed;
        }

        public string ModuleId { get; }

        public ModuleHealthState State { get; }

        public string? Message { get; }

        public System.TimeSpan? StartElapsed { get; }

        public override string ToString() =>
            $"{ModuleId}={State}{(Message is null ? "" : $"({Message})")}";
    }
}
