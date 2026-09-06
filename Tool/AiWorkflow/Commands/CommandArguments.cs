using Lyking.AiWorkflow.Core;

namespace Lyking.AiWorkflow.Commands;

public sealed class CommandArguments
{
    public string Command { get; }
    private readonly Dictionary<string, List<string>> values = new();
    private CommandArguments(string command) => Command = command;
    public string? Value(string name) => values.TryGetValue(name, out var v) ? v[0] : null;
    public IReadOnlyList<string> Values(string name) => values.TryGetValue(name, out var v) ? v : [];
    public bool Has(string name) => values.ContainsKey(name);

    public static CommandArguments Parse(string[] args)
    {
        if (args.Length == 0) throw new WorkflowException("需要命令；使用 help 查看语法。", "command.usage");
        var parsed = new CommandArguments(args[0]);
        string[] options = parsed.Command switch
        {
            "route" => ["--text", "--path"],
            "validate" => ["--change", "--transition"],
            "sync-adapters" => ["--check"],
            "check-workspace" => ["--change", "--allow"],
            "help" => [],
            _ => throw new WorkflowException($"未知命令：{parsed.Command}", "command.unknown")
        };
        for (var i = 1; i < args.Length; i++)
        {
            var name = args[i];
            if (!options.Contains(name) || parsed.Has(name) && name is not ("--path" or "--allow"))
                throw new WorkflowException($"未知或重复选项：{name}", "command.usage");
            var value = "true";
            if (name != "--check")
            {
                if (++i >= args.Length || args[i].StartsWith("--", StringComparison.Ordinal) || string.IsNullOrWhiteSpace(args[i]))
                    throw new WorkflowException($"选项缺少值：{name}", "command.usage");
                value = args[i];
            }
            if (!parsed.values.TryGetValue(name, out var list)) parsed.values[name] = list = [];
            list.Add(value);
        }
        if (parsed.Command == "route" && !parsed.Has("--text") && !parsed.Has("--path") ||
            parsed.Command == "check-workspace" && !parsed.Has("--change") && !parsed.Has("--allow") ||
            parsed.Has("--transition") && (!parsed.Has("--change") || parsed.Value("--transition")!.Split(':').Length != 2))
            throw new WorkflowException("命令缺少必需参数或状态转换格式无效。", "command.usage");
        return parsed;
    }
}
