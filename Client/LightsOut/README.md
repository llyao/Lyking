# Unity 点亮灯光

Unity 2022.3 LTS 的单机点灯（Lights Out）小游戏，复用本仓库既有分层：核心规则在 `Assets/Scripts/Core/` 中保持纯 C#（无 Unity 依赖、注入种子确定性生成谜题），Unity 层只负责鼠标与键盘输入采集和 IMGUI 呈现。打开 `Assets/Scenes/LightsOutDemo.unity` 进入 Play 模式即可游玩：鼠标点击或方向键移动光标后按回车按下格位，格位自身与上下左右四邻同时翻转，把 5x5 全部灯熄灭即胜利；`R` 键重开、`N` 键换一题。

## 结构

- `Assets/Scripts/Core/`：纯规则（规则常量、事件日志、棋局），`noEngineReferences` 程序集。
- `Assets/Scripts/Unity/`：`LightsOutDemoBootstrap`（装配、键盘光标与输入采集、测试入口 `Step`）、`LightsOutDemoPanel`（IMGUI 灯格、状态栏、胜利提示与事件日志）。
- `Assets/Tests/EditMode/`、`Assets/Tests/PlayMode/`：Unity 测试工程。
- `Core/`、`Tests/`：dotnet 侧纯逻辑工程与 xunit 测试（与 Unity asmdef 并存，源码引用同一批文件）。

## 玩法要素

- 5x5 共 25 格，谜题由注入种子从全灭状态反向随机按下 8-15 下生成，必然可解且不超过该步数即可解，同种子谜题完全一致；构造支持 3 到 7 的棋盘边长。
- 按下一次翻转自身与上下左右四邻（越界邻居跳过），重复按下按异或叠加；全部熄灭即胜利，胜利后拒绝继续按下。
- 步数按有效按下统计；重置回谜题初始布局，换一题步数清零并递增谜题号；生成、按下（含翻转格数）、胜利、重置与换题均写入事件日志（封顶 128 条）。

## 纯逻辑验证

```powershell
dotnet build Client/LightsOut/Lyking.LightsOut.slnx
dotnet test Client/LightsOut/Tests/Lyking.LightsOut.Core.Tests.csproj
```

Unity EditMode/PlayMode 测试需在 Unity 编辑器（固定 2022.3.40f1）中运行；运行 `Assets/Scenes/LightsOutDemo.unity` 后保存真实截图作为界面与交互证据。音效、计分排行榜、关卡进度保存与触屏适配不在第一版范围内。
