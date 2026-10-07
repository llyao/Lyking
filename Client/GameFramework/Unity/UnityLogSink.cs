using Lyking.GameFramework.Client.Core.Logging;
using UnityEngine;

namespace Lyking.GameFramework.Client.Unity
{
    /// <summary>把 Unity 调试通道包装成框架日志输出端。</summary>
    public sealed class UnityLogSink : ILogSink
    {
        public void Write(LogLevel level, string message)
        {
            switch (level)
            {
                case LogLevel.Debug:
                case LogLevel.Info:
                    Debug.Log(message);
                    break;
                case LogLevel.Warn:
                    Debug.LogWarning(message);
                    break;
                case LogLevel.Error:
                    Debug.LogError(message);
                    break;
                default:
                    Debug.Log(message);
                    break;
            }
        }
    }
}
