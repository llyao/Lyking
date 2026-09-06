namespace Lyking.AiWorkflow.Tests;

public class CheckoutTests
{
    [Theory]
    [InlineData(".lyking-ai-adapters.json")]
    [InlineData(".codex/agents/reviewer.toml")]
    [InlineData(".claude/agents/reviewer.md")]
    [InlineData(".gemini/agents/reviewer.md")]
    [InlineData(".agents/skills/lyking-design/SKILL.md")]
    public void 真实受管文件重新检出保持字节(string path)
    {
        using var repo = new TestRepository(); repo.InitGit(); repo.Git("config", "core.autocrlf", "true");
        repo.Write(".gitattributes", File.ReadAllText(Path.Combine(TestRepository.RepositoryRoot, ".gitattributes")));
        repo.Write(path, File.ReadAllText(Path.Combine(TestRepository.RepositoryRoot, path)));
        var before = File.ReadAllBytes(Path.Combine(repo.Root, path));
        repo.Git("add", "--", ".gitattributes", path);
        File.Delete(Path.Combine(repo.Root, path));
        repo.Git("checkout-index", "--all", "--force");
        Assert.Equal(before, File.ReadAllBytes(Path.Combine(repo.Root, path)));
    }
    [Fact]
    public void 自动换行开启时重新检出保持适配源字节()
    {
        using var repo = new TestRepository(); repo.InitGit(); repo.Git("config", "core.autocrlf", "true");
        repo.Write(".gitattributes", File.ReadAllText(Path.Combine(TestRepository.RepositoryRoot, ".gitattributes")));
        const string path = ".agents/skills/sample/SKILL.md";
        repo.Write(path, "---\nname: sample\ndescription: 测试说明\n---\n\n# 中文技能\n");
        var before = File.ReadAllBytes(Path.Combine(repo.Root, path));
        repo.Git("add", "--", ".gitattributes", ".agents/");
        File.Delete(Path.Combine(repo.Root, path));
        repo.Git("checkout-index", "--all", "--force");
        Assert.Equal(before, File.ReadAllBytes(Path.Combine(repo.Root, path)));
    }
}
