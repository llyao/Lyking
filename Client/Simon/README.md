# Unity 西蒙记忆

Unity 2022.3 LTS 的单机西蒙记忆（Simon Says）小游戏，复用本仓库既有分层：核心规则在 `Assets/Scripts/Core/` 中保持纯 C#（无 Unity 依赖、注入种子确定性生成序列），Unity 层只负责鼠标与键盘输入采集、按帧推进播放节奏与复述超时、IMGUI 呈现。打开 `Assets/Scenes/SimonDemo.unity` 进入 Play 模式即可游玩：首次点击任意色块（或按数字 1-4 / 方向键）开局，核心按 0.4 秒点亮 + 0.2 秒间隔的节奏逐个播放本轮序列，播放结束后进入复述，按同样顺序点击色块，全对即升到下一轮（序列多一项），按错或 5 秒内未输满即失败；`R` 键重开，最长纪录跨局保留。等级无上限。

## 结构

- `Assets/Scripts/Core/`：纯规则（规则常量、事件日志、对局状态机），`noEngineReferences` 程序集。
- `Assets/Scripts/Unity/`：`SimonDemoBootstrap`（装配、键盘输入采集、节奏与超时推进、测试入口 `Step`/`Press`）、`SimonDemoPanel`（IMGUI 2x2 四色大块、状态栏、失败提示与事件日志）。
- `Assets/Tests/EditMode/`、`Assets/Tests/PlayMode/`：Unity 测试工程。
- `Core/`、`Tests/`：dotnet 侧纯逻辑工程与 xunit 测试（与 Unity asmdef 并存，源码引用同一批文件）。

## 玩法要素

- 2x2 四色大块（红、绿、蓝、黄），序列由注入种子的 `Random` 逐项 `Next(4)` 预生成 128 项封顶，同种子对局完全一致；第 N 轮固定取前 N 项，任意轮与前一轮共享前缀。
- 状态机：Ready（首次输入开局）→ Showing（按节奏逐个点亮，核心暴露 `LitPadIndex` 与 `ShowingRemaining`）→ Input（逐项比对，限时 5 秒）→ GameOver；复述全对升等级回 Showing，按错或超时记失败并刷新最长纪录。
- 开局、播放开始（带等级）、点亮、复述正确（带进度）、复述错误（带正确答案）、失败、新纪录与重置均写入事件日志，日志封顶 128 条。
- 无物理引擎、无音频；面板仅用 `Texture2D.whiteTexture` 纯色块与 IMGUI，复述点击有闪白反馈。

## 纯逻辑验证

```powershell
dotnet build Client/Simon/Lyking.Simon.slnx
dotnet test Client/Simon/Tests/Lyking.Simon.Core.Tests.csproj
```

Unity EditMode/PlayMode 测试需在 Unity 编辑器（固定 2022.3.40f1）中运行；运行 `Assets/Scenes/SimonDemo.unity` 后保存真实截图作为界面与交互证据。音效、计分排行榜、难度档位与触屏适配不在第一版范围内。
