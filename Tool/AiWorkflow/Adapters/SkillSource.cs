using System.Globalization;
using System.Text.Json;
using Lyking.AiWorkflow.Core;

namespace Lyking.AiWorkflow.Adapters;

public sealed record SkillSource(string Name, string Description, string Path, string Content, string SourceDigest)
{
    public static SkillSource Load(string root, string directory)
    {
        var path = directory + "/SKILL.md";
        var bytes = SourceText.Read(root, path);
        var content = SourceText.Decode(bytes, path);
        var lines = content.Split('\n');
        if (lines[0] != "---") throw Invalid($"技能必须以 YAML 前置区开始：{path}");
        var end = Array.FindIndex(lines, 1, line => line == "---");
        if (end < 0) throw Invalid($"技能 YAML 前置区缺少结束分隔线：{path}");
        var fields = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var index = 1; index < end; index++)
        {
            var line = lines[index];
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#')) continue;
            var colon = line.IndexOf(':');
            if (colon < 1 || char.IsWhiteSpace(line[0])) throw Invalid($"技能前置区只接受单行字段：{path}");
            var key = line[..colon].Trim();
            if (!fields.TryAdd(key, Scalar(line[(colon + 1)..].Trim(), path))) throw Invalid($"技能前置区字段重复：{key}");
        }
        if (!fields.TryGetValue("name", out var name) || !SourceText.ValidName(name) || name.Length > 64 || name != System.IO.Path.GetFileName(PathPolicy.ResolveWithinRoot(root, directory)))
            throw Invalid($"技能名称无效或与目录不一致：{path}");
        if (!fields.TryGetValue("description", out var description) || !SourceText.SingleLine(description))
            throw Invalid($"技能说明必须是非空单行文本：{path}");
        return new(name, description, path, content, SourceText.Digest(bytes));
    }

    private static string Scalar(string value, string path)
    {
        if (value.Length == 0 || value.StartsWith('#')) return "";
        if (value[0] == '"')
        {
            try { return JsonSerializer.Deserialize<string>(value) ?? ""; }
            catch (JsonException) { throw Invalid($"技能前置区引号字符串无效：{path}"); }
        }
        if (value[0] == '\'')
        {
            if (value.Length < 2 || value[^1] != '\'') throw Invalid($"技能前置区引号不匹配：{path}");
            var inner = value[1..^1];
            if (inner.Replace("''", "", StringComparison.Ordinal).Contains('\'')) throw Invalid($"技能前置区单引号必须成对转义：{path}");
            return inner.Replace("''", "'", StringComparison.Ordinal);
        }
        if ("|>!&*[{".Contains(value[0])) throw Invalid($"技能前置区字段必须使用单行字符串：{path}");
        var comment = value.IndexOf(" #", StringComparison.Ordinal);
        var plain = (comment < 0 ? value : value[..comment]).TrimEnd();
        if (plain.Contains(": ", StringComparison.Ordinal) || plain.Contains(":\t", StringComparison.Ordinal) || plain.EndsWith(':'))
            throw Invalid($"技能前置区包含冒号的字段必须使用引号：{path}");
        if (plain == "~" || new[] { "null", "true", "false" }.Contains(plain, StringComparer.OrdinalIgnoreCase)
            || double.TryParse(plain, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
            throw Invalid($"技能前置区的空值、布尔值和数字必须加引号作为字符串：{path}");
        return plain;
    }

    private static WorkflowException Invalid(string message) => new(message, "skill.invalid");
}
