using System.Text.Json;
using Lyking.AiWorkflow.Core;

namespace Lyking.AiWorkflow.Adapters;

public sealed record RoleDefinition(string Name, string Description, string Mode, string Instructions, string SourceDigest)
{
    public static RoleDefinition Load(string root, string directory)
    {
        var metadataPath = directory + "/role.json";
        var instructionsPath = directory + "/instructions.md";
        var metadata = SourceText.Read(root, metadataPath);
        var instructions = SourceText.Read(root, instructionsPath);
        try
        {
            using var document = JsonDocument.Parse(SourceText.Decode(metadata, metadataPath));
            var value = document.RootElement;
            if (value.ValueKind != JsonValueKind.Object) throw Invalid("角色元数据必须是对象。");
            var keys = value.EnumerateObject().Select(item => item.Name).ToArray();
            if (keys.Length != 4 || keys.Distinct(StringComparer.Ordinal).Count() != 4 || keys.Except(new[] { "schemaVersion", "name", "description", "mode" }, StringComparer.Ordinal).Any())
                throw Invalid("角色元数据必须且只能包含版本、名称、说明和模式。");
            if (!value.GetProperty("schemaVersion").TryGetInt32(out var version) || version != 1)
                throw Invalid("角色仅支持版本 1。");
            var name = String(value, "name");
            var description = String(value, "description");
            var mode = String(value, "mode");
            if (!SourceText.ValidName(name) || name != Path.GetFileName(PathPolicy.ResolveWithinRoot(root, directory)))
                throw Invalid("角色名称无效或与源目录不一致。");
            if (!SourceText.SingleLine(description)) throw Invalid("角色说明必须是非空单行文本。");
            if (mode is not ("read-only" or "write")) throw Invalid("角色模式必须是 read-only 或 write。");
            var content = SourceText.Decode(instructions, instructionsPath);
            if (string.IsNullOrWhiteSpace(content)) throw Invalid("角色指令不能为空。");
            return new(name, description, mode, content, SourceText.Digest([.. metadata, 0, .. instructions]));
        }
        catch (JsonException) { throw Invalid($"角色 JSON 无效：{metadataPath}"); }
        catch (InvalidOperationException) { throw Invalid($"角色字段类型无效：{metadataPath}"); }
    }

    private static string String(JsonElement value, string key) => value.GetProperty(key).ValueKind == JsonValueKind.String
        ? value.GetProperty(key).GetString()! : throw Invalid($"角色字段必须是字符串：{key}");
    private static WorkflowException Invalid(string message) => new(message, "role.invalid");
}
