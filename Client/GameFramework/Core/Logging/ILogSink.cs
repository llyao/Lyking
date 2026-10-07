namespace Lyking.GameFramework.Client.Core.Logging
{
    /// <summary>
    /// 日志输出端。核心层不感知控制台或 Unity 调试通道，
    /// 由运行环境提供实现（如 UnityLogSink、控制台输出、文件输出）。
    /// </summary>
    public interface ILogSink
    {
        void Write(LogLevel level, string message);
    }
}
