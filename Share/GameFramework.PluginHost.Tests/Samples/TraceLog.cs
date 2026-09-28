using System;
using System.Collections.Generic;

namespace Lyking.GameFramework.PluginHost.Tests.Samples
{
    /// <summary>测试用生命周期轨迹。记录模块与中间件的调用顺序。</summary>
    public sealed class TraceLog
    {
        private readonly object _gate = new object();
        private readonly List<string> _entries = new List<string>();

        public void Add(string entry)
        {
            lock (_gate)
            {
                _entries.Add(entry);
            }
        }

        public IReadOnlyList<string> Snapshot()
        {
            lock (_gate)
            {
                return _entries.ToArray();
            }
        }
    }
}
