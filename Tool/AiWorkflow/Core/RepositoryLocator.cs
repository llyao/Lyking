namespace Lyking.AiWorkflow.Core;

public static class RepositoryLocator
{
    public static string Find(string? startPath = null)
    {
        var current = new DirectoryInfo(Path.GetFullPath(startPath ?? Directory.GetCurrentDirectory()));
        if (File.Exists(current.FullName)) current = current.Parent ?? current;
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, ".lyking-ai.json"))) return current.FullName;
            current = current.Parent;
        }
        throw new WorkflowException($"未找到工作流配置，起点为：{startPath ?? Directory.GetCurrentDirectory()}", "repository.not-found");
    }
}
