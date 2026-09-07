using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using Lyking.AiWorkflow.Core;

namespace Lyking.AiWorkflow.Adapters;

internal static partial class SourceText
{
    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = true,
        NewLine = "\n"
    };

    internal static byte[] Read(string root, string path)
    {
        var full = PathPolicy.ResolveWithinRoot(root, path);
        if (!File.Exists(full)) throw new WorkflowException($"缺少源文件：{path}", path.Contains("/roles/", StringComparison.Ordinal) ? "role.missing" : "skill.missing");
        return File.ReadAllBytes(full);
    }

    internal static string Decode(byte[] bytes, string path)
    {
        try
        {
            var text = new UTF8Encoding(false, true).GetString(bytes);
            return text.TrimStart('\uFEFF').ReplaceLineEndings("\n");
        }
        catch (DecoderFallbackException)
        {
            throw new WorkflowException($"文件必须使用有效 UTF-8 编码：{path}", "source.encoding");
        }
    }

    internal static string Digest(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    internal static string Quote(string value) => JsonSerializer.Serialize(value, JsonOptions);
    internal static bool ValidName(string name) => NamePattern().IsMatch(name);
    internal static bool SingleLine(string value) => !string.IsNullOrWhiteSpace(value) && !value.Any(char.IsControl);

    [GeneratedRegex("^[a-z0-9-]+$", RegexOptions.CultureInvariant)]
    private static partial Regex NamePattern();
}
