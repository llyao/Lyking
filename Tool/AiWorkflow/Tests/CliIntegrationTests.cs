using System.Diagnostics;
using System.Text.Json;

namespace Lyking.AiWorkflow.Tests;

public class CliIntegrationTests
{
    [Theory]
    [InlineData("help", 0)]
    [InlineData("unknown", 2)]
    [InlineData("route --path Share/Protocol/登录.cs", 0)]
    [InlineData("route --path ../外部", 3)]
    [InlineData("route --text", 2)]
    [InlineData("sync-adapters --check", 4)]
    [InlineData("validate", 4)]
    [InlineData("check-workspace --allow Tool/", 0)]
    public async Task 真实进程输出单个结构化文档与正确退出码(string command, int expected)
    {
        using var repo = new TestRepository(); repo.InitGit();
        var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        var dll = Path.Combine(TestRepository.RepositoryRoot, "Tool/AiWorkflow/bin", configuration, "net10.0/Lyking.AiWorkflow.dll");
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo("dotnet")
            {
                WorkingDirectory = Path.Combine(repo.Root, "Tool"),
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            }
        };
        process.StartInfo.ArgumentList.Add(dll);
        foreach (var arg in command.Split(' ')) process.StartInfo.ArgumentList.Add(arg);
        process.Start();
        var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try { await process.WaitForExitAsync(timeout.Token); }
        catch (OperationCanceledException) { process.Kill(true); await process.WaitForExitAsync(); throw new TimeoutException("命令行验收进程超时。"); }
        using var json = JsonDocument.Parse(await output);
        Assert.Equal(JsonValueKind.Object, json.RootElement.ValueKind);
        Assert.Equal(expected, process.ExitCode);
        Assert.Equal("", await error);
    }
}
