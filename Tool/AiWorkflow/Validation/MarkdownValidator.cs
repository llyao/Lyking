using Lyking.AiWorkflow.Core;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace Lyking.AiWorkflow.Validation;

public sealed class MarkdownValidator(string root)
{
    public ValidationReport Validate(string path, IEnumerable<string>? requiredSections = null)
    {
        var issues = new List<ValidationIssue>();
        try
        {
            var absolute = PathPolicy.ResolveWithinRoot(root, Path.IsPathFullyQualified(path) ? Path.GetRelativePath(root, path) : path);
            var text = StrictUtf8Reader.Read(absolute);
            var tree = Markdown.Parse(text);
            foreach (var heading in requiredSections ?? [])
                if (string.IsNullOrWhiteSpace(Section(text, heading))) issues.Add(new("markdown.empty-section", IssueSeverity.Error, $"章节缺少正文：{heading}", path));
            foreach (var link in tree.Descendants<LinkInline>())
            {
                var url = link.Url;
                if (string.IsNullOrWhiteSpace(url) || url.StartsWith('#')) continue;
                if (Uri.TryCreate(url, UriKind.Absolute, out var remote) && remote.Scheme is "http" or "https" or "mailto") continue;
                var target = Uri.UnescapeDataString(url.Split('#')[0]);
                if (string.IsNullOrWhiteSpace(target)) continue;
                var directory = Path.GetRelativePath(root, Path.GetDirectoryName(absolute)!);
                if (Path.IsPathRooted(target) || target.Contains(':')) throw new WorkflowException("文档链接必须使用仓库内相对路径。", "link.unsafe");
                var resolved = PathPolicy.ResolveWithinRoot(root, Path.Combine(directory, target));
                if (!File.Exists(resolved) && !Directory.Exists(resolved)) issues.Add(new("link.missing", IssueSeverity.Error, $"本地链接目标不存在：{url}", path));
            }
        }
        catch (WorkflowException ex) { issues.Add(new(ex.Code, IssueSeverity.Error, ex.Message, path)); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { issues.Add(new("file.unreadable", IssueSeverity.Error, "文档无法读取。", path)); }
        return ValidationReport.Sorted(issues);
    }

    public static string Section(string markdown, string title)
    {
        var tree = Markdown.Parse(markdown);
        var heading = tree.OfType<HeadingBlock>().FirstOrDefault(h => h.Level == 2 && HeadingText(h) == title);
        if (heading is null) return "";
        var content = new List<string>();
        foreach (var block in tree.SkipWhile(b => b != heading).Skip(1))
        {
            if (block is HeadingBlock h && h.Level <= 2) break;
            if (block is HtmlBlock or HeadingBlock) continue;
            var value = markdown.Substring(block.Span.Start, block.Span.Length).Trim();
            if (value.Length > 0) content.Add(value);
        }
        return string.Join("\n", content);
    }
    private static string HeadingText(HeadingBlock heading) => heading.Inline is null ? "" : string.Concat(heading.Inline.Descendants<LiteralInline>().Select(x => x.Content.ToString()));
}
