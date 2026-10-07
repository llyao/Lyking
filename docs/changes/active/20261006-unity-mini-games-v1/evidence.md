# 验证证据

## 2026-10-06

### 路由与范围

- 命令：`dotnet run --project Tool/AiWorkflow/Lyking.AiWorkflow.csproj -- route --text "用 Unity 写 10 个小型单机游戏项目（贪吃蛇、俄罗斯方块等）" --path "Client/"`
- 结果：主领域 `Client`，`riskSignals` 为空，建议档位 `standard`。
- 范围仅新增 10 个工程目录与本变更目录，另登记 `Client/README.md`、`Client/CONTEXT.md`；`git status` 核对其余脏文件（GasUnity、Share 等）均为任务开始前已存在，未触碰。

### 实现方式

- 以统一 Python 骨架生成器（会话临时目录，不入仓库）批量生成 10 个工程骨架：目录、`.gitignore`、`Packages/manifest.json`、`ProjectSettings`（含 `ProjectVersion.txt` 2022.3.40f1、`EditorBuildSettings.asset`）、`<游戏>Demo.unity` 场景（主相机 + Bootstrap 挂载对象 + RenderSettings）、4 个 asmdef、dotnet 侧 Core/Tests 工程与 `Lyking.<Game>.slnx`、全部 `.meta`（随机 GUID）。
- 首个工程贪吃蛇作为黄金模板由本会话直接实现并验证，其余 9 个工程按同一模板与规范实现，每项完成后各自真实执行 fix-meta、build、test 与结构检查。

### 核心逻辑测试（真实运行，2026-10-06 汇总复跑）

逐工程命令 `dotnet test Client/<Game>/Tests/Lyking.<Game>.Core.Tests.csproj`：

| 工程 | 结果 |
|---|---|
| Snake | 失败 0，通过 12 |
| Tetris | 失败 0，通过 15 |
| Breakout | 失败 0，通过 14 |
| FlappyBird | 失败 0，通过 12 |
| Pong | 失败 0，通过 13 |
| SpaceShooter | 失败 0，通过 13 |
| Sokoban | 失败 0，通过 19 |
| MemoryMatch | 失败 0，通过 13 |
| TicTacToe | 失败 0，通过 18 |
| EndlessRunner | 失败 0，通过 14 |

合计 143 个测试全部通过。过程中修正的实现缺陷包括：贪吃蛇初始蛇身可能越界（新增构造校验并改为头在右端）、无尽跑酷 `Reset` 漏置 `IsGameOver`、测试侧若干断言笔误；修正后均复跑全绿。

### 编译（真实运行）

逐工程 `dotnet build Client/<Game>/Lyking.<Game>.slnx`：10 个工程全部 `0 个警告`、`0 个错误`。

### 结构一致性烟测（真实运行）

一次性 Python 脚本（PyYAML 逐文档解析，存于会话临时目录，不入仓库）对每个工程执行八项检查：Assets 内文件与 meta 双向完整、项目内 GUID 唯一、场景 YAML 全部文档可解析、场景 MonoBehaviour `m_Script` guid 对应 Assets 内 `.cs.meta` 且组件 fileID 全部可解析、`EditorBuildSettings.asset` 场景路径与 guid 一致、`ProjectVersion.txt` 为 2022.3.40f1、asmdef 引用完整（Unity 引 Core、测试引 Core+Unity）、dotnet 侧工程与 slnx 引用齐全。

- 10 个工程全部输出 `OK`（例如 `OK Snake: 22 个 meta、场景 SnakeDemo 共 7 个 YAML 文档、4 个组件引用、GUID 22 个均唯一`）。
- 口径校准：该脚本对 `Client/Minesweeper`、`Client/Game2048`、`Client/DungeonFighter` 三个既有工程同样输出 `OK`（三者中 DungeonFighter 曾随仓库流程通过 Unity 导入验证），证明检查口径与可导入格式一致。

### GUID 一致性

各工程场景中 Bootstrap 脚本引用的 guid 均由骨架生成器在生成场景前预写入 `<游戏>DemoBootstrap.cs.meta`，fix-meta 对已有 meta 幂等跳过，场景引用与 meta 保持一致（由结构检查第 4 项与"场景未引用 Bootstrap 脚本"检查共同保证）。

### Unity 环境缺口

- 工程固定 Unity 2022.3.40f1。本机再次确认（与 20261004-game2048-v1 记录一致）：Unity Hub 默认安装目录仅 `6000.4.0f1` 且此前探测无有效许可证，2022.3.59f1 已不在本机。
- 因此本日未执行工程导入、EditMode/PlayMode 测试与真实运行截图；10 个工程的 Unity 侧测试代码（各 3-4 个 EditMode、2 个 PlayMode）已就绪，待主机具备 Unity 2022.3 编辑器后按 `Client/CONTEXT.md` 的命令执行。
