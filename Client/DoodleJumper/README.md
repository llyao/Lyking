# Unity DoodleJumper（涂鸦跳跃）

Unity 2022.3 LTS 的单机涂鸦跳跃小游戏，复用本仓库既有分层：核心规则在 `Assets/Scripts/Core/` 中保持纯 C#（无 Unity 依赖、注入种子确定性随机），Unity 层只负责按键采集、按固定子步长推进与 IMGUI 呈现。打开 `Assets/Scenes/DoodleJumperDemo.unity` 进入 Play 模式即可游玩：左右方向键或 `A`/`D` 水平移动（带加速，松开减速），下方向键快落（重力放大 3 倍），`R` 键重开；玩家在有限宽度、向上无上限的场地内自动弹跳，踩上普通与移动平台即弹起，易碎平台踩上即碎不弹起，跌出坠落判定线即结束，分数等于历史最大爬升高度换算。

## 结构

- `Assets/Scripts/Core/`：纯规则（二维向量、事件日志、输入快照、平台快照、规则常量、棋局模拟），`noEngineReferences` 程序集。
- `Assets/Scripts/Unity/`：`DoodleJumperDemoBootstrap`（装配、按键映射、固定子步驱动、测试入口 `Step`）、`DoodleJumperDemoPanel`（IMGUI 镜头视野、三种平台、玩家、HUD、提示与事件日志）。
- `Assets/Tests/EditMode/`、`Assets/Tests/PlayMode/`：Unity 测试工程。
- `Core/`、`Tests/`：dotnet 侧纯逻辑工程与 xunit 测试（与 Unity asmdef 并存，源码引用同一批文件）。

## 玩法要素

- 场地逻辑宽度 16、高度向上无上限；玩家中心起点 (8, 0.5)、半宽半高 0.4/0.5；重力 20、弹跳速度 10（理论弹跳高度 2.5 米）、下落上限 20、快落倍率 3。
- 水平移动最大速度 4、加速度 24，默认夹边停在场界内（可手动指定环绕模式从另一侧出现）。
- 平台宽 2.4、厚 0.4；起始平台顶面为高度 0，向上按垂直间距 1.2~2.0（随高度每米 +0.02，上限 1.6/2.2，始终不超过可跳高度）生成；易碎与移动平台各占 25%。移动平台以振幅 2、角速度 π/2 往返，相位由生成序号确定，保持确定性。
- 只有下落时才与平台顶碰撞（扫掠区间判定，上升直接穿透）；普通平台永久、易碎平台踩上即碎不弹起、移动平台踩上照常弹起；平台生成到镜头上缘加前视余量 2 为止，完全落到镜头下缘再低一个厚度即移除。
- 镜头只升不降：上升时玩家保持在自屏幕下缘 1/3 高度附近；玩家底部低于历史最高高度减坠落窗口 8 即结束；分数 = 历史最大爬升高度（米）× 每米 10 分；弹起、踩碎、踩到移动平台、坠落与重置均写入封顶 128 条的事件日志。
- 驱动方按 1/120 秒固定子步长调用 `Step`，水平与快落均为持续输入，每个子步都生效，保证确定性。

## 纯逻辑验证

```powershell
dotnet build Client/DoodleJumper/Lyking.DoodleJumper.slnx
dotnet test Client/DoodleJumper/Tests/Lyking.DoodleJumper.Core.Tests.csproj
```

Unity EditMode/PlayMode 测试需在编辑器中执行。音效、皮肤、最高分持久化、触屏手势与网络对战不在第一版范围内。
