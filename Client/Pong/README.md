# Unity 乒乓对战

Unity 2022.3 LTS 的单机乒乓小游戏（玩家对 AI），复用本仓库既有分层：核心规则在 `Assets/Scripts/Core/` 中保持纯 C#（无 Unity 依赖、注入种子确定性随机），Unity 层只负责按键采集、按帧推进固定子步物理与 IMGUI 呈现。打开 `Assets/Scenes/PongDemo.unity` 进入 Play 模式即可游玩：W/S 或 ↑/↓ 移动左挡板，`R` 键重开；球出左/右边界对方得 1 分，先到 7 分获胜。

## 结构

- `Assets/Scripts/Core/`：纯规则（向量、规则常量、对局状态、事件日志、对局模拟），`noEngineReferences` 程序集。
- `Assets/Scripts/Unity/`：`PongDemoBootstrap`（装配、按键映射、物理推进、测试入口 `Step`）、`PongDemoPanel`（IMGUI 场地、比分、胜利提示与事件日志）。
- `Assets/Tests/EditMode/`、`Assets/Tests/PlayMode/`：Unity 测试工程。
- `Core/`、`Tests/`：dotnet 侧纯逻辑工程与 xunit 测试（与 Unity asmdef 并存，源码引用同一批文件）。

## 玩法要素

- 24x14 逻辑场地（Y 向上），左挡板由玩家控制（速度 10），右挡板为 AI：以限速 8 追踪来球、球远离时回中，目标差在 0.35 死区内停止防抖动；AI 速度低于极限球速的垂直分量，连续多拍后必被击败。
- 发球：得分后球重置到场心并朝失分方发出；发球仰角固定 0.35 弧度（垂直分量恒定），上下方向由注入种子随机。
- 反弹：上下墙反射取反 Y 分量；挡板反射角按命中位置偏离中心的比例偏转（中心水平回弹、边缘最大 60°，纯函数 `ReflectAngle`），每次挡板反弹球速 +5% 并夹在 18。
- 物理按 1/120 秒固定子步推进防穿透；事件日志（发球、挡板反弹、墙反弹、得分、胜利）封顶 128 条。
- 先到 7 分获胜（状态 Running/LeftWon/RightWon）；胜利后推进与重发均被拒绝，按 R 重开。

## 纯逻辑验证

```powershell
dotnet build Client/Pong/Lyking.Pong.slnx
dotnet test Client/Pong/Tests/Lyking.Pong.Core.Tests.csproj
```

Unity EditMode/PlayMode 测试与运行截图遵循 `Client/CONTEXT.md` 的验证入口。音效、双人本地对战、最高分持久化与触屏操作不在第一版范围内。
