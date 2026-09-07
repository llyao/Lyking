using Lyking.AiWorkflow.Core;
using Lyking.AiWorkflow.Validation;

namespace Lyking.AiWorkflow.Tests;

public class DocumentationTests
{
    [Theory]
    [InlineData("Art")]
    [InlineData("Client")]
    [InlineData("Server")]
    [InlineData("Share")]
    [InlineData("Tool")]
    public void 领域上下文包含职责与验证入口(string area)
    {
        var path = Path.Combine(TestRepository.RepositoryRoot, area, "CONTEXT.md");
        var report = new MarkdownValidator(TestRepository.RepositoryRoot).Validate(path, ["职责", "当前入口", "必读资料", "不属于本目录", "验证入口"]);
        Assert.True(report.IsValid, string.Join("\n", report.Issues));
    }
    [Theory]
    [InlineData("change")]
    [InlineData("evidence")]
    [InlineData("design")]
    [InlineData("plan")]
    [InlineData("tasks")]
    [InlineData("review")]
    public void 工件模板有效且为简体中文(string name)
    {
        var path = Path.Combine(TestRepository.RepositoryRoot, "docs/ai/templates", name + ".md");
        var content = StrictUtf8Reader.Read(path);
        Assert.NotEmpty(content);
        Assert.DoesNotMatch("[\u3040-\u30ff\uac00-\ud7af]", content);
    }
}
