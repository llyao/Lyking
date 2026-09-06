using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Lyking.AiWorkflow.Adapters;
using Lyking.AiWorkflow.Configuration;

namespace Lyking.AiWorkflow.Tests.Adapters;

public sealed class AdapterSynchronizerTests
{
    [Fact]
    public void 同步生成技能和三平台角色并使用正确权限格式()
    {
        using var repo = new AdapterRepository();
        repo.WriteSources();

        var report = repo.Synchronizer.Sync();

        Assert.True(report.IsValid, string.Join("\n", report.Issues.Select(issue => issue.Message)));
        Assert.Contains("# 示例技能", repo.Read(".claude/skills/sample-skill/SKILL.md"));
        Assert.Contains("developer_instructions = \"\"\"", repo.Read(".codex/agents/explorer.toml"));
        Assert.Contains("sandbox_mode = \"read-only\"", repo.Read(".codex/agents/explorer.toml"));
        Assert.DoesNotContain("sandbox_mode", repo.Read(".codex/agents/implementer.toml"));
        Assert.Contains("tools: Read, Grep, Glob", repo.Read(".claude/agents/explorer.md"));
        Assert.Contains("tools: [read_file, grep_search, glob]", repo.Read(".gemini/agents/explorer.md"));
        Assert.DoesNotContain("tools:", repo.Read(".claude/agents/implementer.md"));
        Assert.DoesNotContain("tools:", repo.Read(".gemini/agents/implementer.md"));
        Assert.False(Directory.Exists(Path.Combine(repo.Root, ".gemini/skills")));
    }

    [Fact]
    public void 清单摘要按源字节计算并按路径排序且没有时间戳()
    {
        using var repo = new AdapterRepository();
        repo.WriteSources();
        Assert.True(repo.Synchronizer.Sync().IsValid);

        using var manifest = JsonDocument.Parse(repo.Read(".lyking-ai-adapters.json"));
        Assert.Equal(new[] { "schemaVersion", "files" }, manifest.RootElement.EnumerateObject().Select(item => item.Name));
        Assert.Equal(1, manifest.RootElement.GetProperty("schemaVersion").GetInt32());
        var files = manifest.RootElement.GetProperty("files").EnumerateArray().ToArray();
        var paths = files.Select(file => file.GetProperty("path").GetString()!).ToArray();
        Assert.Equal(paths.Order(StringComparer.Ordinal), paths);
        var skillDigest = Digest(repo.Bytes(".agents/skills/sample-skill/SKILL.md"));
        var roleDigest = Digest([.. repo.Bytes(".agents/roles/explorer/role.json"), 0, .. repo.Bytes(".agents/roles/explorer/instructions.md")]);
        Assert.Equal(skillDigest, files.Single(file => file.GetProperty("path").GetString() == ".claude/skills/sample-skill/SKILL.md").GetProperty("sourceDigest").GetString());
        Assert.Equal(roleDigest, files.Single(file => file.GetProperty("path").GetString() == ".codex/agents/explorer.toml").GetProperty("sourceDigest").GetString());
        Assert.Contains($"sha256：{skillDigest}", repo.Read(".claude/skills/sample-skill/SKILL.md"));
        Assert.Contains($"sha256：{roleDigest}", repo.Read(".codex/agents/explorer.toml"));
    }

    [Fact]
    public void 连续同步不改变文件字节与修改时间()
    {
        using var repo = new AdapterRepository();
        repo.WriteSources();
        Assert.True(repo.Synchronizer.Sync().IsValid);
        var before = repo.Snapshot();

        Assert.True(repo.Synchronizer.Sync().IsValid);

        Assert.Equal(before, repo.Snapshot());
    }

