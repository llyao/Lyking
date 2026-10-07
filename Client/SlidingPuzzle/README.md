# Unity 数字华容道

Unity 2022.3 LTS 的单机数字华容道（滑块拼图）小游戏，复用本仓库既有分层：核心规则在 `Assets/Scripts/Core/` 中保持纯 C#（无 Unity 依赖、注入种子确定性打乱），Unity 层只负责键盘与鼠标输入采集、按帧累计计时与 IMGUI 呈现。打开 `Assets/Scenes/SlidingPuzzleDemo.unity` 进入 Play 模式即可游玩：方向键或 WASD 把该方向一侧的相邻滑块推入空位（按 ← 把空位右侧的滑块左移，空位等效向右移），鼠标点击与空位同行/同列的滑块一次滑入多格，数字键 3/4 在 3x3 与 4x4 间切换（默认 4x4），`Z` 撤销上一步，`R` 键重开。全部滑块归位且空位回到右下角即胜利。

## 结构

- `Assets/Scripts/Core/`：纯规则（规则常量、方向语义、事件日志、棋局），`noEngineReferences` 程序集。
- `Assets/Scripts/Unity/`：`SlidingPuzzleDemoBootstrap`（装配、输入采集、尺寸切换、测试入口 `Move`/`SlideTile`）、`SlidingPuzzleDemoPanel`（IMGUI 滑块网格、HUD、胜利提示与事件日志）。
- `Assets/Tests/EditMode/`、`Assets/Tests/PlayMode/`：Unity 测试工程。
- `Core/`、`Tests/`：dotnet 侧纯逻辑工程与 xunit 测试（与 Unity asmdef 并存，源码引用同一批文件）。

## 玩法要素

- 棋盘支持 3x3（编号 1-8）与 4x4（编号 1-15），行优先存放、0 表示空位；构造时即已解状态，开局由引导以 `Environment.TickCount` 为种子打乱。
- 打乱从已解状态反向随机行走（3x3 走 60 步、4x4 走 120 步，每步避免立即回退上一步），同种子布局完全一致，谜题可解性由构造保证。
- 每滑入一枚滑块记 1 步，一次多格滑动按滑入块数累计；滑动、非法拒绝、撤销、重置、打乱与胜利均写入事件日志（封顶保留最近 128 条）。
- 面板中已归位的滑块加亮边框，空位显示深色凹槽；顶部状态栏显示步数/尺寸/状态/耗时，底部滚动显示最近事件，还原成功后弹出胜利提示并停止计时。

## 纯逻辑验证

```powershell
dotnet build Client/SlidingPuzzle/Lyking.SlidingPuzzle.slnx
dotnet test Client/SlidingPuzzle/Tests/Lyking.SlidingPuzzle.Core.Tests.csproj
```

Unity EditMode/PlayMode 测试需在 Unity 编辑器（固定 2022.3.40f1）中运行；运行 `Assets/Scenes/SlidingPuzzleDemo.unity` 后保存真实截图作为界面与交互证据。音效、计分排行榜、最优解提示与触屏适配不在第一版范围内。
