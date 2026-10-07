# 游戏客户端

本目录用于存放游戏客户端的代码、配置和运行资源。

## 预计内容

- 客户端源代码
- 客户端配置和运行资源
- 界面与交互实现

## 客户端基础框架

[GameFramework/](GameFramework/README.md)：Unity 技术栈的客户端基础框架第一版，分层为 Unity 适配层 → 界面层 → 基础服务层 → 核心层；纯逻辑在 dotnet 范围测试，Unity 适配层需编辑器验证。

## GAS Unity 演示

`GasUnity/` 是 Unity 2022.3 LTS 的单机能力系统演示。核心能力在 `Assets/Scripts/Core/` 中保持纯 C#，Unity 层只负责输入、场景生命周期和只读事件面板。打开 `Assets/Scenes/GasDemo.unity` 后按 `Space` 施放火球：每次消耗 20 Mana，造成 25 点伤害，冷却 2 秒。

纯逻辑测试：

```powershell
dotnet test Client/GasUnity/Tests/Lyking.GameplayAbilities.Core.Tests.csproj
```

Unity EditMode/PlayMode 测试和截图验证见 `CONTEXT.md`。网络复制、预测回滚、存档、ECS 和 Addressables 不在第一版范围内。

## Unity 扫雷

`Minesweeper/` 是 Unity 2022.3 LTS 的经典扫雷游戏。核心规则在 `Assets/Scripts/Core/` 中保持纯 C#（首击安全布雷、洪水展开、旗格阻挡、胜负判定），Unity 层的 `MinesweeperDemoBootstrap` 负责棋盘生命周期、按键和计时，`MinesweeperDemoPanel` 用 IMGUI 绘制状态区、可点击网格和事件日志。打开 `Assets/Scenes/MinesweeperDemo.unity` 运行：左键翻开、右键插旗、`R` 重开、`1`/`2`/`3` 切换初级/中级/高级难度。

纯逻辑测试：

```powershell
dotnet test Client/Minesweeper/Tests/Lyking.Minesweeper.Core.Tests.csproj
```

Unity EditMode/PlayMode 测试见 `CONTEXT.md`。存档、排行榜、网络对战和自动求解器不在第一版范围内。

## Unity 小型游戏合集

以下 10 个工程与上述小游戏同一分层模式：核心规则在各自 `Assets/Scripts/Core/` 中保持纯 C#（确定性、注入种子随机），Unity 层用 `*DemoBootstrap` 装配与采集按键、`*DemoPanel` 用 IMGUI 绘制状态栏、游戏视图与事件日志；打开各自 `Assets/Scenes/<游戏名>Demo.unity` 进入 Play 模式即可游玩，`R` 键重开。设计、验证证据与范围见 `docs/changes/active/20261006-unity-mini-games-v1/`。

| 工程 | 玩法要点 |
|---|---|
| [Snake/](Snake/README.md) | 贪吃蛇：两步转向队列、吃食加长加速，撞墙撞身结束 |
| [Tetris/](Tetris/README.md) | 俄罗斯方块：7 种方块旋转踢墙、消行计分升级、软降硬降 |
| [Breakout/](Breakout/README.md) | 打砖块：挡板按命中位置偏转、耐久砖阵、3 命清版进关 |
| [FlappyBird/](FlappyBird/README.md) | 像素小鸟：重力扇翅、管道缺口随机、穿管计分 |
| [Pong/](Pong/README.md) | 乒乓人机：反射角按命中偏移、限速追踪 AI、先到 7 分胜 |
| [SpaceShooter/](SpaceShooter/README.md) | 太空射击：冷却射击、种子化敌人波次、3 命升波 |
| [Sokoban/](Sokoban/README.md) | 推箱子：5 个内置关卡、撤销与重置 |
| [MemoryMatch/](MemoryMatch/README.md) | 4x4 记忆翻牌：洗牌配对、不配对倒计时翻回 |
| [TicTacToe/](TicTacToe/README.md) | 井字棋人机：minimax AI（测试穷举断言 AI 不败） |
| [EndlessRunner/](EndlessRunner/README.md) | 无尽跑酷：跳跃二段跳快落、种子化障碍、距离计分 |

