# Unity 俄罗斯方块

Unity 2022.3 LTS 的单机俄罗斯方块小游戏，复用本仓库既有分层：核心规则在 `Assets/Scripts/Core/` 中保持纯 C#（无 Unity 依赖、注入种子确定性随机），Unity 层只负责按键采集、按等级节拍驱动重力 Tick 与 IMGUI 呈现。打开 `Assets/Scenes/TetrisDemo.unity` 进入 Play 模式即可游玩：←→ 移动、↓ 软降、↑ 旋转、空格硬降，`R` 键重开；消行按行数计分并逐步升级加速，井口被堆叠堵死即结束。

## 结构

- `Assets/Scripts/Core/`：纯规则（井格坐标、方块偏移表、规则常量、事件日志、棋局模拟），`noEngineReferences` 程序集。
- `Assets/Scripts/Unity/`：`TetrisDemoBootstrap`（装配、按键映射、步进驱动、测试入口 `Step`）、`TetrisDemoPanel`（IMGUI 井体、HUD、Next 预览、结束提示与事件日志）。
- `Assets/Tests/EditMode/`、`Assets/Tests/PlayMode/`：Unity 测试工程。
- `Core/`、`Tests/`：dotnet 侧纯逻辑工程与 xunit 测试（与 Unity asmdef 并存，源码引用同一批文件）。

面板组件可在场景任意物体上挂载，`TetrisDemoBootstrap` 启动时通过 `FindObjectOfType` 自动查找并绑定；骨架场景未预置该组件时，可在 Play 前手工挂上 `TetrisDemoPanel`。

## 玩法要素

- 10x20 井，7 种四格方块（I/O/T/S/Z/J/L）各 4 个顺时针旋转态；旋转简化踢墙：原位、左 1、右 1、下 1，全部失败保持原朝向。
- 消行计分 1/2/3/4 行 = 100/300/500/800 × 等级；每消 10 行升一级，重力步进间隔 0.8 秒 × 0.85^(等级-1)，下限 0.1 秒。
- Next 预览队列由注入种子的 `System.Random` 驱动，核心亦支持注入固定方块序列供测试回放；新方块出场即与堆叠重叠则游戏结束，重叠形状保留用于展示。

## 纯逻辑验证

```powershell
dotnet build Client/Tetris/Lyking.Tetris.slnx
dotnet test Client/Tetris/Tests/Lyking.Tetris.Core.Tests.csproj
```

Unity EditMode/PlayMode 测试与运行截图遵循 `Client/CONTEXT.md` 的验证边界。音效、皮肤、最高分持久化、触屏手势与网络对战不在第一版范围内。
