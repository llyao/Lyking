# Unity 数独

Unity 2022.3 LTS 的单机数独小游戏，复用本仓库既有分层：核心规则在 `Assets/Scripts/Core/` 中保持纯 C#（无 Unity 依赖、注入种子确定性生成谜题），Unity 层只负责按键采集与 IMGUI 呈现。打开 `Assets/Scenes/SudokuDemo.unity` 进入 Play 模式即可游玩：方向键移动光标，数字键 1-9 在光标格填数，`0` 或退格清除玩家数，`R` 生成新谜题，`H` 提示一格，鼠标点击也可选格；给定格锁定不可覆盖，行列宫重复即时标红但不阻止填写，全盘填满且与解一致即通关。

## 结构

- `Assets/Scripts/Core/`：纯规则（规则常量与难度表、求解器、生成器、事件日志、棋局状态机），`noEngineReferences` 程序集。
- `Assets/Scripts/Unity/`：`SudokuDemoBootstrap`（装配、光标与按键映射、测试入口 `Fill`/`Clear`/`Hint`）、`SudokuDemoPanel`（IMGUI 棋盘、状态栏、完成提示与事件日志）。
- `Assets/Tests/EditMode/`、`Assets/Tests/PlayMode/`：Unity 测试工程。
- `Core/`、`Tests/`：dotnet 侧纯逻辑工程与 xunit 测试（与 Unity asmdef 并存，源码引用同一批文件）。

## 玩法要素

- 9x9 棋盘划分 3x3 宫；生成器先用注入种子的随机数回溯填满整盘（候选 1-9 Fisher-Yates 洗牌后按序尝试），再按难度随机挖洞：简单保留约 40 个提示、中等 32、困难 26（允许 ±2），每次挖洞后必须仍可解且解与原解一致，同种子谜题完全一致。
- 给定格锁定不可改；玩家数可填、可覆盖、可清除，行列宫重复计入冲突状态（面板标红）但不阻止填写；全盘填满且与生成解一致即通关，填满但存在冲突不判胜。
- `H` 提示在剩余空格中随机选中一格填入正确答案，永不产生冲突；重置清空全部玩家填数并清零步数；生成、填数、冲突、清除、提示、通关、重置均写入事件日志（封顶 128 条）。

## 纯逻辑验证

```powershell
dotnet build Client/Sudoku/Lyking.Sudoku.slnx
dotnet test Client/Sudoku/Tests/Lyking.Sudoku.Core.Tests.csproj
```

Unity EditMode/PlayMode 测试需在 Unity 编辑器（固定 2022.3.40f1）中运行；运行 `Assets/Scenes/SudokuDemo.unity` 后保存真实截图作为界面与交互证据。多难度切换菜单、笔记候选数、计时与排行榜、存档、触屏适配与网络对战不在第一版范围内。
