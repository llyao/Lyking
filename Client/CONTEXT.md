# 游戏客户端上下文

## 职责

Unity 客户端、界面、交互、场景和运行时。

## 当前入口

[目录说明](README.md)。

## 必读资料

[上下文地图](../CONTEXT-MAP.md)、[工作流](../docs/ai/workflow.md)和[验证边界](../docs/ai/verification.md)。按实际影响读取相邻领域。

## 不属于本目录

服务端部署、跨端共享协议和美术源文件。

## 验证入口

使用编辑模式测试验证纯逻辑，运行模式测试验证场景和生命周期；界面与交互保存真实运行截图。

当前可执行验证（客户端基础框架纯逻辑，本会话真实运行过）：

```bash
dotnet build Client/GameFramework/Lyking.ClientFramework.slnx
dotnet test Client/GameFramework/Tests/Lyking.GameFramework.Client.Tests.csproj
dotnet run --project Client/GameFramework/Demo/Lyking.GameFramework.Client.Demo.csproj
```

Unity 适配层（`Client/GameFramework/Unity/`）与场景、生命周期、运行时集成仍需 Unity 编辑器验证。当前可执行 GAS 工程为 `Client/GasUnity/`，目标编辑器为 Unity 2022.3 LTS：

```powershell
dotnet test Client/GasUnity/Tests/Lyking.GameplayAbilities.Core.Tests.csproj
New-Item -ItemType Directory -Force Client/GasUnity/TestResults | Out-Null
Unity.exe -runTests -batchmode -nographics -projectPath Client/GasUnity -testResults Client/GasUnity/TestResults/editmode.xml -testPlatform EditMode
Unity.exe -runTests -batchmode -nographics -projectPath Client/GasUnity -testResults Client/GasUnity/TestResults/playmode.xml -testPlatform PlayMode
```

当前可执行扫雷工程为 `Client/Minesweeper/`，同样固定 Unity 2022.3 LTS：

```powershell
dotnet test Client/Minesweeper/Tests/Lyking.Minesweeper.Core.Tests.csproj
New-Item -ItemType Directory -Force Client/Minesweeper/TestResults | Out-Null
Unity.exe -runTests -batchmode -nographics -projectPath Client/Minesweeper -testResults Client/Minesweeper/TestResults/editmode.xml -testPlatform EditMode
Unity.exe -runTests -batchmode -nographics -projectPath Client/Minesweeper -testResults Client/Minesweeper/TestResults/playmode.xml -testPlatform PlayMode
```

Unity 小型游戏合集（`Client/Snake/`、`Tetris/`、`Breakout/`、`FlappyBird/`、`Pong/`、`SpaceShooter/`、`Sokoban/`、`MemoryMatch/`、`TicTacToe/`、`EndlessRunner/`，及第二批 `Client/Asteroids/`、`DoodleJumper/`、`Frogger/`、`BattleCity/`、`MazeChase/`、`Bomberman/`、`WhackAMole/`、`SlidingPuzzle/`、`LightsOut/`、`FloodIt/`、`Match3/`、`Sudoku/`、`WaterSort/`、`ConnectFour/`、`Reversi/`、`Gomoku/`、`Blackjack/`、`Wordle/`、`Simon/`、`GameOfLife/`）与上述工程同一结构，每个工程执行：

```powershell
dotnet build Client/Snake/Lyking.Snake.slnx
dotnet test Client/Snake/Tests/Lyking.Snake.Core.Tests.csproj
```

（将目录名替换即可；30 个工程合计 494 个纯逻辑测试，2026-10-06 真实运行全部通过。）其 Unity EditMode/PlayMode 测试同样需要 Unity 2022.3 编辑器执行，证据记录见 `docs/changes/active/20261006-unity-mini-games-v1/` 与 `docs/changes/active/20261006-unity-mini-games-v2/`。

工程固定在 Unity 2022.3.40f1；当前主机可用 Unity 2022.3.59f1。最近在临时副本中以 2022.3.59f1 执行 EditMode 与 PlayMode，均为 3/3 通过、编译 0 警告和 0 错误；此结果不证明固定补丁版本 2022.3.40f1 的导入差异，也未验证真实 `Space` 键盘输入。运行 `Client/GasUnity/Assets/Scenes/GasDemo.unity` 后仍需保存玩家 Mana、训练假人 Health、冷却拒绝和事件日志的真实截图。
