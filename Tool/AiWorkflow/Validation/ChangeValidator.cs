using System.Text.RegularExpressions;
using Lyking.AiWorkflow.Changes;
using Lyking.AiWorkflow.Configuration;
using Lyking.AiWorkflow.Core;
using Lyking.AiWorkflow.Routing;

namespace Lyking.AiWorkflow.Validation;

public sealed class ChangeValidator(string root, WorkflowConfig config)
{
    public ValidationReport ValidateChange(string changeDirectory, string? transition)
    {
        var issues = new List<ValidationIssue>();
        void Add(string code, string message, string? path = null) => issues.Add(new(code, IssueSeverity.Error, message, path));
        try
        {
            var relative = PathPolicy.Relative(root, Path.IsPathFullyQualified(changeDirectory) ? Path.GetRelativePath(root, changeDirectory) : changeDirectory);
            var directory = PathPolicy.ResolveWithinRoot(root, relative);
            var document = PathPolicy.ResolveWithinRoot(root, relative + "/change.md");
            if (!File.Exists(document)) return new([new("artifact.missing", IssueSeverity.Error, "缺少 change.md。", "change.md")]);
            var text = StrictUtf8Reader.Read(document);
            var metadata = ChangeDocumentParser.Parse(text);
            if (metadata.Id != Path.GetFileName(directory)) Add("change.id-mismatch", "变更目录名与编号不一致。", "change.md");
            if (metadata.Areas.Except(config.Areas.Select(a => a.Name)).Any()) Add("change.unknown-area", "变更包含未知领域。", "change.md");
            foreach (var path in metadata.Paths) PathPolicy.ResolveWithinRoot(root, path);
            var route = new Router(root, config).Route("", metadata.Paths);
            if (metadata.Profile != WorkProfile.Controlled && (route.RiskSignals.Count > 0 || metadata.Areas.Count >= 3))
                Add("profile.risk-required", "硬路径或多领域风险要求受控目标。", "change.md");
            var required = config.Profiles[metadata.Profile.ToString().ToLowerInvariant()];
            foreach (var file in required)
                if (!File.Exists(PathPolicy.ResolveWithinRoot(root, relative + "/" + file))) Add("artifact.missing", $"缺少工件：{file}", file);
            var reviewRequired = metadata.Profile == WorkProfile.Controlled || metadata.RiskSignals.Count > 0 || route.RiskSignals.Count > 0;
            if (reviewRequired && !File.Exists(Path.Combine(directory, "review.md"))) Add("review.missing", "本变更要求独立审查记录。", "review.md");
            var markdown = new MarkdownValidator(root);
            issues.AddRange(markdown.Validate(document, ["解决什么问题", "完成后应看到什么", "验收方式", "范围与非目标", "风险与回滚"]).Issues);
            foreach (var file in required.Concat(reviewRequired ? new[] { "review.md" } : []).Distinct().Where(f => f != "change.md"))
            {
                var path = PathPolicy.ResolveWithinRoot(root, relative + "/" + file);
                if (File.Exists(path)) issues.AddRange(markdown.Validate(path).Issues);
            }
            if (transition is not null)
            {
                var states = transition.Split(':');
                if (states.Length != 2 || !WorkflowStateMachine.CanTransition(states[0], states[1])) Add("state.invalid-transition", "状态转换不在允许边中。", "change.md");
                if (states.Length != 2 || !ChangeDocumentParser.Statuses.TryGetValue(states[1], out var target) || target != metadata.Status)
                    Add("state.target-mismatch", "元数据状态必须与转换目标一致。", "change.md");
            }
            var archived = PathPolicy.Contains(PathPolicy.Relative(root, config.Changes.Archive), relative);
            if (metadata.Status == WorkflowStatus.Archived && !archived || metadata.Status != WorkflowStatus.Archived && archived)
                Add("state.archive-location", "状态与活动或归档目录不一致。", "change.md");
            if (!archived && !PathPolicy.Contains(PathPolicy.Relative(root, config.Changes.Active), relative))
                Add("change.location", "变更必须位于配置的活动或归档目录。", "change.md");
            if (metadata.Status is WorkflowStatus.Verified or WorkflowStatus.Archived)
            {
                var evidence = ReadOptional("evidence.md");
                foreach (var section in metadata.Areas.Any(a => a is "Art" or "Client") ? new[] { "验证命令", "验证结果", "人工验收" } : new[] { "验证命令", "验证结果" })
                    if (string.IsNullOrWhiteSpace(MarkdownValidator.Section(evidence, section))) Add("evidence.empty-section", $"证据章节缺少正文：{section}", "evidence.md");
                if (Regex.IsMatch(ReadOptional("tasks.md"), @"(?m)^\s*[-*+]\s+\[ \]")) Add("tasks.incomplete", "任务清单包含未完成项目。", "tasks.md");
                if (reviewRequired)
                {
                    var conclusion = MarkdownValidator.Section(ReadOptional("review.md"), "结论");
                    var unresolved = conclusion.Replace("未发现阻塞问题", "", StringComparison.Ordinal).Replace("无阻塞问题", "", StringComparison.Ordinal);
                    if (string.IsNullOrWhiteSpace(conclusion) || !conclusion.Contains("通过", StringComparison.Ordinal) ||
                        unresolved.Contains("阻塞", StringComparison.Ordinal) || new[] { "未通过", "尚未", "待审查" }.Any(conclusion.Contains))
                        Add("review.blocked", "独立审查没有明确通过或仍有阻塞。", "review.md");
                }
            }
            string ReadOptional(string file)
            {
                var path = PathPolicy.ResolveWithinRoot(root, relative + "/" + file);
                return File.Exists(path) ? StrictUtf8Reader.Read(path) : "";
            }
        }
        catch (WorkflowException ex) { Add(ex.Code, ex.Message); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Add("file.unreadable", "变更文件无法读取。"); }
        return ValidationReport.Sorted(issues);
    }
}
