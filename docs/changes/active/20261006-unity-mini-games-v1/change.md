<!-- lyking-ai
{"schemaVersion":1,"id":"20261006-unity-mini-games-v1","profile":"standard","status":"active","areas":["Client"],"paths":["docs/changes/active/20261006-unity-mini-games-v1/","Client/Snake/","Client/Tetris/","Client/Breakout/","Client/FlappyBird/","Client/Pong/","Client/SpaceShooter/","Client/Sokoban/","Client/MemoryMatch/","Client/TicTacToe/","Client/EndlessRunner/"],"riskSignals":[]}
-->

# Unity 小型游戏合集（10 个独立工程）

## 解决什么问题

在客户端仓库中一次性补齐 10 个可独立打开、可完整游玩的 Unity 2022.3 小型游戏工程，全部沿用本仓库既定分层（`Assets/Scripts/Core/` 纯 C# 规则、`Assets/Scripts/Unity/` 引导与 IMGUI 呈现、三层测试工程、dotnet 侧纯逻辑工程），作为 Game2048、Minesweeper、DungeonFighter 之后同一模式的小游戏系列。

## 十个工程与玩法

| 工程 | 玩法要点 |
|---|---|
| `Client/Snake/` | 24x17 贪吃蛇：两步转向队列防原地掉头、吃食加长加速、撞墙撞身结束 |
| `Client/Tetris/` | 10x20 俄罗斯方块：7 种方块 4 旋转态、简化踢墙、消行计分升级、软降硬降、Next 预览 |
| `Client/Breakout/` | 打砖块：挡板按命中位置偏转、8x5 砖阵含耐久、3 命、清版进关球速递增 |
| `Client/FlappyBird/` | 像素小鸟：重力与扇翅置顶速度、管道对缺口随机、穿管计分、触地即死 |
| `Client/Pong/` | 乒乓人机：挡板反射角按命中偏移、限速追踪 AI、每回合加速、先到 7 分胜 |
| `Client/SpaceShooter/` | 太空射击：冷却射击、种子化敌人波次（下落带正弦摆动）、AABB 命中、3 命升波 |
| `Client/Sokoban/` | 推箱子：5 个内置关卡（测试含 BFS 可解性断言）、推箱上目标、撤销与重置 |
| `Client/MemoryMatch/` | 4x4 记忆翻牌：种子化洗牌、两张判定、不配对倒计时翻回、全部配对胜利 |
| `Client/TicTacToe/` | 井字棋人机：minimax 深度折扣 AI，测试穷举全部对局断言 AI 不败 |
| `Client/EndlessRunner/` | 无尽跑酷：地面跳 + 二段跳 + 快落、种子化高矮障碍、距离计分提速 |

## 当前状态

10 个工程全部实现完成；dotnet 侧构建 0 警告 0 错误、纯逻辑测试 143 个全部通过、结构一致性检查全部通过。Unity 编辑器侧（导入、EditMode/PlayMode 测试、真实运行截图）因本机无可用 Unity 2022.3 编辑器（详见 evidence.md）未执行，与 Game2048/DungeonFighter 变更记录中的环境缺口一致。

## 完成后应看到什么

每个工程打开 `Assets/Scenes/<游戏名>Demo.unity` 进入 Play 模式即可游玩：顶部状态栏、中央游戏视图、底部事件日志，`R` 键重开；具体按键见各工程 README。纯规则全部在 `Assets/Scripts/Core/` 中，可用 dotnet 直接测试。

## 范围与非目标

- 范围：仅新增上述 10 个工程目录与本变更记录；更新 `Client/README.md` 与 `Client/CONTEXT.md` 的项目登记与验证入口。统一用 IMGUI 与纯代码装配，不引入场景资产。
- 非目标：不修改 `Client/GasUnity`、`Client/GameFramework`、`Share/`、`Server/` 等任何既有路径（工作区中的其他脏文件均为本任务开始前已存在）；不做音效、精灵动画、存档、最高分持久化、网络与移动端触屏适配。

## 验收方式

- 每个工程 `dotnet build Client/<Game>/Lyking.<Game>.slnx` 0 警告 0 错误；`dotnet test Client/<Game>/Tests/Lyking.<Game>.Core.Tests.csproj` 全绿（合计 143 个测试）。
- 一次性 Python 结构检查（8 项：meta 双向完整、GUID 唯一、场景 YAML 可解析、场景脚本/组件引用自洽、EditorBuildSettings 一致、ProjectVersion 2022.3.40f1、asmdef 引用、dotnet 工程齐全）对 10 个工程全部通过；口径已用 Minesweeper、Game2048、DungeonFighter 三个既有工程对照校准。
- Unity EditMode/PlayMode 测试与真实运行截图待主机具备 Unity 2022.3 编辑器后补充。

## 风险与回滚

主要风险与既有小游戏工程相同：Unity 内置资源 API 在补丁版本间的差异、手写场景 YAML 的导入兼容性；通过只使用稳定 API（IMGUI + `Texture2D.whiteTexture`）、复用已验证的场景模板与结构检查缓解。回滚时删除 10 个新工程目录与本变更记录即可，无共享路径影响。
