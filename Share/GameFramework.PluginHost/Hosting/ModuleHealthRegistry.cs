using System;
using System.Collections.Generic;
using System.Threading;
using Lyking.GameFramework.PluginAbstractions.Hosting;
using Lyking.GameFramework.PluginAbstractions.Modules;

namespace Lyking.GameFramework.PluginHost.Hosting
{
    /// <summary>线程安全的模块健康登记表。宿主持有，模块通过 IModuleContext 上报。</summary>
    public sealed class ModuleHealthRegistry
    {
        private sealed class Entry
        {
            public ModuleHealthState State = ModuleHealthState.NotStarted;
            public string? Message;
            public TimeSpan? StartElapsed;
        }

        private readonly object _gate = new object();
        private readonly Dictionary<string, Entry> _entries = new Dictionary<string, Entry>(StringComparer.Ordinal);

        public void Register(ModuleId moduleId)
        {
            lock (_gate)
            {
                _entries[moduleId.Value] = new Entry();
            }
        }

        public void SetState(ModuleId moduleId, ModuleHealthState state, string? message = null)
        {
            lock (_gate)
            {
                _entries[moduleId.Value].State = state;
                _entries[moduleId.Value].Message = message;
            }
        }

        public void SetReady(ModuleId moduleId, TimeSpan startElapsed, string? message = null)
        {
            lock (_gate)
            {
                var entry = _entries[moduleId.Value];
                entry.State = ModuleHealthState.Ready;
                entry.Message = message;
                entry.StartElapsed = startElapsed;
            }
        }

        public IReadOnlyList<ModuleHealthReport> Snapshot(IReadOnlyList<PlannedModuleRef> order)
        {
            lock (_gate)
            {
                var reports = new List<ModuleHealthReport>(order.Count);
                foreach (var entryRef in order)
                {
                    _entries.TryGetValue(entryRef.Id, out var entry);
                    reports.Add(new ModuleHealthReport(
                        entryRef.Id,
                        entry?.State ?? ModuleHealthState.NotStarted,
                        entry?.Message,
                        entry?.StartElapsed));
                }

                return reports;
            }
        }

        /// <summary>
        /// 健康报告的顺序 id 引用：只承载"按哪个 id、什么顺序"输出报告。
        /// 与 Planning/PlannedModule（拓扑计划项）无关；两者同名前缀但职责不同。
        /// </summary>
        public readonly struct PlannedModuleRef
        {
            public PlannedModuleRef(string id)
            {
                Id = id;
            }

            public string Id { get; }
        }
    }
}
