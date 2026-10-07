# Unity 打砖块

Unity 2022.3 LTS 的单机打砖块小游戏，复用本仓库既有分层：核心规则在 `Assets/Scripts/Core/` 中保持纯 C#（无 Unity 依赖、注入种子确定性随机），Unity 层只负责按键采集、按帧推进固定子步物理与 IMGUI 呈现。打开 `Assets/Scenes/BreakoutDemo.unity` 进入 Play 模式即可游玩：←→ 或 A/D 移动挡板，`空格` 发球，`R` 键重开；击碎 8 列 × 5 行砖阵得分，顶行砖需两次击破，球落底损失 1 命（共 3 命），清空全部砖块进入下一关且球速提升。

## 结构

- `Assets/Scripts/Core/`：纯规则（向量、包围盒、规则常量、事件日志、砖块与对局模拟），`noEngineReferences` 程序集。
- `Assets/Scripts/Unity/`：`BreakoutDemoBootstrap`（装配、按键映射、物理推进、测试入口 `Step`）、`BreakoutDemoPanel`（IMGUI 场地、HUD、提示与事件日志）。
- `Assets/Tests/EditMode/`、`Assets/Tests/PlayMode/`：Unity 测试工程。
- `Core/`、`Tests/`：dotnet 侧纯逻辑工程与 xunit 测试（与 Unity asmdef 并存，源码引用同一批文件）。

## 玩法要素

- 20x15 逻辑场地（Y 向上），挡板贴底左右移动，球以恒定模长飞行并随关卡缓增（8 起步、每关 +1.2、上限 16）。
- 挡板反射角按命中位置偏离中心的比例偏转（中心竖直上弹、边缘最大 60°），球速含最小垂直分量防止水平死循环。
- 8 列 × 5 行砖阵：顶行耐久 2 次击破，行分值自顶向下 20/15/10/10/5，受击未碎的耐久砖在面板上变浅显示。
- 物理按 1/120 秒固定子步推进防穿透；球与挡板、砖块按圆对 AABB 最近点碰撞，反射轴取穿透最小轴。
- 3 条生命；球落底或过关后球贴回挡板，按发球键再次发射；事件日志封顶 128 条。

## 纯逻辑验证

```powershell
dotnet build Client/Breakout/Lyking.Breakout.slnx
dotnet test Client/Breakout/Tests/Lyking.Breakout.Core.Tests.csproj
```

Unity EditMode/PlayMode 测试与运行截图遵循 `Client/CONTEXT.md` 的验证入口。音效、道具、多球、最高分持久化与触屏操作不在第一版范围内。
