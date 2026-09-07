using System.Text.Json;
using Lyking.AiWorkflow.Commands;

namespace Lyking.AiWorkflow.Tests;

public class GateTests
{
    private static async Task<(int Code, JsonDocument Json)> Run(TestRepository repo, params string[] args)
    {
        var output = new StringWriter();
        var code = await CommandDispatcher.RunAsync(args, repo.Root, output, TextWriter.Null);
        return (code, JsonDocument.Parse(output.ToString()));
    }
    [Theory]
    [InlineData("route --unknown x")]
    [InlineData("route --text")]
    [InlineData("route --text x --text y")]
    [InlineData("validate --transition active:verified")]
    [InlineData("sync-adapters --change x")]
    [InlineData("check-workspace")]
    public async Task 非法参数返回二(string command)
    {
        using var repo = new TestRepository();
        var (code, json) = await Run(repo, command.Split(' ')); using (json) Assert.Equal(2, code);
    }
    [Fact]
    public async Task 路由结构化输出使用驼峰属性()
    {
        using var repo = new TestRepository();
        var (code, json) = await Run(repo, "route", "--path", "Share/Protocol/登录.cs");
        using (json) { Assert.Equal(0, code); Assert.Equal("controlled", json.RootElement.GetProperty("suggestedProfile").GetString()); }
    }
    [Theory]
    [InlineData("main")]
    [InlineData("master")]
    public async Task 受保护分支返回五(string branch)
    {
        using var repo = new TestRepository(); repo.InitGit(branch);
        var (code, json) = await Run(repo, "check-workspace", "--allow", "Tool/"); using (json) Assert.Equal(5, code);
    }
    [Fact]
    public async Task 范围外已暂存文件阻塞且检查不改变仓库()
    {
        using var repo = new TestRepository(); repo.InitGit();
        repo.Write("Server/用户 文件.txt", "用户内容"); repo.Git("add", "--", "Server/用户 文件.txt");
        var before = repo.Git("status", "--porcelain=v1", "-z") + repo.Git("diff", "--cached");
        var (code, json) = await Run(repo, "check-workspace", "--allow", "Tool/");
        using (json) Assert.Equal(5, code);
        Assert.Equal(before, repo.Git("status", "--porcelain=v1", "-z") + repo.Git("diff", "--cached"));
    }
    [Fact]
    public async Task 范围外未暂存文件只警告()
    {
        using var repo = new TestRepository(); repo.InitGit(); repo.Write("Server/用户.txt", "内容");
        var (code, json) = await Run(repo, "check-workspace", "--allow", "Tool/");
        using (json) { Assert.Equal(0, code); Assert.Contains("workspace.unstaged-outside-scope", json.RootElement.ToString()); }
    }
    [Fact]
    public async Task 目录范围不能误匹配相似前缀()
    {
        using var repo = new TestRepository(); repo.InitGit(); repo.Write("Tool/Another/file.txt", "内容"); repo.Git("add", "--", "Tool/Another/file.txt");
        var (code, json) = await Run(repo, "check-workspace", "--allow", "Tool/A"); using (json) Assert.Equal(5, code);
    }
    [Theory]
    [InlineData("MERGE_HEAD")]
    [InlineData("CHERRY_PICK_HEAD")]
    [InlineData("REVERT_HEAD")]
    [InlineData("rebase-merge")]
    [InlineData("rebase-apply")]
    public async Task 进行中的操作阻塞(string operation)
    {
        using var repo = new TestRepository(); repo.InitGit(); repo.Write(".git/" + operation, "操作中");
        var (code, json) = await Run(repo, "check-workspace", "--allow", "Tool/"); using (json) Assert.Equal(5, code);
    }
    [Fact]
    public async Task 游离分支阻塞()
    {
        using var repo = new TestRepository(); repo.InitGit(); repo.Git("checkout", "--detach");
        var (code, json) = await Run(repo, "check-workspace", "--allow", "Tool/"); using (json) Assert.Equal(5, code);
    }
    [Fact]
    public async Task 重命名必须检查源路径范围()
    {
        using var repo = new TestRepository(); repo.InitGit(); repo.Write("Server/旧文件.txt", "原始内容"); repo.Git("add", "--", "Server/"); repo.Git("commit", "-m", "测试数据");
        repo.Git("mv", "--", "Server/旧文件.txt", "Tool/新文件.txt");
        var (code, json) = await Run(repo, "check-workspace", "--allow", "Tool/"); using (json) Assert.Equal(5, code);
    }
}
