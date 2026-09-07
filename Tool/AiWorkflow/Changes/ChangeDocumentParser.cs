using System.Text.Json;
using Lyking.AiWorkflow.Core;

namespace Lyking.AiWorkflow.Changes;

public static class ChangeDocumentParser
{
    public static readonly IReadOnlyDictionary<string, WorkflowStatus> Statuses = new Dictionary<string, WorkflowStatus>(StringComparer.Ordinal)
    {
        ["draft"] = WorkflowStatus.Draft,
        ["ready"] = WorkflowStatus.Ready,
        ["active"] = WorkflowStatus.Active,
        ["blocked"] = WorkflowStatus.Blocked,
        ["verified"] = WorkflowStatus.Verified,
        ["archived"] = WorkflowStatus.Archived
    };
    private static readonly IReadOnlyDictionary<string, WorkProfile> Profiles = new Dictionary<string, WorkProfile>(StringComparer.Ordinal)
    {
        ["direct"] = WorkProfile.Direct,
        ["standard"] = WorkProfile.Standard,
        ["controlled"] = WorkProfile.Controlled
    };

    public static ChangeMetadata Parse(string markdown, string source = "change.md")
    {
        const string marker = "<!-- lyking-ai";
        var first = markdown.IndexOf(marker, StringComparison.Ordinal);
        if (first < 0 || markdown.IndexOf(marker, first + marker.Length, StringComparison.Ordinal) >= 0)
            throw new WorkflowException($"{source} 必须包含且只能包含一个元数据块。", "change.metadata");
        var close = markdown.IndexOf("-->", first, StringComparison.Ordinal);
        if (close < 0) throw new WorkflowException($"{source} 元数据块未闭合。", "change.metadata");
        try
        {
            using var document = JsonDocument.Parse(markdown[(first + marker.Length)..close]);
            var json = document.RootElement;
            if (json.ValueKind != JsonValueKind.Object || json.EnumerateObject().GroupBy(p => p.Name).Any(g => g.Count() > 1))
                throw new FormatException();
            if (json.GetProperty("schemaVersion").GetInt32() != 1) throw new FormatException();
            var id = RequiredString(json.GetProperty("id"));
            if (id.IndexOfAny(['/', '\\']) >= 0 || id is "." or "..") throw new FormatException();
            var profileText = RequiredString(json.GetProperty("profile")); var statusText = RequiredString(json.GetProperty("status"));
            if (!Profiles.TryGetValue(profileText, out var profile) || !Statuses.TryGetValue(statusText, out var status)) throw new FormatException();
            var areas = Strings(json.GetProperty("areas"));
            if (areas.Count == 0) throw new FormatException();
            var paths = json.TryGetProperty("paths", out var p) ? Strings(p) : [];
            return new(1, id, profile, status, areas, paths, Strings(json.GetProperty("riskSignals")));
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
        {
            throw new WorkflowException($"{source} 元数据格式、版本或字段值无效。", "change.metadata");
        }
    }

    private static string RequiredString(JsonElement value) =>
        value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString()) ? value.GetString()! : throw new FormatException();

    private static List<string> Strings(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Array) throw new FormatException();
        var result = value.EnumerateArray().Select(RequiredString).ToList();
        if (result.Distinct(StringComparer.Ordinal).Count() != result.Count) throw new FormatException();
        return result;
    }
}
