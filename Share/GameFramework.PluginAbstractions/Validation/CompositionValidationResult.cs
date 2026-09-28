using System.Collections.Generic;
using Lyking.GameFramework.PluginAbstractions.Planning;
using Lyking.GameFramework.PluginAbstractions.Registrar;

namespace Lyking.GameFramework.PluginAbstractions.Validation
{
    /// <summary>组合校验结果：全部诊断 + 是否可用。诊断只收集，不抛出。</summary>
    public sealed class CompositionValidationResult
    {
        public static readonly CompositionValidationResult Success =
            new CompositionValidationResult(new PlanDiagnostic[0]);

        public CompositionValidationResult(IReadOnlyList<PlanDiagnostic> diagnostics)
        {
            Diagnostics = diagnostics;
        }

        public IReadOnlyList<PlanDiagnostic> Diagnostics { get; }

        public bool IsValid => Diagnostics.Count == 0;
    }
}
