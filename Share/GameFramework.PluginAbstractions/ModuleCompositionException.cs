using System;
using System.Collections.Generic;
using System.Linq;
using Lyking.GameFramework.PluginAbstractions.Planning;

namespace Lyking.GameFramework.PluginAbstractions
{
    /// <summary>
    /// 组合失败异常：携带全部诊断。启动前失败必须结构化可读，
    /// 不允许靠堆栈推断模块组合问题。
    /// </summary>
    public class ModuleCompositionException : Exception
    {
        public ModuleCompositionException(IReadOnlyList<PlanDiagnostic> diagnostics)
            : base(Format(diagnostics))
        {
            Diagnostics = diagnostics;
        }

        public IReadOnlyList<PlanDiagnostic> Diagnostics { get; }

        private static string Format(IReadOnlyList<PlanDiagnostic> diagnostics) =>
            $"模块组合失败，共 {diagnostics.Count} 条诊断：{Environment.NewLine}"
            + string.Join(Environment.NewLine, diagnostics.Select(d => "  - " + d));
    }
}
