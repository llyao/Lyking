# Unity 青蛙过河

Unity 2022.3 LTS 的单机青蛙过河小游戏，复用本仓库既有分层：核心规则在 `Assets/Scripts/Core/` 中保持纯 C#（无 Unity 依赖、注入种子确定性随机），Unity 层只负责按键采集、按固定子步长驱动车流与浮排以及 IMGUI 呈现。打开 `Assets/Scenes/FroggerDemo.unity` 进入 Play 模式即可游玩：方向键或 WASD 跳一格（取按下沿），`R` 键重开；穿越 4 条马路与 4 条河道，把对岸 5 个荷花位全部占满过关，被车撞、落水或被木排带出边界损失生命，生命用尽结束。

## 结构

- `Assets/Scripts/Core/`：纯规则（格坐标、方向、规则常量、滚动车道、事件日志、棋局模拟），`noEngineReferences` 程序集。
- `Assets/Scripts/Unity/`：`FroggerDemoBootstrap`（装配、按键映射、子步驱动、测试入口 `Step`）、`FroggerDemoPanel`（IMGUI 分色场地、车辆木排、荷花位、HUD、结束提示与事件日志）。
- `Assets/Tests/EditMode/`、`Assets/Tests/PlayMode/`：Unity 测试工程。
- `Core/`、`Tests/`：dotnet 侧纯逻辑工程与 xunit 测试（与 Unity asmdef 并存，源码引用同一批文件）。

## 玩法要素

- 13x13 场地：1 行起点草地、4 行马路、3 行中间安全带、4 行河道、1 行对岸 5 个荷花位（1、4、6、8、11 列）。
- 车辆与木排沿周长等于场宽的环形轨道匀速滚动，初始相位与整体起点方向由种子决定、相邻行方向交替；离散跳跃在帧首结算，连续滚动按 1/120 秒固定子步推进，同种子同输入结果完全一致。
- 马路格与车辆 AABB 相交即被撞，河道格未落上木排即溺水，站上木排随排平移、被带出左右边界即失命并回到起点；到达未占用荷花位 +200 分并回起点，5 格进度全满过关进入下一轮（全体速度 +15%、进度清零），生命 3 用尽游戏结束。

## 纯逻辑验证

```powershell
dotnet build Client/Frogger/Lyking.Frogger.slnx
dotnet test Client/Frogger/Tests/Lyking.Frogger.Core.Tests.csproj
```

Unity EditMode/PlayMode 测试需在 Unity 2022.3 编辑器中执行。计时器、关卡编辑、音效、触屏手势与网络对战不在第一版范围内。
