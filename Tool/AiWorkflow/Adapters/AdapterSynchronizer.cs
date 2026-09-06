using System.Text;
using System.Text.Json;
using Lyking.AiWorkflow.Configuration;
using Lyking.AiWorkflow.Core;

namespace Lyking.AiWorkflow.Adapters;

public sealed record AdapterIssue(string Code, string Message);
public sealed record AdapterReport(bool IsValid, List<AdapterIssue> Issues);

public sealed class AdapterSynchronizer(string root, WorkflowConfig config)
{
    private readonly string root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
    private static readonly StringComparer PathComparer = StringComparer.OrdinalIgnoreCase;

    public AdapterReport Check() => Compare(false);
    public AdapterReport Sync() => Compare(true);

    private AdapterReport Compare(bool write)
    {
        var issues = new List<AdapterIssue>();
        try
        {
            var directories = ValidateDirectories();
            var generated = Generate().OrderBy(file => file.Path, StringComparer.Ordinal).ToArray();
            if (generated.Select(file => file.Path).Distinct(PathComparer).Count() != generated.Length)
                throw new WorkflowException("多个适配输出使用相同路径。", "adapter.path-conflict");
            var manifestPath = Relative(Resolve(config.Adapters.Manifest));
            var expectedManifest = new AdapterManifest(1, generated.Select(file => new AdapterManifestFile(file.Path, file.SourceDigest)).ToArray());
            var manifestText = JsonSerializer.Serialize(expectedManifest, SourceText.JsonOptions) + "\n";
            var previous = ReadManifest(manifestPath, directories);
            var managed = previous.Files.Select(file => file.Path).ToHashSet(PathComparer);
            var expected = generated.Select(file => file.Path).ToHashSet(PathComparer);
            var obsolete = managed.Where(path => !expected.Contains(path)).Order(StringComparer.Ordinal).ToArray();

            // 所有源、清单和目标均完成预检后，才能开始修改文件。
            foreach (var file in generated)
            {
                var target = Resolve(file.Path);
                ValidateTarget(file.Path, write);
                if (File.Exists(target) && !managed.Contains(file.Path))
                    issues.Add(new("adapter.unmanaged", $"拒绝覆盖非受管文件：{file.Path}"));
                if (!write && !Matches(target, file.Content))
                    issues.Add(new("adapter.drift", $"适配文件缺失或漂移：{file.Path}"));
            }
            ValidateTarget(manifestPath, write);
            foreach (var path in obsolete)
            {
                ValidateTarget(path, write);
                if (!write) issues.Add(new("adapter.drift", $"清单包含过期适配：{path}"));
            }
            if (!write && !Matches(Resolve(manifestPath), manifestText))
                issues.Add(new("adapter.drift", "适配清单缺失或漂移。"));
            if (write && issues.Count == 0)
                Apply(generated.Append(new GeneratedFile(manifestPath, manifestText, "")).ToArray(), obsolete, directories);
        }
        catch (WorkflowException error)
        {
            issues.Add(new(error.Code, error.Message));
            if (!write && error.Code.StartsWith("adapter.manifest", StringComparison.Ordinal))
                issues.Add(new("adapter.drift", "适配清单无效。"));
        }
        catch (IOException)
        {
            issues.Add(new("adapter.io", "适配文件读写失败，请检查路径和文件占用。"));
        }
        catch (UnauthorizedAccessException)
        {
            issues.Add(new("adapter.access", "没有访问适配文件的权限。"));
        }
        return new AdapterReport(issues.Count == 0, issues.OrderBy(issue => issue.Code, StringComparer.Ordinal).ThenBy(issue => issue.Message, StringComparer.Ordinal).ToList());
    }