    [Theory]
    [InlineData(".codex/agents/explorer.toml")]
    [InlineData(".claude/agents/explorer.md")]
    [InlineData(".gemini/agents/explorer.md")]
    [InlineData(".claude/skills/sample-skill/SKILL.md")]
    [InlineData(".lyking-ai-adapters.json")]
    public void 检查模式发现漂移且不修改任何文件(string changedPath)
    {
        using var repo = new AdapterRepository();
        repo.WriteSources();
        Assert.True(repo.Synchronizer.Sync().IsValid);
        repo.Write(changedPath, "手工修改\n");
        var before = repo.Snapshot();

        var report = repo.Synchronizer.Check();

        Assert.False(report.IsValid);
        Assert.Contains(report.Issues, issue => issue.Code == "adapter.drift");
        Assert.Equal(before, repo.Snapshot());
    }

    [Theory]
    [InlineData("没有前置区\n")]
    [InlineData("---\nname: sample-skill\ndescription: 说明\n")]
    [InlineData("---\nname: other\ndescription: 说明\n---\n正文\n")]
    [InlineData("---\nname: sample-skill\ndescription: \n---\n正文\n")]
    [InlineData("---\nname: sample-skill\ndescription: |\n  多行说明\n---\n正文\n")]
    [InlineData("---\nname: sample-skill\nname: sample-skill\ndescription: 说明\n---\n正文\n")]
    public void 非法技能前置区在写入前失败(string source)
    {
        using var repo = new AdapterRepository();
        repo.WriteSources();
        repo.Write(".agents/skills/sample-skill/SKILL.md", source);
        var before = repo.Snapshot();

        var report = repo.Synchronizer.Sync();

        Assert.False(report.IsValid);
        Assert.Contains(report.Issues, issue => issue.Code.StartsWith("skill.", StringComparison.Ordinal));
        Assert.Equal(before, repo.Snapshot());
    }

