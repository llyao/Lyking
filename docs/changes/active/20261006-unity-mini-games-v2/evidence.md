# 验证证据

## 2026-10-06

### 路由与范围

- 本批沿用第一批任务同日的 route 结论：主领域 `Client`，`riskSignals` 为空，档位 `standard`（`dotnet run --project Tool/AiWorkflow/Lyking.AiWorkflow.csproj -- route --text "用 Unity 写 20 个小型单机游戏项目" --path "Client/"`）。
- 范围仅新增 20 个工程目录与本变更目录，另登记 `Client/README.md`、`Client/CONTEXT.md`；`git status` 核对其余脏文件（GasUnity、Share 等）均为任务开始前已存在，未触碰。

### 实现方式

- 与第一批同一套骨架生成器（会话临时目录，不入仓库）批量生成 20 个工程骨架：目录、`.gitignore`、`Packages/manifest.json`、`ProjectSettings`（`ProjectVersion.txt` 2022.3.40f1、`EditorBuildSettings.asset`）、`<游戏>Demo.unity` 场景、4 个 asmdef、dotnet 侧 Core/Tests 工程与 `Lyking.<Game>.slnx`、全部 `.meta`（随机 GUID；场景引用的 Bootstrap guid 预写入 meta 保证一致）。
- 以第一批已交付的 10 个工程作为黄金模板（按玩法就近参考：街机参考 SpaceShooter/EndlessRunner、网格谜题参考 MemoryMatch/LightsOut、棋类参考 TicTacToe/ConnectFour、撤销类参考 Sokoban/WaterSort），逐个实现并各自真实执行 fix-meta、build、test 与结构检查；每批并发不超过 2-3 个实现代理。

### 核心逻辑测试（真实运行，2026-10-06 汇总复跑）

逐工程命令 `dotnet test Client/<Game>/Tests/Lyking.<Game>.Core.Tests.csproj`（独立复跑，非引用实现代理的自报）：

| 工程 | 结果 | | 工程 | 结果 |
|---|---|---|---|---|
| Asteroids | 通过 15 | | WaterSort | 通过 21 |
| DoodleJumper | 通过 17 | | ConnectFour | 通过 22 |
| Frogger | 通过 16 | | Reversi | 通过 20 |
| BattleCity | 通过 16 | | Gomoku | 通过 20 |
| MazeChase | 通过 15 | | Blackjack | 通过 19 |
| Bomberman | 通过 14 | | Wordle | 通过 18 |
| WhackAMole | 通过 16 | | Simon | 通过 15 |
| SlidingPuzzle | 通过 13 | | GameOfLife | 通过 23 |
| LightsOut | 通过 19 | | FloodIt | 通过 16 |
| Match3 | 通过 18 | | Sudoku | 通过 18 |

合计 351 个测试全部通过。过程中修复的实现缺陷包括：数独生成器挖洞前未把完整解复制进盘面（补 `Array.Copy` 后全绿）；水体排序求解器规范化键导致瓶序号错位（改为队列携带真实局面、规范键仅去重）；贪吃蛇式起点校验、跑酷 Reset 漏置终局标志等（详见各工程实现记录）。

### 编译（真实运行）

逐工程 `dotnet build Client/<Game>/Lyking.<Game>.slnx`：20 个工程全部 `0 个警告`、`0 个错误`。

### 结构一致性烟测（真实运行）

与第一批同一 Python 检查脚本（PyYAML 逐文档解析，存于会话临时目录，不入仓库）的八项检查，20 个工程全部输出 `OK`（例如 `OK BattleCity: 26 个 meta、场景 BattleCityDemo 共 7 个 YAML 文档、4 个组件引用、GUID 26 个均唯一`）。脚本口径已在本日第一批任务中用 Minesweeper、Game2048、DungeonFighter 三个既有工程对照校准。

### GUID 一致性

各工程场景中 Bootstrap 脚本引用的 guid 均由骨架生成器预写入 `<游戏>DemoBootstrap.cs.meta`，fix-meta 对已有 meta 幂等跳过（复跑 `created=0`），场景引用与 meta 保持一致（由结构检查第 4 项与"场景未引用 Bootstrap 脚本"检查共同保证）。

### Unity 环境缺口

- 工程固定 Unity 2022.3.40f1。本机再次确认（与 20261004-game2048-v1 及本日第一批记录一致）：Unity Hub 默认安装目录仅 `6000.4.0f1` 且此前探测无有效许可证，2022.3.59f1 已不在本机。
- 因此本日未执行工程导入、EditMode/PlayMode 测试与真实运行截图；20 个工程的 Unity 侧测试代码（各 3-4 个 EditMode、2 个 PlayMode）已就绪，待主机具备 Unity 2022.3 编辑器后按 `Client/CONTEXT.md` 的命令执行。
