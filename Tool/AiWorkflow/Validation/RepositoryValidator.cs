using Lyking.AiWorkflow.Adapters;
using Lyking.AiWorkflow.Configuration;
using Lyking.AiWorkflow.Core;

namespace Lyking.AiWorkflow.Validation;

public sealed class RepositoryValidator(string root, WorkflowConfig config)
{
    public ValidationReport Validate(string? changeDirectory = null, string? transition = null)
    {
        var issues = new List<ValidationIssue>();
        var documents = new List<string> { "AGENTS.md", "CLAUDE.md", "GEMINI.md", "CONTEXT-MAP.md", "docs/ai/workflow.md", "docs/ai/authority.md", "docs/ai/verification.md", "docs/ai/platforms.md" };
        documents.AddRange(config.Areas.Select(a => a.Context));
        documents.AddRange(new[] { "change", "evidence", "design", "plan", "tasks", "review" }.Select(n => $"docs/ai/templates/{n}.md"));
        foreach (var name in new[] { "design", "implement", "debug", "verify", "review", "deliver" })
            documents.Add($".agents/skills/lyking-{name}/SKILL.md");
        foreach (var name in new[] { "explorer", "implementer", "reviewer" })
        {
            documents.Add($".agents/roles/{name}/instructions.md");
            if (!File.Exists(PathPolicy.ResolveWithinRoot(root, $".agents/roles/{name}/role.json")))
                issues.Add(new("role.missing", IssueSeverity.Error, $"缺少共享角色：{name}"));
        }
        foreach (var document in documents)
        {
            try
            {
                var path = PathPolicy.ResolveWithinRoot(root, document);
                if (!File.Exists(path)) issues.Add(new("document.missing", IssueSeverity.Error, "缺少必需文档。", document));
                else issues.AddRange(new MarkdownValidator(root).Validate(path).Issues);
            }
            catch (WorkflowException ex) { issues.Add(new(ex.Code, IssueSeverity.Error, ex.Message, document)); }
        }
        try
        {
            var adapters = new AdapterSynchronizer(root, config).Check();
            issues.AddRange(adapters.Issues.Select(i => new ValidationIssue(i.Code, IssueSeverity.Error, i.Message)));
        }
        catch (WorkflowException ex) { issues.Add(new(ex.Code, IssueSeverity.Error, ex.Message)); }
        if (changeDirectory is not null) issues.AddRange(new ChangeValidator(root, config).ValidateChange(changeDirectory, transition).Issues);
        return ValidationReport.Sorted(issues);
    }
}