纯逻辑测试（每个工程同构）：

```powershell
dotnet test Client/Snake/Tests/Lyking.Snake.Core.Tests.csproj
```

10 个工程合计 143 个纯逻辑测试；Unity EditMode/PlayMode 测试与截图见 `CONTEXT.md` 与变更记录。音效、精灵动画、存档、最高分持久化、网络与触屏不在第一版范围内。

## Unity 小型游戏合集第二批

以下 20 个工程与上述小游戏同一分层模式与验证方式（纯 C# Core + IMGUI 面板，打开各自 `Assets/Scenes/<游戏名>Demo.unity` 进入 Play 即可游玩，`R` 重开）。设计、验证证据与范围见 `docs/changes/active/20261006-unity-mini-games-v2/`。

| 工程 | 类型 | 玩法要点 |
|---|---|---|
| [Asteroids/](Asteroids/README.md) | 街机 | 小行星：环形环绕、惯性漂移、三段碎裂、逐波提速 |
| [DoodleJumper/](DoodleJumper/README.md) | 街机 | 涂鸦跳跃：弹跳平台、易碎/移动平台、镜头跟随 |
| [Frogger/](Frogger/README.md) | 街机 | 青蛙过河：车流木排滚动、溺水撞车判负、荷花位过关 |
| [BattleCity/](BattleCity/README.md) | 街机 | 坦克大战：砖钢墙、子弹对消、基地防守、全歼过关 |
| [MazeChase/](MazeChase/README.md) | 街机 | 吃豆迷宫：转角缓冲、惊惧反杀幽灵、清豆过关 |
| [Bomberman/](Bomberman/README.md) | 街机 | 炸弹人：十字火焰炸墙、连锁引爆、寻出口 |
| [WhackAMole/](WhackAMole/README.md) | 街机 | 打地鼠：限时计分、连击加成、炸弹地鼠 |
| [SlidingPuzzle/](SlidingPuzzle/README.md) | 解谜 | 数字华容道：可解性打乱、撤销、多格滑入 |
| [LightsOut/](LightsOut/README.md) | 解谜 | 点灯：十字翻转、可解性保证生成 |
| [FloodIt/](FloodIt/README.md) | 解谜 | 色彩淹没：洪水填充扩张、步数限制 |
| [Match3/](Match3/README.md) | 解谜 | 三消：连锁倍率、重力填充、死局重排 |
| [Sudoku/](Sudoku/README.md) | 解谜 | 数独：回溯生成求解、三档难度、冲突提示 |
| [WaterSort/](WaterSort/README.md) | 解谜 | 水体排序：部分倒水、撤销、可解性验证 |
| [ConnectFour/](ConnectFour/README.md) | 棋类 | 四子连珠：四向四连、minimax 深度 4 AI |
| [Reversi/](Reversi/README.md) | 棋类 | 黑白棋：夹翻与过手、深度 3 minimax AI |
| [Gomoku/](Gomoku/README.md) | 棋类 | 五子棋：四向五连、启发式评分 AI |
| [Blackjack/](Blackjack/README.md) | 牌类 | 21 点：软硬点数、庄家规则、筹码管理 |
| [Wordle/](Wordle/README.md) | 文字 | 猜单词：两趟判定、词库校验、键盘染色 |
| [Simon/](Simon/README.md) | 记忆 | 西蒙：序列播放复述、最长纪录 |
| [GameOfLife/](GameOfLife/README.md) | 演示 | 生命游戏：B3/S23、滑翔枪图案、暂停单步 |

纯逻辑测试（每个工程同构）：

```powershell
dotnet test Client/Asteroids/Tests/Lyking.Asteroids.Core.Tests.csproj
```

20 个工程合计 351 个纯逻辑测试（2026-10-06 真实运行全部通过）；Unity EditMode/PlayMode 测试与截图见 `CONTEXT.md` 与变更记录。音效、精灵动画、存档、最高分持久化、网络与触屏不在第一版范围内。
