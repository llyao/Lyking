# Unity 打地鼠

Unity 2022.3 LTS 的单机限时计分打地鼠小游戏，复用本仓库既有分层：核心规则在 `Assets/Scripts/Core/` 中保持纯 C#（无 Unity 依赖、注入种子确定性调度），Unity 层只负责鼠标与键盘输入采集、按帧推进倒计时与 IMGUI 呈现。打开 `Assets/Scenes/WhackAMoleDemo.unity` 进入 Play 模式即可游玩：首次点击地洞或按数字键 1-9 开始对局，冒头地鼠分普通（金色，+100 基础分）与炸弹（深色带红色引信，−200 且总分夹 0 下限），命中普通地鼠按当前连击数追加 +20×连击数（上限 +100）的加成，打空连击清零；地鼠到时未打自动缩回，冒头间隔 0.4~1.0 秒、停留时长随剩余时间从 1.2 秒线性缩短至 0.5 秒。倒计时 30 秒归零即结束，`R` 键随时重开。

## 结构

- `Assets/Scripts/Core/`：纯规则（规则常量、事件日志、对局），`noEngineReferences` 程序集。
- `Assets/Scripts/Unity/`：`WhackAMoleDemoBootstrap`（装配、打击入口、倒计时推进、测试入口 `Step`）、`WhackAMoleDemoPanel`（IMGUI 地洞网格、冒头地鼠、命中闪白、HUD、结束提示与事件日志）。
- `Assets/Tests/EditMode/`、`Assets/Tests/PlayMode/`：Unity 测试工程。
- `Core/`、`Tests/`：dotnet 侧纯逻辑工程与 xunit 测试（与 Unity asmdef 并存，源码引用同一批文件）。

## 玩法要素

- 3x3 共 9 个地洞，同屏最多 1 只地鼠；洞位、类型与冒头间隔由注入种子的 `Random` 依次抽签决定，同种子对局完全一致。
- 状态机 Ready → Running → GameOver：Ready 阶段倒计时不走，首次打击只负责开局；Running 阶段倒计时递减，命中即得分并立刻缩回，到时未打自动缩回；时间到写入时间到与结束事件，之后打击与推进均无效。
- 计分：普通地鼠 100 基础分 + 连击加成（20×连击数，上限 100）；炸弹地鼠 −200，总分夹 0 下限；打空或命中炸弹连击清零。
- 开始、冒头、命中、打空、命中炸弹、缩回、时间到与结束均写入事件日志，封顶 128 条后从最旧开始裁剪。

## 纯逻辑验证

```powershell
dotnet build Client/WhackAMole/Lyking.WhackAMole.slnx
dotnet test Client/WhackAMole/Tests/Lyking.WhackAMole.Core.Tests.csproj
```

Unity EditMode/PlayMode 测试需在 Unity 编辑器（固定 2022.3.40f1）中运行；运行 `Assets/Scenes/WhackAMoleDemo.unity` 后保存真实截图作为界面与交互证据。音效、连击特效动画、排行榜与触屏适配不在第一版范围内。