    [Theory]
    [InlineData("Bad_Name")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    public void 非法技能名称被拒绝(string name)
    {
        using var repo = new AdapterRepository();
        repo.WriteSources();
        repo.Write($".agents/skills/{name}/SKILL.md", $"---\nname: {name}\ndescription: 示例说明\n---\n正文\n");

        Assert.False(repo.Synchronizer.Sync().IsValid);
        Assert.False(File.Exists(Path.Combine(repo.Root, ".lyking-ai-adapters.json")));
    }

    [Theory]
    [InlineData("不是有效结构")]
    [InlineData("{\"schemaVersion\":2,\"name\":\"explorer\",\"description\":\"说明\",\"mode\":\"read-only\"}")]
    [InlineData("{\"schemaVersion\":1,\"name\":\"../escape\",\"description\":\"说明\",\"mode\":\"read-only\"}")]
    [InlineData("{\"schemaVersion\":1,\"name\":\"other\",\"description\":\"说明\",\"mode\":\"read-only\"}")]
    [InlineData("{\"schemaVersion\":1,\"name\":\"explorer\",\"description\":\"\",\"mode\":\"read-only\"}")]
    [InlineData("{\"schemaVersion\":1,\"name\":\"explorer\",\"description\":\"说明\",\"mode\":\"unknown\"}")]
    [InlineData("{\"schemaVersion\":1,\"name\":\"explorer\",\"name\":\"explorer\",\"description\":\"说明\",\"mode\":\"read-only\"}")]
    public void 非法角色元数据被拒绝且保留全部文件(string source)
    {
        using var repo = new AdapterRepository();
        repo.WriteSources();
        repo.Write(".agents/roles/explorer/role.json", source);
        var before = repo.Snapshot();

        var report = repo.Synchronizer.Sync();

        Assert.False(report.IsValid);
        Assert.Contains(report.Issues, issue => issue.Code.StartsWith("role.", StringComparison.Ordinal));
        Assert.Equal(before, repo.Snapshot());
    }

    [Fact]
    public void 角色指令缺失时不生成其他角色()
    {
        using var repo = new AdapterRepository();
        repo.WriteSources();
        File.Delete(Path.Combine(repo.Root, ".agents/roles/reviewer/instructions.md"));
        var before = repo.Snapshot();

        Assert.False(repo.Synchronizer.Sync().IsValid);
        Assert.Equal(before, repo.Snapshot());
    }

    [Fact]
    public void 非受管同名文件在全部预检完成前不会被覆盖()
    {
        using var repo = new AdapterRepository();
        repo.WriteSources();
        repo.Write(".gemini/agents/reviewer.md", "人工维护\n");
        var before = repo.Snapshot();

        var report = repo.Synchronizer.Sync();

        Assert.False(report.IsValid);
        Assert.Contains(report.Issues, issue => issue.Code == "adapter.unmanaged");
        Assert.Equal(before, repo.Snapshot());
    }

    [Fact]
    public void 更新过程中发现新文件冲突也不覆盖已受管文件()
    {
        using var repo = new AdapterRepository();
        repo.WriteSources();
        Assert.True(repo.Synchronizer.Sync().IsValid);
        repo.Write(".agents/roles/explorer/instructions.md", "新的探索指令\n");
        repo.Write(".agents/skills/second-skill/SKILL.md", "---\nname: second-skill\ndescription: 第二个说明\n---\n正文\n");
        repo.Write(".claude/skills/second-skill/SKILL.md", "人工维护\n");
        var before = repo.Snapshot();

        Assert.False(repo.Synchronizer.Sync().IsValid);
        Assert.Equal(before, repo.Snapshot());
    }

    [Fact]
    public void 删除旧清单受管文件时保留非受管文件与空目录()
    {
        using var repo = new AdapterRepository();
        repo.WriteSources();
        Assert.True(repo.Synchronizer.Sync().IsValid);
        Directory.Delete(Path.Combine(repo.Root, ".agents/skills/sample-skill"), true);
        repo.Write(".claude/skills/manual/说明.md", "人工文件\n");
        Directory.CreateDirectory(Path.Combine(repo.Root, ".claude/skills/manual-empty"));

        Assert.True(repo.Synchronizer.Sync().IsValid);

        Assert.False(Directory.Exists(Path.Combine(repo.Root, ".claude/skills/sample-skill")));
        Assert.True(File.Exists(Path.Combine(repo.Root, ".claude/skills/manual/说明.md")));
        Assert.True(Directory.Exists(Path.Combine(repo.Root, ".claude/skills/manual-empty")));
        Assert.True(repo.Synchronizer.Check().IsValid);
    }

    [Theory]
    [InlineData("docs/重要.md")]
    [InlineData(".claude/skills-other/重要.md")]
    [InlineData("../重要.md")]
    public void 旧清单不能授权删除适配目录以外的文件(string path)
    {
        using var repo = new AdapterRepository();
        repo.WriteSources();
        Assert.True(repo.Synchronizer.Sync().IsValid);
        if (!path.StartsWith("..", StringComparison.Ordinal)) repo.Write(path, "重要内容\n");
        repo.Write(".lyking-ai-adapters.json", JsonSerializer.Serialize(new { schemaVersion = 1, files = new[] { new { path, sourceDigest = new string('a', 64) } } }));
        var before = repo.Snapshot();

        Assert.False(repo.Synchronizer.Sync().IsValid);
        Assert.Equal(before, repo.Snapshot());
    }

    [Fact]
    public void 适配目录经过重解析点时拒绝写入()
    {
        using var repo = new AdapterRepository();
        using var outside = new AdapterRepository();
        repo.WriteSources();
        Directory.CreateDirectory(Path.Combine(repo.Root, ".claude"));
        if (OperatingSystem.IsWindows())
        {
            var start = new System.Diagnostics.ProcessStartInfo("cmd.exe") { UseShellExecute = false, CreateNoWindow = true };
            start.ArgumentList.Add("/c");
            start.ArgumentList.Add("mklink");
            start.ArgumentList.Add("/J");
            start.ArgumentList.Add(Path.Combine(repo.Root, ".claude", "skills"));
            start.ArgumentList.Add(outside.Root);
            using var process = System.Diagnostics.Process.Start(start)!;
            Assert.True(process.WaitForExit(10000));
            Assert.Equal(0, process.ExitCode);
        }
        else Directory.CreateSymbolicLink(Path.Combine(repo.Root, ".claude/skills"), outside.Root);
        try
        {
            var report = repo.Synchronizer.Sync();
            Assert.False(report.IsValid);
            Assert.Empty(Directory.EnumerateFileSystemEntries(outside.Root));
        }
        finally { Directory.Delete(Path.Combine(repo.Root, ".claude/skills")); }
    }

    [Fact]
    public void 源换行统一输出且摘要仍反映原始字节()
    {
        using var repo = new AdapterRepository();
        repo.WriteSources();
        repo.Write(".agents/skills/sample-skill/SKILL.md", repo.Read(".agents/skills/sample-skill/SKILL.md").Replace("\n", "\r\n"));
        repo.Write(".agents/roles/explorer/instructions.md", "中文指令\r\n第二行\r\n");

        Assert.True(repo.Synchronizer.Sync().IsValid);

        Assert.DoesNotContain("\r", repo.Read(".claude/skills/sample-skill/SKILL.md"));
        Assert.DoesNotContain("\r", repo.Read(".codex/agents/explorer.toml"));
        Assert.Contains(Digest(repo.Bytes(".agents/skills/sample-skill/SKILL.md")), repo.Read(".claude/skills/sample-skill/SKILL.md"));
        Assert.True(repo.Synchronizer.Check().IsValid);
    }

    [Fact]
    public void 角色引号与反斜杠不能突破平台字符串边界()
    {
        using var repo = new AdapterRepository();
        repo.WriteSources();
        repo.WriteRole("explorer", "read-only", "说明：\"引号\"与路径 C:\\目录", "包含\"\"\"和路径 C:\\目录\n下一行\n");

        Assert.True(repo.Synchronizer.Sync().IsValid);

        var toml = repo.Read(".codex/agents/explorer.toml");
        Assert.Contains("description = \"说明：\\\"引号\\\"与路径 C:\\\\目录\"", toml);
        Assert.Contains("包含\\\"\\\"\\\"和路径 C:\\\\目录", toml);
        Assert.Contains("description: \"说明：\\\"引号\\\"与路径 C:\\\\目录\"", repo.Read(".gemini/agents/explorer.md"));
    }

    [Fact]
    public void 角色指令中的字面反斜杠序列不能被改成换行()
    {
        using var repo = new AdapterRepository();
        repo.WriteSources();
        repo.Write(".agents/roles/explorer/instructions.md", "读取 C:\\new\\file\n返回结果\n");

        Assert.True(repo.Synchronizer.Sync().IsValid);

        Assert.Contains("读取 C:\\\\new\\\\file\n返回结果\n", repo.Read(".codex/agents/explorer.toml"));
    }

    [Fact]
    public void 只读检查允许内容一致的只读受管文件()
    {
        using var repo = new AdapterRepository();
        repo.WriteSources();
        Assert.True(repo.Synchronizer.Sync().IsValid);
        var path = Path.Combine(repo.Root, ".codex/agents/explorer.toml");
        File.SetAttributes(path, FileAttributes.ReadOnly);
        try { Assert.True(repo.Synchronizer.Check().IsValid); }
        finally { File.SetAttributes(path, FileAttributes.Normal); }
    }

    [Fact]
    public void 清单无法写入时在写任何适配前失败()
    {
        using var repo = new AdapterRepository();
        repo.WriteSources();
        Assert.True(repo.Synchronizer.Sync().IsValid);
        repo.Write(".agents/roles/explorer/instructions.md", "新的指令\n");
        var before = repo.Snapshot();
        using var locked = new FileStream(Path.Combine(repo.Root, ".lyking-ai-adapters.json"), FileMode.Open, FileAccess.Read, FileShare.Read);

        var report = repo.Synchronizer.Sync();

        Assert.False(report.IsValid);
        Assert.Equal(before, repo.Snapshot());
    }

    [Theory]
    [InlineData("---\nname: sample-skill\ndescription: 说明: 细节\n---\n正文\n")]
    [InlineData("---\nname: sample-skill\ndescription: '引号'内部'\n---\n正文\n")]
    [InlineData("---\nname: sample-skill\ndescription: null\n---\n正文\n")]
    [InlineData("---\nname: sample-skill\ndescription: true\n---\n正文\n")]
    [InlineData("---\nname: sample-skill\ndescription: 123\n---\n正文\n")]
    [InlineData("---\nname: sample-skill\ndescription: # 只有注释\n---\n正文\n")]
    public void 无效单行标量不能成为技能前置区(string source)
    {
        using var repo = new AdapterRepository();
        repo.WriteSources();
        repo.Write(".agents/skills/sample-skill/SKILL.md", source);

        Assert.False(repo.Synchronizer.Sync().IsValid);
        Assert.False(File.Exists(Path.Combine(repo.Root, ".lyking-ai-adapters.json")));
    }

    [Fact]
    public void 无效编码不会生成适配()
    {
        using var repo = new AdapterRepository();
        repo.WriteSources();
        File.WriteAllBytes(Path.Combine(repo.Root, ".agents/roles/explorer/instructions.md"), [0xff, 0xfe, 0x80]);
        var before = repo.Snapshot();

        var report = repo.Synchronizer.Sync();

        Assert.False(report.IsValid);
        Assert.Contains(report.Issues, issue => issue.Code == "source.encoding");
        Assert.Equal(before, repo.Snapshot());
    }

    [Theory]
    [InlineData(".")]
    [InlineData(".agents")]
    [InlineData("../外部目录")]
    public void 适配输出目录不能使用仓库根目录真源或外部目录(string path)
    {
        using var repo = new AdapterRepository();
        repo.WriteSources();
        repo.Directories = repo.Directories with { ClaudeSkills = path };
        var before = repo.Snapshot();

        Assert.False(repo.Synchronizer.Sync().IsValid);
        Assert.Equal(before, repo.Snapshot());
    }

    private static string Digest(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    private sealed class AdapterRepository : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "lyking-ai-adapter-tests", Guid.NewGuid().ToString("N"));
        public AdapterDirectories Directories { get; set; } = new(".claude/skills", ".codex/agents", ".claude/agents", ".gemini/agents", ".lyking-ai-adapters.json");
        public AdapterSynchronizer Synchronizer => new(Root, new WorkflowConfig
        {
            SchemaVersion = 1,
            WorkflowVersion = "1.0",
            Areas = [],
            RiskRules = [],
            Profiles = [],
            Changes = new("docs/changes/active", "docs/changes/archive"),
            ExcludedPaths = [],
            Adapters = Directories
        });

        public AdapterRepository() => Directory.CreateDirectory(Root);
        public void Write(string path, string text)
        {
            var full = Path.Combine(Root, path);
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllText(full, text, new UTF8Encoding(false));
        }
        public string Read(string path) => File.ReadAllText(Path.Combine(Root, path));
        public byte[] Bytes(string path) => File.ReadAllBytes(Path.Combine(Root, path));
        public void WriteSources()
        {
            Write(".agents/skills/sample-skill/SKILL.md", "---\nname: sample-skill\ndescription: 当需要验证示例时使用。\n---\n\n# 示例技能\n\n读取输入并返回证据。\n");
            WriteRole("explorer", "read-only", "只读探索者", "只读定位并返回证据。\n");
            WriteRole("implementer", "write", "实现者", "按授权范围实施并验证。\n");
            WriteRole("reviewer", "read-only", "审查者", "只读审查并报告风险。\n");
        }
        public void WriteRole(string name, string mode, string description, string instructions)
        {
            Write($".agents/roles/{name}/role.json", JsonSerializer.Serialize(new { schemaVersion = 1, name, description, mode }));
            Write($".agents/roles/{name}/instructions.md", instructions);
        }
        public string[] Snapshot() => Directory.EnumerateFiles(Root, "*", SearchOption.AllDirectories)
            .Order(StringComparer.Ordinal).Select(path => $"{Path.GetRelativePath(Root, path)}|{Digest(File.ReadAllBytes(path))}|{File.GetLastWriteTimeUtc(path).Ticks}").ToArray();
        public void Dispose()
        {
            var parent = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "lyking-ai-adapter-tests")) + Path.DirectorySeparatorChar;
            if (Path.GetFullPath(Root).StartsWith(parent, StringComparison.OrdinalIgnoreCase)) Directory.Delete(Root, true);
        }
    }
}
