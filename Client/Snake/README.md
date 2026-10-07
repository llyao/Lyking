# Unity 贪吃蛇

Unity 2022.3 LTS 的单机贪吃蛇小游戏，复用本仓库既有分层：核心规则在 `Assets/Scripts/Core/` 中保持纯 C#（无 Unity 依赖、注入种子确定性随机），Unity 层只负责按键采集、按步进节奏驱动 Tick 与 IMGUI 呈现。打开 `Assets/Scenes/SnakeDemo.unity` 进入 Play 模式即可游玩：方向键或 WASD 转向（输入队列缓存两步、忽略原地掉头），`R` 键重开；吃食物加长加分并逐步加速，撞墙或撞到自己的身体即结束。

## 结构

- `Assets/Scripts/Core/`：纯规则（棋盘、方向、规则常量、事件日志、棋局模拟），`noEngineReferences` 程序集。
- `Assets/Scripts/Unity/`：`SnakeDemoBootstrap`（装配、按键映射、步进驱动、测试入口 `Step`）、`SnakeDemoPanel`（IMGUI 棋盘、HUD、结束提示与事件日志）。
- `Assets/Tests/EditMode/`、`Assets/Tests/PlayMode/`：Unity 测试工程。
- `Core/`、`Tests/`：dotnet 侧纯逻辑工程与 xunit 测试（与 Unity asmdef 并存，源码引用同一批文件）。

## 玩法要素

- 24x17 棋盘，初始长度 3，每食 +10 分；步进间隔随进食线性缩短（0.14 秒起步，下限 0.06 秒）。
- 方向输入队列最多缓存 2 个转向，相同与原地掉头方向被忽略，快速连按不会自撞。
- 蛇占满全部棋盘格显示通关；死亡瞬间蛇身保持完整形状用于展示。

## 纯逻辑验证

```powershell
dotnet build Client/Snake/Lyking.Snake.slnx
dotnet test Client/Snake/Tests/Lyking.Snake.Core.Tests.csproj
```

Unity EditMode/PlayMode 测试与运行截图见根目录 `docs/changes/active/20261006-unity-mini-games-v1/` 的证据记录。音效、皮肤、关卡、最高分持久化、触屏手势与网络对战不在第一版范围内。
