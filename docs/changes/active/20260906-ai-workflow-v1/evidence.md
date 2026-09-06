# 验证证据

## 验证命令

执行日期：2026-09-06 至 2026-09-07，最终自动验证日期为 2026-09-07（北京时间）。工作区为 `D:\Lyking\.worktrees\ai-workflow-v1`，分支为 `codex/ai-workflow-v1`。

```powershell
dotnet restore Tool/AiWorkflow/Lyking.AiWorkflow.slnx
dotnet build Tool/AiWorkflow/Lyking.AiWorkflow.slnx --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false
dotnet test Tool/AiWorkflow/Tests/Lyking.AiWorkflow.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --logger 'trx;LogFileName=人工智能工作流验证.trx' --logger 'console;verbosity=quiet'
dotnet Tool/AiWorkflow/bin/Release/net10.0/Lyking.AiWorkflow.dll route --text '修改共享登录协议' --path Share/Protocol/Login.cs
dotnet Tool/AiWorkflow/bin/Release/net10.0/Lyking.AiWorkflow.dll sync-adapters
dotnet Tool/AiWorkflow/bin/Release/net10.0/Lyking.AiWorkflow.dll sync-adapters --check
dotnet Tool/AiWorkflow/bin/Release/net10.0/Lyking.AiWorkflow.dll validate --change docs/changes/active/20260906-ai-workflow-v1
dotnet Tool/AiWorkflow/bin/Release/net10.0/Lyking.AiWorkflow.dll check-workspace --change docs/changes/active/20260906-ai-workflow-v1
```

## 验证结果

| 验证 | 结果 |
|---|---|
| SDK 与依赖 | .NET SDK 10.0.201；还原退出码 0 |
| 发布构建 | 退出码 0，零警告、零错误 |
| 最终完整测试 | 退出码 0；156 项通过，0 项失败，0 项跳过 |
| 领域路由 | 主领域 Share；建议 controlled；命中 shared-contract 和 network-protocol |
| 仓库验证 | 当前 active 变更退出码 0，无结构问题 |
| 适配同步 | 首次生成成功；重复同步的文件 SHA-256 和修改时间完全一致；检查退出码 0 |
| 工作区 | 任务分支、无阻塞；检查前后的 Git 零分隔状态完全相同 |
| 非法跳转 | draft:verified 返回退出码 4，报告非法边和目标状态不一致 |
| 编码与语言 | 严格 UTF-8 测试通过；项目文件字符扫描无禁止字符 |
| 自动换行检出 | 临时 Git 仓库开启 core.autocrlf，删除临时工作副本后重新检出，技能、角色和清单原始字节保持一致 |

完整测试结果位于本工作树 `Tool/AiWorkflow/Tests/TestResults/人工智能工作流验证.trx`，该生成目录由 Git 忽略。上述数量以实际测试进程的汇总为依据，未把空测试项目的退出码当作行为通过。

## 失败用例与修复证据

- 核心首批 40 项测试曾有 20 项失败，修复路径保护、空配置、固定元数据值、状态数字绕过、文本所有权和主路径优先后全部通过。
- Git 与参数首批 19 项测试曾有 16 项失败，接入真实只读 Git 检查、严格参数和驼峰 JSON 后全部通过。
- 变更验证测试先因实现缺失失败，完成工件、章节、链接、任务与审查门禁后通过。
- 适配生成器由独立执行上下文实现，草稿曾出现 29 项失败；最终 46 项专项测试通过，覆盖漂移、非受管文件、重解析点、只读文件、清单越界、编码和写入恢复。
- 六个技能的格式与边界契约最初全部失败，补齐各技能正文后逐项通过。这属于确定性结构检查，不是代理压力行为测试。
- 后续复核新增的 Git 元数据清单路径、矛盾审查结论、配置无效字节和风险前缀空项均先复现失败，再修复并纳入最终测试。
- 真实清单检出测试复现 Windows 默认 JSON 换行导致的漂移；清单序列化显式使用 LF 后，六项检出测试全部通过。测试删除临时副本后再检出，防止 Git 复用现有工作文件造成假通过。
- 仓库根与 main 未修改；改动保存在已存在的任务工作树。

## 人工验收

本次不包含游戏界面、美术、场景或交互改动，无需 Unity 视觉截图。游戏目录尚无实际可执行工程，未执行或声称执行 Unity 测试。

三平台版本已确认：Codex CLI 0.134.0、Claude Code 2.1.159、Gemini CLI 0.39.1。生成适配的本地格式与摘要通过自动检查，实际发现存在以下缺口：

| 平台 | 实际命令与结果 |
|---|---|
| Gemini | `gemini skills list` 退出码 0，但明确报告项目目录未受信任，跳过项目角色，列表中没有六个项目技能。命令成功不代表项目发现通过。 |
| Codex | `codex exec --ephemeral --sandbox read-only --color never` 配合只读发现提示；出现插件认证、模型缓存兼容及工具连接错误，未得到有效发现结果，终止检查，退出码 1。 |
| Claude | `claude -p --no-session-persistence --permission-mode plan` 配合只读发现提示；最终认证失败，HTTP 401，密钥无效，退出码 1。 |

没有修改用户级代理、认证或信任配置，没有推送、合并或归档。

## 尚未完成的验收

1. 任务九至十四要求的六组全新上下文压力场景，包含未加载技能的失败基线和加载后的原文对照，尚未执行。不得用结构测试或当前上下文自述替代。
2. 三个平台的实际技能与角色发现尚未全部通过；需要先解决上述认证、版本或目录信任环境问题。
3. 整体变更的最终独立审查尚未完成；适配实现子任务和本上下文复核不能冒充全量独立审查。
4. 因以上缺口，保持 active，不执行 active:verified，不归档。原计划的分步提交与行为测试顺序也没有全部按原样执行，任务清单只勾选已有对应证据的项目。
