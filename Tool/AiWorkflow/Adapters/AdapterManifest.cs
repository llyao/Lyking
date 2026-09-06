using System.Text.Json;
using Lyking.AiWorkflow.Core;

namespace Lyking.AiWorkflow.Adapters;

public sealed record AdapterManifestFile(string Path, string SourceDigest);
public sealed record AdapterManifest(int SchemaVersion, AdapterManifestFile[] Files)
{
    internal static AdapterManifest Parse(byte[] bytes)
    {
        try
        {
            using var document = JsonDocument.Parse(bytes);
            var value = document.RootElement;
            RequireKeys(value, ["schemaVersion", "files"]);
            if (!value.GetProperty("schemaVersion").TryGetInt32(out var version) || version != 1 || value.GetProperty("files").ValueKind != JsonValueKind.Array)
                throw Invalid();
            var files = new List<AdapterManifestFile>();
            foreach (var item in value.GetProperty("files").EnumerateArray())
            {
                RequireKeys(item, ["path", "sourceDigest"]);
                if (item.GetProperty("path").ValueKind != JsonValueKind.String || item.GetProperty("sourceDigest").ValueKind != JsonValueKind.String) throw Invalid();
                var path = item.GetProperty("path").GetString()!;
                var digest = item.GetProperty("sourceDigest").GetString()!;
                if (string.IsNullOrWhiteSpace(path) || digest.Length != 64 || digest.Any(character => !char.IsAsciiHexDigitLower(character))) throw Invalid();
                files.Add(new(path, digest));
            }
            return new(1, files.ToArray());
        }
        catch (JsonException) { throw Invalid(); }
        catch (InvalidOperationException) { throw Invalid(); }
    }

    private static void RequireKeys(JsonElement value, string[] expected)
    {
        if (value.ValueKind != JsonValueKind.Object) throw Invalid();
        var keys = value.EnumerateObject().Select(item => item.Name).ToArray();
        if (keys.Length != expected.Length || keys.Distinct(StringComparer.Ordinal).Count() != expected.Length || keys.Except(expected, StringComparer.Ordinal).Any()) throw Invalid();
    }

    private static WorkflowException Invalid() => new("适配清单结构、版本或摘要无效，拒绝覆盖已有清单。", "adapter.manifest-invalid");
}
