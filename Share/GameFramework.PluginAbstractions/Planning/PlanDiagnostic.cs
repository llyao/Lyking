using System;

namespace Lyking.GameFramework.PluginAbstractions.Planning
{
    /// <summary>组合校验与计划生成的结构化诊断码。</summary>
    public enum PlanDiagnosticCode
    {
        InvalidDescriptor = 0,
        DuplicateModuleId = 1,
        MissingDependency = 2,
        IncompatibleVersion = 3,
        DependencyCycle = 4,
        DuplicateCapability = 5,
        StateNamespaceConflict = 6,
        DuplicateServiceRegistration = 7,
    }

    /// <summary>一条诊断。组合失败时一次性收集全部诊断，便于一次修复。</summary>
    public sealed class PlanDiagnostic
    {
        public PlanDiagnostic(PlanDiagnosticCode code, string message, string? moduleId = null)
        {
            Code = code;
            Message = message ?? throw new ArgumentNullException(nameof(message));
            ModuleId = moduleId;
        }

        public PlanDiagnosticCode Code { get; }

        public string Message { get; }

        public string? ModuleId { get; }

        public override string ToString() =>
            ModuleId is null ? $"[{Code}] {Message}" : $"[{Code}] {ModuleId}: {Message}";
    }
}
