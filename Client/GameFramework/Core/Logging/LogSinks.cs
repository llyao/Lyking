using System;

namespace Lyking.GameFramework.Client.Core.Logging
{
    /// <summary>
    /// 把写入委托包装成输出端，便于测试与无头环境（控制台、内存列表）使用。
    /// </summary>
    public sealed class DelegateLogSink : ILogSink
    {
        private readonly Action<LogLevel, string> _writer;

        public DelegateLogSink(Action<LogLevel, string> writer)
        {
            _writer = writer ?? throw new ArgumentNullException(nameof(writer));
        }

        public void Write(LogLevel level, string message)
        {
            _writer(level, message);
        }
    }

    /// <summary>
    /// 复合输出端：把一条日志扇出到多个输出端。单个输出端异常被跳过，不阻断其他输出端。
    /// </summary>
    public sealed class CompositeLogSink : ILogSink
    {
        private readonly System.Collections.Generic.List<ILogSink> _sinks = new System.Collections.Generic.List<ILogSink>();

        public void Add(ILogSink sink)
        {
            if (sink is null)
            {
                throw new ArgumentNullException(nameof(sink));
            }

            _sinks.Add(sink);
        }

        public void Write(LogLevel level, string message)
        {
            for (int i = 0; i < _sinks.Count; i++)
            {
                try
                {
                    _sinks[i].Write(level, message);
                }
                catch
                {
                    // 输出端故障不允许打断游戏帧循环；个别输出端异常直接跳过。
                }
            }
        }
    }
}
