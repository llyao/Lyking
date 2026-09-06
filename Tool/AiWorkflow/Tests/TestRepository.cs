using System.Diagnostics;
using System.Text;
using System.Text.Json.Nodes;
using Lyking.AiWorkflow.Core;

namespace Lyking.AiWorkflow.Tests;

public sealed class TestRepository : IDisposable
{
    public static string RepositoryRoot => RepositoryLocator.Find(AppContext.BaseDirectory);
    public string Root { get; } = Path.Combine(Path.GetTempPath(), "lyking-ai-tests", Guid.NewGuid().ToString("N"));
    public TestRepository()
    {
        Directory.CreateDirectory(Root);
        Write(".lyking-ai.json", File.ReadAllText(Path.Combine(RepositoryRoot, ".lyking-ai.json")));
        foreach (var area in new[] { "Art", "Client", "Server", "Share", "Tool" }) Directory.CreateDirectory(Path.Combine(Root, area));
    }
    public string Write(string path, string content)
    {
        var target = Path.Combine(Root, path);
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        File.WriteAllText(target, content, new UTF8Encoding(false));
        return target;
    }
    public void Configure(Action<JsonObject> update)
    {
        var config = JsonNode.Parse(File.ReadAllText(Path.Combine(Root, ".lyking-ai.json")))!.AsObject();
        update(config);
        Write(".lyking-ai.json", config.ToJsonString());
    }
    public string Change(string profile = "standard", string status = "active", string[]? areas = null, string[]? paths = null)
    {
        var relative = "docs/changes/active/sample";
        var metadata = System.Text.Json.JsonSerializer.Serialize(new { schemaVersion = 1, id = "sample", profile, status, areas = areas ?? ["Tool"], paths = paths ?? new[] { "Tool/Feature/" }, riskSignals = Array.Empty<string>() });
        Write(relative + "/change.md", "<!-- lyking-ai\n" + metadata + "\n-->\n\n# 示例\n\n" + string.Join("\n\n", new[] { "解决什么问题", "完成后应看到什么", "验收方式", "范围与非目标", "风险与回滚" }.Select(h => "## " + h + "\n\n已明确本项结果。")));
        Write(relative + "/evidence.md", "# 验证证据\n\n## 验证命令\n\n执行对应测试。\n\n## 验证结果\n\n2026-09-06：退出码 0。\n\n## 人工验收\n\n本变更无需视觉验收。\n");
        if (profile == "controlled")
        {
            Write(relative + "/design.md", "# 设计\n\n设计已经确认。\n");
            Write(relative + "/plan.md", "# 计划\n\n执行对应任务。\n");
            Write(relative + "/tasks.md", "# 任务清单\n\n- [x] 完成验证\n");
            Write(relative + "/review.md", "# 审查\n\n## 结论\n\n通过。\n");
        }
        return relative;
    }
    public string Git(params string[] args)
    {
        using var process = new Process { StartInfo = new ProcessStartInfo("git") { WorkingDirectory = Root, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true } };
        foreach (var arg in args) process.StartInfo.ArgumentList.Add(arg);
        process.Start();
        var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(15000)) { process.Kill(true); throw new TimeoutException("测试 Git 进程超时。"); }
        Task.WaitAll(output, error);
        if (process.ExitCode != 0) throw new InvalidOperationException(error.Result);
        return output.Result;
    }
    public void InitGit(string branch = "codex/sample")
    {
        Git("init", "-b", branch); Git("config", "user.name", "测试用户"); Git("config", "user.email", "test@example.invalid");
        Git("add", "--", ".lyking-ai.json"); Git("commit", "-m", "测试基线");
    }
    public void Dispose()
    {
        var parent = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "lyking-ai-tests")) + Path.DirectorySeparatorChar;
        if (!Path.GetFullPath(Root).StartsWith(parent, StringComparison.Ordinal)) throw new InvalidOperationException("测试清理路径越界。");
        if (Directory.Exists(Root))
        {
            foreach (var file in Directory.EnumerateFiles(Root, "*", SearchOption.AllDirectories)) File.SetAttributes(file, FileAttributes.Normal);
            Directory.Delete(Root, true);
        }
    }
}
