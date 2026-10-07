# Unity 记忆翻牌

Unity 2022.3 LTS 的单机记忆翻牌配对小游戏，复用本仓库既有分层：核心规则在 `Assets/Scripts/Core/` 中保持纯 C#（无 Unity 依赖、注入种子确定性洗牌），Unity 层只负责鼠标点击采集、按帧推进翻回倒计时与 IMGUI 呈现。打开 `Assets/Scenes/MemoryMatchDemo.unity` 进入 Play 模式即可游玩：鼠标点击卡格翻开，一次翻两张，图案相同即配对消除，不同则短暂展示后自动翻回；`R` 键重开。8 对图案全部配对即胜利。

## 结构

- `Assets/Scripts/Core/`：纯规则（规则常量、事件日志、棋局），`noEngineReferences` 程序集。
- `Assets/Scripts/Unity/`：`MemoryMatchDemoBootstrap`（装配、翻牌入口、倒计时推进、测试入口 `Step`）、`MemoryMatchDemoPanel`（IMGUI 卡格、HUD、胜利提示与事件日志）。
- `Assets/Tests/EditMode/`、`Assets/Tests/PlayMode/`：Unity 测试工程。
- `Core/`、`Tests/`：dotnet 侧纯逻辑工程与 xunit 测试（与 Unity asmdef 并存，源码引用同一批文件）。

## 玩法要素

- 4x4 共 16 张卡、8 种图案各两张，布局由注入种子的 Fisher-Yates 洗牌决定，同种子布局完全一致。
- 翻第二张立即判定：同图案双双标记配对，异图案进入 1.0 秒翻回倒计时，倒计时内禁止翻第三张。
- 步数按翻第二张次数统计，全部 8 对配对即胜利；翻开、配对成功、配对失败、翻回与胜利均写入事件日志。

## 纯逻辑验证

```powershell
dotnet build Client/MemoryMatch/Lyking.MemoryMatch.slnx
dotnet test Client/MemoryMatch/Tests/Lyking.MemoryMatch.Core.Tests.csproj
```

Unity EditMode/PlayMode 测试需在 Unity 编辑器（固定 2022.3.40f1）中运行；运行 `Assets/Scenes/MemoryMatchDemo.unity` 后保存真实截图作为界面与交互证据。音效、计分排行榜、双人对战与触屏适配不在第一版范围内。
