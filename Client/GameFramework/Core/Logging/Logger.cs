using System;

namespace Lyking.GameFramework.Client.Core.Logging
{
    /// <summary>命名的日志入口。</summary>
    public interface ILogger
    {
        /// <summary>日志名，通常为模块或子系统名。</summary>
        string Name { get; }

        void Debug(string message);
        void Info(string message);
        void Warn(string message);
        void Error(string message, Exception? exception = null);
    }

    /// <summary>
    /// 默认日志实现：按阈值过滤后格式化为 "[级别][名称] 消息" 写入输出端。
    /// </summary>
    public sealed class Logger : ILogger
    {
        private readonly ILogSink _sink;

        public Logger(string name, ILogSink sink, LogLevel minLevel = LogLevel.Debug)
        {
            Name = string.IsNullOrWhiteSpace(name) ? throw new ArgumentException("日志名不能为空。", nameof(name)) : name;
            _sink = sink ?? throw new ArgumentNullException(nameof(sink));
            MinLevel = minLevel;
        }

        public string Name { get; }

        public LogLevel MinLevel { get; set; }

        public void Debug(string message) => Write(LogLevel.Debug, message, null);

        public void Info(string message) => Write(LogLevel.Info, message, null);

        public void Warn(string message) => Write(LogLevel.Warn, message, null);

        public void Error(string message, Exception? exception = null) => Write(LogLevel.Error, message, exception);

        private void Write(LogLevel level, string message, Exception? exception)
        {
            if (level < MinLevel)
            {
                return;
            }

            string text = $"[{level.ToString().ToUpperInvariant()}][{Name}] {message}";
            if (exception != null)
            {
                text = text + Environment.NewLine + exception;
            }

            _sink.Write(level, text);
        }
    }
}
