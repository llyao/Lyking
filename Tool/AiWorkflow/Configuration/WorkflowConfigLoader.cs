using System.Text.Json;
using Lyking.AiWorkflow.Core;
using Lyking.AiWorkflow.Validation;

namespace Lyking.AiWorkflow.Configuration;

public static class WorkflowConfigLoader
{
    public static WorkflowConfig Load(string repositoryRoot)
    {
        var path = PathPolicy.ResolveWithinRoot(repositoryRoot, ".lyking-ai.json");
        if (!File.Exists(path)) throw new WorkflowException("缺少 .lyking-ai.json 配置。", "config.missing");
        WorkflowConfig? config;
        try { config = JsonSerializer.Deserialize<WorkflowConfig>(StrictUtf8Reader.Read(path), new JsonSerializerOptions { PropertyNameCaseInsensitive = true, AllowTrailingCommas = true }); }
        catch (JsonException) { throw new WorkflowException("配置 JSON 无效或必需字段缺失。", "config.json"); }
        if (config is null || string.IsNullOrWhiteSpace(config.WorkflowVersion) || config.Changes is null || config.Adapters is null ||
            config.RiskRules is null || config.ExcludedPaths is null || config.Profiles is null)
            throw new WorkflowException("配置必需字段不能为空。", "config.empty");
        if (config.SchemaVersion != 1) throw new WorkflowException($"不支持的配置版本 {config.SchemaVersion}。", "config.version");
        if (config.Areas is null || config.Areas.Count != 5) throw new WorkflowException("配置必须包含五个领域。", "config.areas");
        if (config.Areas.Any(a => a is null || string.IsNullOrWhiteSpace(a.Name) || a.Keywords is null || a.Keywords.Any(string.IsNullOrWhiteSpace)) ||
            !new[] { "Art", "Client", "Server", "Share", "Tool" }.All(n => config.Areas.Any(a => a.Name == n)))
            throw new WorkflowException("配置领域名称或关键词无效。", "config.areas");
        if (config.Areas.Select(a => a.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != config.Areas.Count) throw new WorkflowException("领域名称不能重复。", "config.areas");
        if (config.Areas.Select(a => a.Root).Distinct(StringComparer.OrdinalIgnoreCase).Count() != config.Areas.Count) throw new WorkflowException("领域根路径不能重复。", "config.areas");
        foreach (var area in config.Areas)
        {
            var areaRoot = PathPolicy.ResolveWithinRoot(repositoryRoot, area.Root);
            if (!Directory.Exists(areaRoot) || PathPolicy.Relative(repositoryRoot, area.Root) != area.Name)
                throw new WorkflowException("领域根目录必须与名称对应且存在。", "config.areas");
            PathPolicy.ResolveWithinRoot(repositoryRoot, area.Context);
        }
        if (config.Profiles is null || !config.Profiles.Keys.Any(k => k.Equals("direct", StringComparison.OrdinalIgnoreCase)) || !config.Profiles.Keys.Any(k => k.Equals("standard", StringComparison.OrdinalIgnoreCase)) || !config.Profiles.Keys.Any(k => k.Equals("controlled", StringComparison.OrdinalIgnoreCase))) throw new WorkflowException("配置缺少任务档位。", "config.profiles");
        PathPolicy.ResolveWithinRoot(repositoryRoot, config.Changes.Active); PathPolicy.ResolveWithinRoot(repositoryRoot, config.Changes.Archive);
        PathPolicy.ResolveWithinRoot(repositoryRoot, config.Adapters.ClaudeSkills); PathPolicy.ResolveWithinRoot(repositoryRoot, config.Adapters.CodexAgents); PathPolicy.ResolveWithinRoot(repositoryRoot, config.Adapters.ClaudeAgents); PathPolicy.ResolveWithinRoot(repositoryRoot, config.Adapters.GeminiAgents); PathPolicy.ResolveWithinRoot(repositoryRoot, config.Adapters.Manifest);
        foreach (var pathValue in config.ExcludedPaths) PathPolicy.ResolveWithinRoot(repositoryRoot, pathValue);
        var requiredProfiles = new Dictionary<string, string[]>
        {
            ["direct"] = [],
            ["standard"] = ["change.md", "evidence.md"],
            ["controlled"] = ["change.md", "evidence.md", "design.md", "plan.md", "tasks.md", "review.md"]
        };
        foreach (var (profile, required) in requiredProfiles)
        {
            if (!config.Profiles.TryGetValue(profile, out var artifacts) || artifacts is null || required.Except(artifacts).Any())
                throw new WorkflowException("档位必需工件不能省略。", "config.profiles");
            foreach (var artifact in artifacts)
                if (string.IsNullOrWhiteSpace(artifact) || artifact.IndexOfAny(['/', '\\', ':']) >= 0 || !artifact.EndsWith(".md", StringComparison.Ordinal))
                    throw new WorkflowException("工件必须是当前变更目录内的 Markdown 文件。", "config.profiles");
        }
        if (config.RiskRules.Any(r => r is null || string.IsNullOrWhiteSpace(r.Id) || string.IsNullOrWhiteSpace(r.Description) ||
                r.PathPrefixes is null || r.PathSegments is null || r.PathPrefixes.Any(string.IsNullOrWhiteSpace) || r.PathSegments.Any(string.IsNullOrWhiteSpace)) ||
            config.RiskRules.Select(r => r.Id).Distinct().Count() != config.RiskRules.Count)
            throw new WorkflowException("风险规则格式无效或编号重复。", "config.risks");
        var mandatory = new Dictionary<string, string[]>
        {
            ["save-data"] = ["Save", "Persistence"],
            ["network-protocol"] = ["Protocol", "Network", "Rpc"],
            ["security"] = ["Security", "Auth", "Permission"]
        };
        foreach (var (id, segments) in mandatory)
            if (!config.RiskRules.Any(r => r.Id == id && !segments.Except(r.PathSegments, StringComparer.OrdinalIgnoreCase).Any()))
                throw new WorkflowException("配置不能移除第一版的硬路径风险规则。", "config.risks");
        if (!config.RiskRules.Any(r => r.Id == "shared-contract" && r.PathPrefixes.Any(p => p.TrimEnd('/') == "Share")))
            throw new WorkflowException("配置不能移除共享契约风险规则。", "config.risks");
        foreach (var rule in config.RiskRules)
        {
            foreach (var prefix in rule.PathPrefixes) PathPolicy.ResolveWithinRoot(repositoryRoot, prefix);
            if (rule.PathSegments.Any(s => string.IsNullOrWhiteSpace(s) || s.IndexOfAny(['/', '\\', ':']) >= 0))
                throw new WorkflowException("风险路径段无效。", "config.risks");
        }
        var outputs = new[] { config.Adapters.ClaudeSkills, config.Adapters.CodexAgents, config.Adapters.ClaudeAgents, config.Adapters.GeminiAgents };
        var normalizedOutputs = outputs.Select(p => PathPolicy.Relative(repositoryRoot, p)).ToArray();
        var manifestPath = PathPolicy.Relative(repositoryRoot, config.Adapters.Manifest);
        if (new[] { ".git", ".worktrees", ".agents" }.Any(p => PathPolicy.Contains(p, manifestPath)))
            throw new WorkflowException("适配清单不能位于 Git 元数据、工作树池或真源目录。", "config.adapters");
        if (normalizedOutputs.Any(p => p == "." || PathPolicy.Contains(".agents", p) || PathPolicy.Contains(".git", p)) ||
            normalizedOutputs.Where((p, i) => normalizedOutputs.Where((_, j) => j != i).Any(q => PathPolicy.Contains(p, q))).Any())
            throw new WorkflowException("适配目录不能覆盖真源或相互重叠。", "config.adapters");
        if (PathPolicy.Contains(PathPolicy.Relative(repositoryRoot, config.Changes.Active), PathPolicy.Relative(repositoryRoot, config.Changes.Archive)) ||
            PathPolicy.Contains(PathPolicy.Relative(repositoryRoot, config.Changes.Archive), PathPolicy.Relative(repositoryRoot, config.Changes.Active)))
            throw new WorkflowException("活动与归档目录不能重叠。", "config.changes");
        return config;
    }
}
