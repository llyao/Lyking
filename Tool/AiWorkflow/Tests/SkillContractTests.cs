using Lyking.AiWorkflow.Adapters;

namespace Lyking.AiWorkflow.Tests;

public class SkillContractTests
{
    [Theory]
    [InlineData("design", "## 档位")]
    [InlineData("implement", "## 入口条件")]
    [InlineData("debug", "## 停止条件")]
    [InlineData("verify", "## 失败处理")]
    [InlineData("review", "## 角色边界")]
    [InlineData("deliver", "## 权限边界")]
    public void 技能具有可发现的前置区与操作边界(string suffix, string section)
    {
        var source = SkillSource.Load(TestRepository.RepositoryRoot, ".agents/skills/lyking-" + suffix);
        Assert.Equal("lyking-" + suffix, source.Name);
        Assert.Contains(section, source.Content);
        Assert.Contains("## 不适用", source.Content);
        Assert.True(source.Content.Split('\n').Length <= 150);
    }
}
