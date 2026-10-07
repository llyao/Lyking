# Unity 康威生命游戏

Unity 2022.3 LTS 的康威生命游戏（Conway's Game of Life）技术演示，复用本仓库既有分层：核心规则在 `Assets/Scripts/Core/` 中保持纯 C#（无 Unity 依赖、无内建随机，随机撒子由注入种子确定性完成），Unity 层只负责键盘与鼠标输入采集和 IMGUI 呈现。打开 `Assets/Scenes/GameOfLifeDemo.unity` 进入 Play 模式即可运行：48x32 环形网格按 B3/S23 规则每 0.125 秒推进一代，活细胞按存活代数由亮绿渐变到蓝；`空格` 暂停/继续、`N` 单步、`C` 清空、`P` 载入滑翔机枪、`R` 随机重撒，鼠标按下翻转格位、拖拽连续绘制。

## 结构

- `Assets/Scripts/Core/`：纯规则（规则常量、事件日志、棋局、内置图案），`noEngineReferences` 程序集。
- `Assets/Scripts/Unity/`：`GameOfLifeDemoBootstrap`（装配、固定节拍推进与键盘输入采集、测试入口 `Step`）、`GameOfLifeDemoPanel`（IMGUI 网格、状态栏、事件日志与操作提示）。
- `Assets/Tests/EditMode/`、`Assets/Tests/PlayMode/`：Unity 测试工程。
- `Core/`、`Tests/`：dotnet 侧纯逻辑工程与 xunit 测试（与 Unity asmdef 并存，源码引用同一批文件）。

## 玩法要素

- B3/S23 规则：死细胞恰有 3 个邻居复活，活细胞有 2 或 3 个邻居存活，其余死亡；邻居计数在环形边界上跨到对侧。
- 棋盘默认 48x32（构造可调 4 到 256），双缓冲逐代推进；细胞记录存活代数，新生记 1，每多存活一代加 1，死亡清零。
- 内置图案：闪烁器（周期 2）、方块（静物）、滑翔机（每 4 代沿对角线平移一格）、滑翔机枪（36 细胞标准坐标，每 30 代发射一枚滑翔机）；载入时清空旧局面并按包围盒居中放置。
- 随机撒子由注入种子按行优先逐格判定，同种子同密度布局完全一致；密度默认 0.3，支持 0 到 1。
- 随机撒子、清空、载入图案与暂停/继续写入事件日志（封顶 128 条），逐代推进不记录。
- 暂停只停固定节拍（0.125 秒/代），`N` 键单步推进不受暂停限制。

## 纯逻辑验证

```powershell
dotnet build Client/GameOfLife/Lyking.GameOfLife.slnx
dotnet test Client/GameOfLife/Tests/Lyking.GameOfLife.Core.Tests.csproj
```

Unity EditMode/PlayMode 测试需在 Unity 编辑器（固定 2022.3.40f1）中运行；运行 `Assets/Scenes/GameOfLifeDemo.unity` 后保存真实截图作为界面与交互证据。
