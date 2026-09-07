using System.Text.Json.Nodes;
using Lyking.AiWorkflow.Changes;
using Lyking.AiWorkflow.Configuration;
using Lyking.AiWorkflow.Core;
using Lyking.AiWorkflow.Routing;

namespace Lyking.AiWorkflow.Tests;

public class CoreBehaviorTests
{
    [Fact]
    public void 适配清单不能写入Git目录()
    {
        using var repo = new TestRepository(); repo.Configure(c => c["adapters"]!["manifest"] = ".git/新文件");
        Assert.Throws<WorkflowException>(() => WorkflowConfigLoader.Load(repo.Root));
    }
    [Fact]
    public void 风险前缀空项返回输入错误()
    {
        using var repo = new TestRepository(); repo.Configure(c => c["riskRules"]![0]!["pathPrefixes"] = new JsonArray((JsonNode?)null));
        Assert.Throws<WorkflowException>(() => WorkflowConfigLoader.Load(repo.Root));
    }
    [Fact]
    public void 配置无效字节不能被替换后接受()
    {
        using var repo = new TestRepository();
        var path = Path.Combine(repo.Root, ".lyking-ai.json");
        var text = File.ReadAllText(path).Replace("美术", "损坏");
        var bytes = System.Text.Encoding.UTF8.GetBytes(text);
        var index = Array.IndexOf(bytes, (byte)0xE6);
        bytes[index] = 0xFF;
        File.WriteAllBytes(path, bytes);
        Assert.Throws<WorkflowException>(() => WorkflowConfigLoader.Load(repo.Root));
    }
    [Theory]
    [InlineData("../外部")]
    [InlineData("/外部")]
    [InlineData("C:/外部")]
    [InlineData("C:外部")]
    [InlineData("Tool/../../外部")]
    [InlineData("Tool/资料:秘密")]
    public void 拒绝越界或特殊路径(string path)
    {
        using var repo = new TestRepository();
        Assert.Throws<WorkflowException>(() => PathPolicy.ResolveWithinRoot(repo.Root, path));
    }
    [Fact]
    public void 中文路径与混合分隔符可解析()
    {
        using var repo = new TestRepository();
        Assert.Equal(Path.Combine(repo.Root, "Tool", "中文", "文件.md"), PathPolicy.ResolveWithinRoot(repo.Root, "./Tool\\中文/文件.md"));
    }
    [Fact]
    public void 子目录可定位根配置()
    {
        using var repo = new TestRepository();
        Assert.Equal(repo.Root, RepositoryLocator.Find(Path.Combine(repo.Root, "Tool")));
    }
    [Theory]
    [InlineData("areas")]
    [InlineData("changes")]
    [InlineData("adapters")]
    [InlineData("riskRules")]
    [InlineData("profiles")]
    [InlineData("excludedPaths")]
    public void 空配置字段返回明确输入错误(string field)
    {
        using var repo = new TestRepository(); repo.Configure(c => c[field] = null);
        Assert.Throws<WorkflowException>(() => WorkflowConfigLoader.Load(repo.Root));
    }
    [Fact]
    public void 配置不能移除高风险保护规则()
    {
        using var repo = new TestRepository(); repo.Configure(c => c["riskRules"] = new JsonArray());
        Assert.Throws<WorkflowException>(() => WorkflowConfigLoader.Load(repo.Root));
    }
    [Theory]
    [InlineData("{\"schemaVersion\":1}")]
    [InlineData("{\"schemaVersion\":2}")]
    [InlineData("[]")]
    [InlineData("无效")]
    public void 拒绝错误元数据(string json)
    {
        Assert.Throws<WorkflowException>(() => ChangeDocumentParser.Parse("<!-- lyking-ai\n" + json + "\n-->", "示例.md"));
    }
    [Theory]
    [InlineData("profile", "0")]
    [InlineData("status", "100")]
    [InlineData("id", "")]
    [InlineData("status", "ACTIVE")]
    public void 元数据只接受固定字符串值(string field, string value)
    {
        var json = JsonNode.Parse("{\"schemaVersion\":1,\"id\":\"sample\",\"profile\":\"standard\",\"status\":\"active\",\"areas\":[\"Tool\"],\"riskSignals\":[]}")!;
        json[field] = value;
        Assert.Throws<WorkflowException>(() => ChangeDocumentParser.Parse("<!-- lyking-ai\n" + json + "\n-->"));
    }
    [Theory]
    [InlineData("draft", "ready", true)]
    [InlineData("ready", "active", true)]
    [InlineData("active", "blocked", true)]
    [InlineData("blocked", "active", true)]
    [InlineData("active", "verified", true)]
    [InlineData("verified", "archived", true)]
    [InlineData("archived", "active", false)]
    [InlineData("draft", "verified", false)]
    [InlineData("2", "4", false)]
    public void 状态只允许固定转换(string from, string to, bool expected) => Assert.Equal(expected, WorkflowStateMachine.CanTransition(from, to));
    [Theory]
    [InlineData("Art")]
    [InlineData("Client")]
    [InlineData("Server")]
    [InlineData("Share")]
    [InlineData("Tool")]
    public void 路径路由覆盖五领域(string area)
    {
        using var repo = new TestRepository();
        var result = new Router(repo.Root, WorkflowConfigLoader.Load(repo.Root)).Route("测试", [area + "/中文.md"]);
        Assert.Equal(area, result.PrimaryArea);
        Assert.Contains(area + "/CONTEXT.md", result.Contexts);
        Assert.Equal("CONTEXT-MAP.md", result.Contexts[0]);
    }
    [Fact]
    public void 文本不能冒充确定所有权()
    {
        using var repo = new TestRepository();
        var result = new Router(repo.Root, WorkflowConfigLoader.Load(repo.Root)).Route("服务端界面", []);
        Assert.Null(result.PrimaryArea);
        Assert.Equal(["CONTEXT-MAP.md"], result.Contexts);
    }
    [Fact]
    public void 第一条路径决定主领域()
    {
        using var repo = new TestRepository();
        var result = new Router(repo.Root, WorkflowConfigLoader.Load(repo.Root)).Route("", ["Share/Protocol/登录.cs", "Client/UI.cs"]);
        Assert.Equal("Share", result.PrimaryArea); Assert.Equal("controlled", result.SuggestedProfile);
    }
    [Fact]
    public void 路由拒绝外部路径()
    {
        using var repo = new TestRepository();
        Assert.Throws<WorkflowException>(() => new Router(repo.Root, WorkflowConfigLoader.Load(repo.Root)).Route("", ["../Share/协议.cs"]));
    }
}
