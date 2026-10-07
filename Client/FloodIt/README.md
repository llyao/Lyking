# Unity 色彩淹没

Unity 2022.3 LTS 的单机色彩淹没（Flood It）小游戏，复用本仓库既有分层：核心规则在 `Assets/Scripts/Core/` 中保持纯 C#（无 Unity 依赖、注入种子确定性取色），Unity 层只负责键盘与鼠标选色采集和 IMGUI 呈现。打开 `Assets/Scenes/FloodItDemo.unity` 进入 Play 模式即可游玩：14x14 棋盘铺满六种颜色，左上角的同色连通块为已淹没区（变亮显示），按数字键 `1`-`6` 或点击底部色块按钮选色，淹没区整体变成所选颜色并吞并相邻同色格；`R` 键重开、`N` 键换题。25 步内全盘同色即胜利，步数用尽仍未全盘同色即失败。

## 结构

- `Assets/Scripts/Core/`：纯规则（规则常量、事件日志、棋局），`noEngineReferences` 程序集。
- `Assets/Scripts/Unity/`：`FloodItDemoBootstrap`（装配、键盘选色入口、测试入口 `Step`）、`FloodItDemoPanel`（IMGUI 棋盘、色块按钮、HUD、终局提示与事件日志）。
- `Assets/Tests/EditMode/`、`Assets/Tests/PlayMode/`：Unity 测试工程。
- `Core/`、`Tests/`：dotnet 侧纯逻辑工程与 xunit 测试（与 Unity asmdef 并存，源码引用同一批文件）。

## 玩法要素

- 14x14 共 196 格、6 种颜色，布局由注入种子的 `Random` 逐格取色决定，同种子布局完全一致；棋盘边长支持 4 到 30，越界构造被拒绝。
- 淹没区定义为从左上角出发的同色连通块；选色时把淹没区整体改成所选颜色，再重算左上角连通块作为新的淹没区。
- 与当前淹没色相同的选择被拒绝且不计步，终局后的选色同样被拒绝；有效选色记 1 步，步数用尽未胜利即失败。
- 生成、选色（含扩张格数）、拒绝、胜利、失败与重置均写入事件日志，日志封顶 128 条。

## 纯逻辑验证

```powershell
dotnet build Client/FloodIt/Lyking.FloodIt.slnx
dotnet test Client/FloodIt/Tests/Lyking.FloodIt.Core.Tests.csproj
```

Unity EditMode/PlayMode 测试需在 Unity 编辑器（固定 2022.3.40f1）中运行；运行 `Assets/Scenes/FloodItDemo.unity` 后保存真实截图作为界面与交互证据。音效、计时排行榜、难度档位与触屏适配不在第一版范围内。
