# Unity FlappyBird（像素小鸟）

Unity 2022.3 LTS 的单机 Flappy 小游戏，复用本仓库既有分层：核心规则在 `Assets/Scripts/Core/` 中保持纯 C#（无 Unity 依赖、注入种子确定性随机），Unity 层只负责点按采集、按固定子步长推进与 IMGUI 呈现。打开 `Assets/Scenes/FlappyBirdDemo.unity` 进入 Play 模式即可游玩：空格或鼠标左键点按扇翅（竖直速度置顶而非叠加），`R` 键重开；穿过管道缺口中心 +1 分，触地或撞上管道即结束，顶棚不死但上升速度清零。

## 结构

- `Assets/Scripts/Core/`：纯规则（二维向量、阶段与事件日志、规则常量、管道对、棋局模拟），`noEngineReferences` 程序集。
- `Assets/Scripts/Unity/`：`FlappyBirdDemoBootstrap`（装配、点按映射、固定子步驱动、测试入口 `Step`）、`FlappyBirdDemoPanel`（IMGUI 天空、管道、小鸟、HUD、提示与事件日志）。
- `Assets/Tests/EditMode/`、`Assets/Tests/PlayMode/`：Unity 测试工程。
- `Core/`、`Tests/`：dotnet 侧纯逻辑工程与 xunit 测试（与 Unity asmdef 并存，源码引用同一批文件）。

## 玩法要素

- 场地逻辑坐标 20x12（Y 向上，0 为地面），小鸟固定在 X=5、半宽半高 0.25；重力 20、扇翅置顶速度 6、下落上限 10。
- 管道宽 1、缺口高 3.5、水平间距 7、水平速度 3.5；缺口中心由注入种子在 3~9 内生成，首根管道在世界滚动 14 个单位后出现。
- 状态机 Ready（悬停等待首次点按）→ Running → GameOver；驱动方按 1/120 秒固定子步长调用 `Step`，本帧点按只作用于第一个子步，保证确定性。

## 纯逻辑验证

```powershell
dotnet build Client/FlappyBird/Lyking.FlappyBird.slnx
dotnet test Client/FlappyBird/Tests/Lyking.FlappyBird.Core.Tests.csproj
```

Unity EditMode/PlayMode 测试需在编辑器中执行。音效、皮肤、最高分持久化、触屏手势与网络对战不在第一版范围内。