    private string[] ValidateDirectories()
    {
        var directories = new[] { config.Adapters.ClaudeSkills, config.Adapters.CodexAgents, config.Adapters.ClaudeAgents, config.Adapters.GeminiAgents }
            .Select(path => Relative(Resolve(path))).ToArray();
        var sources = new[] { ".agents/skills", ".agents/roles" };
        foreach (var directory in directories)
        {
            if (directory == "." || sources.Any(source => Within(directory, source) || Within(source, directory)))
                throw new WorkflowException("适配目录不能与共享真源目录重叠。", "adapter.path-conflict");
        }
        for (var index = 0; index < directories.Length; index++)
            for (var other = index + 1; other < directories.Length; other++)
                if (Within(directories[index], directories[other]) || Within(directories[other], directories[index]))
                    throw new WorkflowException("适配目录不能相互重叠。", "adapter.path-conflict");
        var manifest = Relative(Resolve(config.Adapters.Manifest));
        if (directories.Concat(sources).Any(directory => Within(manifest, directory) || Within(directory, manifest)))
            throw new WorkflowException("适配清单不能与源目录或适配目录重叠。", "adapter.path-conflict");
        return directories;
    }

    private IEnumerable<GeneratedFile> Generate()
    {
        foreach (var directory in SourceDirectories(".agents/skills", "skill"))
        {
            var source = SkillSource.Load(root, directory);
            var target = Relative(Resolve($"{config.Adapters.ClaudeSkills}/{source.Name}/SKILL.md"));
            var marker = $"<!-- 由 sync-adapters 自动生成；真源：{source.Path}；sha256：{source.SourceDigest} -->";
            yield return new(target, source.Content.TrimEnd() + "\n\n" + marker + "\n", source.SourceDigest);
        }
        foreach (var directory in SourceDirectories(".agents/roles", "role"))
        {
            var role = RoleDefinition.Load(root, directory);
            var instructions = role.Instructions.TrimEnd();
            var marker = $"由 sync-adapters 自动生成；sha256：{role.SourceDigest}";
            var codex = $"name = {SourceText.Quote(role.Name)}\ndescription = {SourceText.Quote(role.Description)}\n";
            if (role.Mode == "read-only") codex += "sandbox_mode = \"read-only\"\n";
            codex += "developer_instructions = \"\"\"\n" + EscapeTomlInstructions(instructions) + "\n\"\"\"\n# " + marker + "\n";
            yield return new(Relative(Resolve($"{config.Adapters.CodexAgents}/{role.Name}.toml")), codex, role.SourceDigest);
            foreach (var (destination, tools) in new[]
                     {
                         (config.Adapters.ClaudeAgents, "Read, Grep, Glob"),
                         (config.Adapters.GeminiAgents, "[read_file, grep_search, glob]")
                     })
            {
                var content = $"---\nname: {role.Name}\ndescription: {SourceText.Quote(role.Description)}\n";
                if (role.Mode == "read-only") content += $"tools: {tools}\n";
                content += $"---\n\n{instructions}\n\n<!-- {marker} -->\n";
                yield return new(Relative(Resolve($"{destination}/{role.Name}.md")), content, role.SourceDigest);
            }
        }
    }

    private IEnumerable<string> SourceDirectories(string source, string category)
    {
        var full = Resolve(source);
        if (!Directory.Exists(full)) throw new WorkflowException($"缺少共享真源目录：{source}", $"{category}.missing");
        return Directory.EnumerateDirectories(full).Select(path => Relative(Resolve(Relative(path)))).Order(StringComparer.Ordinal).ToArray();
    }

    private AdapterManifest ReadManifest(string path, string[] directories)
    {
        var full = Resolve(path);
        if (!File.Exists(full)) return new(1, []);
        var manifest = AdapterManifest.Parse(SourceText.Read(root, path));
        var paths = new HashSet<string>(PathComparer);
        foreach (var file in manifest.Files)
        {
            var normalized = Relative(Resolve(file.Path));
            if (file.Path != normalized || !directories.Any(directory => normalized.StartsWith(directory + "/", StringComparison.OrdinalIgnoreCase)))
                throw new WorkflowException($"清单路径不属于配置适配目录：{file.Path}", "adapter.manifest-path");
            if (!paths.Add(normalized)) throw new WorkflowException($"清单路径重复：{file.Path}", "adapter.manifest-path");
        }
        return manifest;
    }

