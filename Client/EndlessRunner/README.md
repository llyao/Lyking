# Unity EndlessRunner（无尽跑酷）

Unity 2022.3 LTS 的单机无尽跑酷小游戏，复用本仓库既有分层：核心规则在 `Assets/Scripts/Core/` 中保持纯 C#（无 Unity 依赖、注入种子确定性随机），Unity 层只负责按键采集、按固定子步长推进与 IMGUI 呈现。打开 `Assets/Scenes/EndlessRunnerDemo.unity` 进入 Play 模式即可游玩：空格或上方向键跳跃（空中可二段跳，落地重置跳段），下方向键快落（重力放大 3 倍），`R` 键重开；玩家固定横坐标、世界向左滚动，撞上高矮两种障碍即结束，得分随滚动距离持续增长。

## 结构

- `Assets/Scripts/Core/`：纯规则（二维向量、事件日志、输入快照、障碍快照、规则常量、棋局模拟），`noEngineReferences` 程序集。
- `Assets/Scripts/Unity/`：`EndlessRunnerDemoBootstrap`（装配、按键映射、固定子步驱动、测试入口 `Step`）、`EndlessRunnerDemoPanel`（IMGUI 场地、障碍、玩家、HUD、提示与事件日志）。
- `Assets/Tests/EditMode/`、`Assets/Tests/PlayMode/`：Unity 测试工程。
- `Core/`、`Tests/`：dotnet 侧纯逻辑工程与 xunit 测试（与 Unity asmdef 并存，源码引用同一批文件）。

## 玩法要素

- 场地逻辑坐标 20x10（Y 向上，0 为地面），玩家固定在 X=4、半宽半高 0.4/0.5；重力 20、起跳初速 8、二段跳初速 5、下落上限 12、快落倍率 3。
- 障碍宽 0.8，高矮两档（0.6/1.2），由注入种子在生成线 X=20.4 处放置；相邻间距在 4~8 内随机，首个障碍在世界滚动 10 米后出现，越出左缘即移除。
- 滚动速度 5 米/秒起步、每米 +0.01，上限 9；分数 = 滚动距离（米）× 每米 10 分；跳跃、二段跳、快落、落地、越障、碰撞与重置均写入封顶 128 条的事件日志。
- 驱动方按 1/120 秒固定子步长调用 `Step`，本帧跳跃按下沿只作用于第一个子步，保证确定性。

## 纯逻辑验证

```powershell
dotnet build Client/EndlessRunner/Lyking.EndlessRunner.slnx
dotnet test Client/EndlessRunner/Tests/Lyking.EndlessRunner.Core.Tests.csproj
```

Unity EditMode/PlayMode 测试需在编辑器中执行。音效、皮肤、最高分持久化、触屏手势与网络对战不在第一版范围内。
