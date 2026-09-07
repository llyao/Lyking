using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Lyking.AiWorkflow.Adapters;
using Lyking.AiWorkflow.Configuration;
using Lyking.AiWorkflow.Core;
using Lyking.AiWorkflow.Routing;
using Lyking.AiWorkflow.Validation;
using Lyking.AiWorkflow.Workspace;

namespace Lyking.AiWorkflow.Commands;

public static class CommandDispatcher
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public static Task<int> RunAsync(string[] args, string currentDirectory, TextWriter output, TextWriter error)
    {
        try
        {
            var parsed = CommandArguments.Parse(args);
            if (parsed.Command == "help") return Write(output, ExitCodes.Success, new
            {
                command = "help",
                commands = new[] { "route [--text 文本] [--path 相对路径]...", "validate [--change 变更目录] [--transition 起始状态:目标状态]",
                    "sync-adapters [--check]", "check-workspace [--change 变更目录] [--allow 相对路径]..." }
            });
            var root = RepositoryLocator.Find(currentDirectory);
            var config = WorkflowConfigLoader.Load(root);
            switch (parsed.Command)
            {
                case "route":
                    return Write(output, ExitCodes.Success, new Router(root, config).Route(parsed.Value("--text"), parsed.Values("--path")));
                case "validate":
                    var validation = new RepositoryValidator(root, config).Validate(parsed.Value("--change"), parsed.Value("--transition"));
                    return Write(output, validation.IsValid ? ExitCodes.Success : ExitCodes.ValidationFailed, validation);
                case "sync-adapters":
                    var synchronizer = new AdapterSynchronizer(root, config);
                    var adapters = parsed.Has("--check") ? synchronizer.Check() : synchronizer.Sync();
                    return Write(output, adapters.IsValid ? ExitCodes.Success : ExitCodes.ValidationFailed, adapters);
                case "check-workspace":
                    var workspace = new WorkspaceChecker(root).Check(parsed.Value("--change"), parsed.Values("--allow"));
                    return Write(output, workspace.CanProceed ? ExitCodes.Success : ExitCodes.WorkspaceBlocked, workspace);
                default:
                    throw new WorkflowException("未知命令。", "command.unknown");
            }
        }
        catch (WorkflowException ex)
        {
            var code = ex.Code.StartsWith("command.", StringComparison.Ordinal) ? ExitCodes.Usage :
                ex.Code.StartsWith("git.", StringComparison.Ordinal) ? ExitCodes.WorkspaceBlocked : ExitCodes.InvalidInput;
            return Write(output, code, new { issues = new[] { new { code = ex.Code, message = ex.Message } } });
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return Write(output, ExitCodes.InvalidInput, new { issues = new[] { new { code = "input.unreadable", message = "输入文件或路径不可读取，请检查配置与文件权限。" } } });
        }
        catch (Exception ex)
        {
            error.WriteLine(ex);
            return Write(output, ExitCodes.InternalError, new { issues = new[] { new { code = "internal.unexpected", message = "发生未预期内部错误。" } } });
        }
    }

    private static Task<int> Write(TextWriter output, int code, object value)
    {
        output.WriteLine(JsonSerializer.Serialize(value, JsonOptions));
        return Task.FromResult(code);
    }
}
