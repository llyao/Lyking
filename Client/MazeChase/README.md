# Unity 吃豆迷宫

Unity 2022.3 LTS 的单机吃豆迷宫小游戏，复用本仓库既有分层：核心规则在 `Assets/Scripts/Core/` 中保持纯 C#（无 Unity 依赖、注入种子确定性随机），Unity 层只负责按键采集、按固定子步长驱动 `Step` 与 IMGUI 呈现。打开 `Assets/Scenes/MazeChaseDemo.unity` 进入 Play 模式即可游玩：方向键或 WASD 给出意向方向（格点对齐时转向，支持转角缓冲与通道内反向），`R` 键重开；吃豆计分，能量豆进入惊惧期可反吃幽灵，清空全部豆子过关提速 10% 再来，被普通幽灵抓住失命回出生点，3 条生命耗尽即结束。

## 结构

- `Assets/Scripts/Core/`：纯规则（地图解析、方向、规则常量、事件日志、棋局模拟与幽灵决策纯函数），`noEngineReferences` 程序集。
- `Assets/Scripts/Unity/`：`MazeChaseDemoBootstrap`（装配、按键映射、固定子步推进、测试入口 `Step`）、`MazeChaseDemoPanel`（IMGUI 迷宫、HUD、结束提示与事件日志）。
- `Assets/Tests/EditMode/`、`Assets/Tests/PlayMode/`：Unity 测试工程。
- `Core/`、`Tests/`：dotnet 侧纯逻辑工程与 xunit 测试（与 Unity asmdef 并存，源码引用同一批文件）。

## 玩法要素

- 15x11 字符串地图（`#` 墙、`.` 豆子、`o` 能量豆、`P` 出生点、`G` 幽灵巢、空格为无豆通道），四角能量豆，中部双巢格，全部豆子互相连通。
- 玩家 4.5 格/秒、幽灵 4 格/秒连续移动（非离散跳格），在格点对齐时判定转向：意向可走即转、不可走维持原方向、墙前格点停住；通道中可立即反向。
- 能量豆惊惧 6 秒：惊惧期幽灵反向贪心逃离玩家且可被吃掉（200 分/只），被吃后回巢延迟 1.5 秒复活；普通期幽灵朝玩家贪心追击（不回头），距离并列由注入种子决定。
- 每豆 10 分、能量豆 50 分；清空全部豆过关，第 N 关整体速度 +10%（N-1 次）；3 条生命，被抓住失 1 命回出生点、幽灵回巢延迟复活。
- 事件日志记录吃豆、吃能量豆、惊惧开始/结束、吃幽灵、被抓、失命、过关与游戏结束，封顶 128 条自动裁剪。

## 纯逻辑验证

```powershell
dotnet build Client/MazeChase/Lyking.MazeChase.slnx
dotnet test Client/MazeChase/Tests/Lyking.MazeChase.Core.Tests.csproj
```

Unity EditMode/PlayMode 测试需 Unity 2022.3 编辑器执行（本工程固定 2022.3.40f1）；场景、面板与生命周期仍需在编辑器内以真实运行截图复核。音效、皮肤、多关卡地图、最高分持久化、触屏手势与网络对战不在第一版范围内。