    private void ValidateTarget(string path, bool write)
    {
        var full = Resolve(path);
        if (Directory.Exists(full)) throw new WorkflowException($"适配文件位置已被目录占用：{path}", "adapter.path-conflict");
        if (write && File.Exists(full) && File.GetAttributes(full).HasFlag(FileAttributes.ReadOnly))
            throw new WorkflowException($"适配目标为只读文件：{path}", "adapter.read-only");
        if (write && File.Exists(full))
        {
            using var access = new FileStream(Resolve(path), FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        }
        var parent = Path.GetDirectoryName(full)!;
        while (!PathComparer.Equals(parent, root))
        {
            Resolve(Relative(parent));
            if (File.Exists(parent)) throw new WorkflowException($"适配父目录位置已被文件占用：{Relative(parent)}", "adapter.path-conflict");
            parent = Path.GetDirectoryName(parent)!;
        }
    }

    private void Apply(GeneratedFile[] files, string[] obsolete, string[] directories)
    {
        var changes = files.Where(file => !Matches(Resolve(file.Path), file.Content)).ToArray();
        var affected = changes.Select(file => file.Path).Concat(obsolete).ToArray();
        var original = affected.ToDictionary(path => path, path => File.Exists(Resolve(path)) ? File.ReadAllBytes(Resolve(path)) : null, PathComparer);
        var modifiedTimes = affected.Where(path => File.Exists(Resolve(path))).ToDictionary(path => path, path => File.GetLastWriteTimeUtc(Resolve(path)), PathComparer);
        var createdDirectories = new List<string>();
        var modified = new List<string>();
        try
        {
            foreach (var file in changes)
            {
                var parent = Path.GetDirectoryName(Resolve(file.Path))!;
                var missing = new Stack<string>();
                while (!PathComparer.Equals(parent, root) && !Directory.Exists(Resolve(Relative(parent))))
                {
                    missing.Push(Relative(parent));
                    parent = Path.GetDirectoryName(parent)!;
                }
                foreach (var directory in missing)
                {
                    Directory.CreateDirectory(Resolve(directory));
                    createdDirectories.Add(directory);
                }
                modified.Add(file.Path);
                File.WriteAllText(Resolve(file.Path), file.Content, new UTF8Encoding(false));
            }
            foreach (var path in obsolete)
            {
                modified.Add(path);
                File.Delete(Resolve(path));
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or WorkflowException)
        {
            // 意外写入失败时恢复本轮触及的内容，避免留下部分生成结果。
            foreach (var path in modified.AsEnumerable().Reverse())
            {
                if (original[path] is { } bytes)
                {
                    if (!File.Exists(Resolve(path)) || !File.ReadAllBytes(Resolve(path)).AsSpan().SequenceEqual(bytes)) File.WriteAllBytes(Resolve(path), bytes);
                    File.SetLastWriteTimeUtc(Resolve(path), modifiedTimes[path]);
                }
                else if (File.Exists(Resolve(path))) File.Delete(Resolve(path));
            }
            foreach (var directory in createdDirectories.AsEnumerable().Reverse())
                if (Directory.Exists(Resolve(directory)) && !Directory.EnumerateFileSystemEntries(Resolve(directory)).Any()) Directory.Delete(Resolve(directory));
            throw;
        }
        foreach (var path in obsolete) CleanEmptyParents(path, directories);
    }

    private void CleanEmptyParents(string path, string[] directories)
    {
        var parent = Relative(Path.GetDirectoryName(Resolve(path))!);
        while (!directories.Contains(parent, PathComparer) && directories.Any(directory => parent.StartsWith(directory + "/", StringComparison.OrdinalIgnoreCase)))
        {
            var full = Resolve(parent);
            if (!Directory.Exists(full)) return;
            if (Directory.EnumerateFileSystemEntries(full).Any()) return;
            Directory.Delete(full);
            parent = Relative(Path.GetDirectoryName(full)!);
        }
    }

    private static string EscapeTomlInstructions(string instructions)
    {
        return string.Join("\n", instructions.Split('\n').Select(line => SourceText.Quote(line)[1..^1]));
    }

    private bool Matches(string fullPath, string content) => File.Exists(fullPath) && File.ReadAllBytes(fullPath).AsSpan().SequenceEqual(Encoding.UTF8.GetBytes(content));
    private string Resolve(string path) => PathPolicy.ResolveWithinRoot(root, path);
    private string Relative(string fullPath) => Path.GetRelativePath(root, fullPath).Replace('\\', '/');
    private static bool Within(string path, string directory) => PathComparer.Equals(path, directory) || path.StartsWith(directory + "/", StringComparison.OrdinalIgnoreCase);
}
