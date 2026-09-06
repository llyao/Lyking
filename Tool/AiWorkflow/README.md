# 人工智能工作流工具

独立 .NET 10 命令行工具，从仓库根目录运行。具体 SDK 由根目录 `global.json` 固定，不依赖 Unity 编辑器。

## 构建与测试

```powershell
dotnet restore Tool/AiWorkflow/Lyking.AiWorkflow.slnx
dotnet build Tool/AiWorkflow/Lyking.AiWorkflow.slnx --configuration Release --no-restore
dotnet test Tool/AiWorkflow/Tests/Lyking.AiWorkflow.Tests.csproj --configuration Release --no-build
```

## 命令

```powershell
dotnet run --project Tool/AiWorkflow/Lyking.AiWorkflow.csproj -- route --text "修改共享登录协议" --path Share/Protocol/Login.cs
dotnet run --project Tool/AiWorkflow/Lyking.AiWorkflow.csproj -- validate --change docs/changes/active/20260906-ai-workflow-v1
dotnet run --project Tool/AiWorkflow/Lyking.AiWorkflow.csproj -- sync-adapters
dotnet run --project Tool/AiWorkflow/Lyking.AiWorkflow.csproj -- sync-adapters --check
dotnet run --project Tool/AiWorkflow/Lyking.AiWorkflow.csproj -- check-workspace --change docs/changes/active/20260906-ai-workflow-v1
```

脚本需要纯 JSON 输出时先构建，再直接执行 `dotnet Tool/AiWorkflow/bin/Release/net10.0/Lyking.AiWorkflow.dll` 并附加上述子命令，避免构建日志混入输出。

| 退出码 | 含义 |
|---|---|
| `0` | 成功且无阻塞 |
| `2` | 参数无效 |
| `3` | 输入、配置或路径无效 |
| `4` | 验证失败或适配漂移 |
| `5` | Git 工作区阻塞 |
| `10` | 未预期内部错误，详细异常输出到标准错误 |

`route` 只根据显式路径确定领域，自由文本保留为候选；`validate` 不修改状态、不执行测试、不联网，也不替代证据真实性与新鲜度核验。状态转换由调用者写入目标元数据后通过 `--transition 起点:目标` 检查；失败必须恢复状态。检查没有建立历史状态数据库，不能证明调用者提供的起点真实。

`check-workspace` 只读报告；范围外未暂存文件警告，范围外已暂存文件阻塞。`sync-adapters` 仅写受管平台文件；第一次生成遇到同名非受管文件会拒绝覆盖。

规则与边界见[工作流](../../docs/ai/workflow.md)、[验证策略](../../docs/ai/verification.md)、[平台适配](../../docs/ai/platforms.md)。
