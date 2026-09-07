using Lyking.AiWorkflow.Adapters;
using Lyking.AiWorkflow.Configuration;

namespace Lyking.AiWorkflow.Tests;

public class RepositoryAdapterTests
{
    [Fact]
    public void 当前仓库适配与真源一致()
    {
        var root = TestRepository.RepositoryRoot;
        var report = new AdapterSynchronizer(root, WorkflowConfigLoader.Load(root)).Check();
        Assert.True(report.IsValid, string.Join("\n", report.Issues));
    }
    [Fact]
    public void Gemini直接复用六个共享技能()
    {
        var root = TestRepository.RepositoryRoot;
        Assert.False(Directory.Exists(Path.Combine(root, ".gemini/skills")));
        Assert.Equal(6, Directory.GetDirectories(Path.Combine(root, ".agents/skills"), "lyking-*").Length);
    }
}
