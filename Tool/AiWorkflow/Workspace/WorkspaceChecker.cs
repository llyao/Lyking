using Lyking.AiWorkflow.Changes;
using Lyking.AiWorkflow.Core;
using Lyking.AiWorkflow.Git;
using Lyking.AiWorkflow.Validation;

namespace Lyking.AiWorkflow.Workspace;

public sealed record WorkspaceReport(string? Branch, IReadOnlyList<GitStatusEntry> Changes, IReadOnlyList<ValidationIssue> Issues)
{
    public bool CanProceed => Issues.All(i => i.Severity != IssueSeverity.Error);
}

public sealed class WorkspaceChecker(string root)
{
    public WorkspaceReport Check(string? changeDirectory, IEnumerable<string> additionalAllowedPaths)
    {
        var issues = new List<ValidationIssue>();
        var allowed = additionalAllowedPaths.Select(p => PathPolicy.Relative(root, p)).ToList();
        if (changeDirectory is not null)
        {
            var change = PathPolicy.Relative(root, changeDirectory);
            var document = PathPolicy.ResolveWithinRoot(root, change + "/change.md");
            var metadata = ChangeDocumentParser.Parse(File.ReadAllText(document));
            allowed.AddRange(metadata.Paths.Select(p => PathPolicy.Relative(root, p)));
            if (allowed.Count == 0) issues.Add(new("workspace.scope-missing", IssueSeverity.Error, "变更未声明 paths，且没有显式允许路径。"));
            allowed.Add(change);
        }
        if (allowed.Count == 0) issues.Add(new("workspace.scope-missing", IssueSeverity.Error, "必须声明允许路径。"));
        var git = new GitClient(root);
        var branchResult = git.Run("symbolic-ref", "--quiet", "--short", "HEAD");
        var branch = branchResult.ExitCode == 0 ? branchResult.Output.Trim() : null;
        if (branch is null || branch is "main" or "master") issues.Add(new("workspace.branch", IssueSeverity.Error, "游离 HEAD 或受保护分支不能实施本变更。"));
        foreach (var operation in new[] { "MERGE_HEAD", "CHERRY_PICK_HEAD", "REVERT_HEAD", "rebase-merge", "rebase-apply" })
        {
            var result = git.Run("rev-parse", "--git-path", operation);
            if (result.ExitCode != 0) throw new WorkflowException("无法读取 Git 工作区元数据。", "git.invalid");
            var path = Path.GetFullPath(result.Output.Trim(), root);
            if (File.Exists(path) || Directory.Exists(path)) issues.Add(new("workspace.operation", IssueSeverity.Error, $"Git 操作尚未结束：{operation}"));
        }
        var status = git.Run("status", "--porcelain=v1", "-z", "--untracked-files=all");
        if (status.ExitCode != 0) throw new WorkflowException("无法读取 Git 工作区状态。", "git.invalid");
        var entries = GitClient.ParseStatus(status.Output);
        foreach (var entry in entries)
        {
            var paths = entry.OriginalPath is null ? new[] { entry.Path } : new[] { entry.Path, entry.OriginalPath };
            if (entry.IndexStatus == 'U' || entry.WorktreeStatus == 'U' || new[] { "AA", "DD" }.Contains($"{entry.IndexStatus}{entry.WorktreeStatus}"))
                issues.Add(new("workspace.conflict", IssueSeverity.Error, "文件存在未解决冲突。", entry.Path));
            foreach (var path in paths.Where(p => !allowed.Any(a => a == "." || PathPolicy.Contains(a, p))))
            {
                var staged = entry.IndexStatus is not (' ' or '?' or '!');
                issues.Add(new(staged ? "workspace.staged-outside-scope" : "workspace.unstaged-outside-scope",
                    staged ? IssueSeverity.Error : IssueSeverity.Warning, staged ? "范围外文件已暂存。" : "范围外文件保持原状。", path));
            }
        }
        return new(branch, entries, ValidationReport.Sorted(issues).Issues);
    }
}
