namespace Lyking.AiWorkflow.Core;

public static class PathPolicy
{
    public static StringComparison Comparison => OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    public static string ResolveWithinRoot(string root, string path)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new WorkflowException("路径不能为空。", "path.empty");
        var normalized = path.Replace('\\', '/');
        if (normalized.StartsWith('/') || normalized.Contains(':') || normalized.IndexOfAny(['\0', '*', '?', '"', '<', '>', '|']) >= 0)
            throw new WorkflowException("路径必须位于仓库根目录内，不能包含绝对路径或特殊字符。", "path.invalid");
        var fullRoot = Path.GetFullPath(root);
        var full = Path.GetFullPath(Path.Combine(fullRoot, normalized));
        var relative = Path.GetRelativePath(fullRoot, full);
        if (relative == ".." || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) || Path.IsPathRooted(relative))
            throw new WorkflowException("路径不能逃出仓库根目录。", "path.outside");
        var cursor = fullRoot;
        foreach (var segment in relative.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            if (segment == ".") continue;
            if (segment.EndsWith(' ') || segment.EndsWith('.')) throw new WorkflowException("路径段不能以空格或句点结尾。", "path.invalid");
            cursor = Path.Combine(cursor, segment);
            try
            {
                if (File.GetAttributes(cursor).HasFlag(FileAttributes.ReparsePoint))
                    throw new WorkflowException("路径不能经过重解析点。", "path.reparse-point");
            }
            catch (FileNotFoundException) { }
            catch (DirectoryNotFoundException) { }
        }
        return full;
    }

    public static string Relative(string root, string path) => Path.GetRelativePath(root, ResolveWithinRoot(root, path)).Replace('\\', '/');

    public static bool Contains(string directory, string path) =>
        path.Equals(directory.TrimEnd('/'), Comparison) || path.StartsWith(directory.TrimEnd('/') + "/", Comparison);
}
