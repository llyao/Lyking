using Lyking.AiWorkflow.Configuration;
using Lyking.AiWorkflow.Validation;

namespace Lyking.AiWorkflow.Tests;

public class ValidationTests
{
    [Fact]
    public void 活动标准变更可通过()
    {
        using var repo = new TestRepository(); var change = repo.Change();
        var report = new ChangeValidator(repo.Root, WorkflowConfigLoader.Load(repo.Root)).ValidateChange(change, null);
        Assert.True(report.IsValid, string.Join("\n", report.Issues));
    }
    [Theory]
    [InlineData("evidence.md", "artifact.missing")]
    [InlineData("review.md", "review.missing")]
    [InlineData("tasks.md", "artifact.missing")]
    public void 缺少受控工件会阻塞(string file, string code)
    {
        using var repo = new TestRepository(); var change = repo.Change("controlled", "verified");
        File.Delete(Path.Combine(repo.Root, change, file));
        var report = new ChangeValidator(repo.Root, WorkflowConfigLoader.Load(repo.Root)).ValidateChange(change, "active:verified");
        Assert.Contains(report.Issues, i => i.Code == code);
    }
    [Theory]
    [InlineData("evidence.md", "# 证据\n\n## 验证命令\n\n## 验证结果\n", "evidence.empty-section")]
    [InlineData("tasks.md", "# 任务\n\n- [ ] 尚未执行\n", "tasks.incomplete")]
    [InlineData("review.md", "# 审查\n\n## 结论\n\n未通过。\n", "review.blocked")]
    [InlineData("review.md", "# 审查\n\n## 结论\n\n尚未进入审查门禁。\n", "review.blocked")]
    [InlineData("review.md", "# 审查\n\n## 结论\n\n通过，但仍有阻塞。\n", "review.blocked")]
    [InlineData("change.md", "<!-- lyking-ai\n{}\n-->", "change.metadata")]
    public void 无效工件不能进入已验证(string file, string content, string code)
    {
        using var repo = new TestRepository(); var change = repo.Change("controlled", "verified");
        repo.Write(change + "/" + file, content);
        var report = new ChangeValidator(repo.Root, WorkflowConfigLoader.Load(repo.Root)).ValidateChange(change, "active:verified");
        Assert.Contains(report.Issues, i => i.Code == code);
    }
    [Fact]
    public void 非法跳转和元数据目标不一致分别报错()
    {
        using var repo = new TestRepository(); var change = repo.Change();
        var report = new ChangeValidator(repo.Root, WorkflowConfigLoader.Load(repo.Root)).ValidateChange(change, "draft:verified");
        Assert.Contains(report.Issues, i => i.Code == "state.invalid-transition");
        Assert.Contains(report.Issues, i => i.Code == "state.target-mismatch");
    }
    [Fact]
    public void 归档状态必须位于归档目录()
    {
        using var repo = new TestRepository(); var change = repo.Change("standard", "archived");
        var report = new ChangeValidator(repo.Root, WorkflowConfigLoader.Load(repo.Root)).ValidateChange(change, "verified:archived");
        Assert.Contains(report.Issues, i => i.Code == "state.archive-location");
    }
    [Fact]
    public void 硬风险不能通过省略风险字段降档()
    {
        using var repo = new TestRepository(); var change = repo.Change(paths: ["Share/Protocol/登录.cs"]);
        var report = new ChangeValidator(repo.Root, WorkflowConfigLoader.Load(repo.Root)).ValidateChange(change, null);
        Assert.Contains(report.Issues, i => i.Code == "profile.risk-required");
    }
    [Fact]
    public void 缺失链接和非法编码被拒绝()
    {
        using var repo = new TestRepository();
        var file = repo.Write("Tool/中文.md", "# 文档\n\n[缺失](不存在.md)\n\n[外部](https://example.com)\n");
        Assert.Contains(new MarkdownValidator(repo.Root).Validate(file).Issues, i => i.Code == "link.missing");
        File.WriteAllBytes(file, [0xC3, 0x28]);
        Assert.Contains(new MarkdownValidator(repo.Root).Validate(file).Issues, i => i.Code == "encoding.invalid-utf8");
    }
    [Fact]
    public void 空章节不能被下一级标题或注释冒充()
    {
        using var repo = new TestRepository();
        var file = repo.Write("Tool/空.md", "## 验证结果\n\n<!-- 没有结果 -->\n\n## 其他\n\n有内容\n");
        Assert.False(new MarkdownValidator(repo.Root).Validate(file, ["验证结果"]).IsValid);
    }
}
