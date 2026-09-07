namespace Lyking.AiWorkflow.Validation;

public enum IssueSeverity { Warning, Error }
public sealed record ValidationIssue(string Code, IssueSeverity Severity, string Message, string? Path = null);
public sealed record ValidationReport(IReadOnlyList<ValidationIssue> Issues)
{
    public bool IsValid => Issues.All(i => i.Severity != IssueSeverity.Error);
    public static ValidationReport Sorted(IEnumerable<ValidationIssue> issues) => new(issues.OrderBy(i => i.Path, StringComparer.Ordinal).ThenBy(i => i.Code, StringComparer.Ordinal).ToArray());
}
