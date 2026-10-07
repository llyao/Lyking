# Unity 水体排序

Unity 2022.3 LTS 的单机水体排序（Water Sort Puzzle）小游戏，复用本仓库既有分层：核心规则在 `Assets/Scripts/Core/` 中保持纯 C#（无 Unity 依赖、完全确定性），Unity 层只负责按键采集与 IMGUI 呈现。打开 `Assets/Scenes/WaterSortDemo.unity` 进入 Play 模式即可游玩：左右方向键或 A/D 移动选择光标，回车选中光标所在瓶、再对另一瓶回车即倒入（数字键 1-7 可直接选瓶），`U` 撤销上一次倒水，`R` 重开本局；默认 5 种颜色 7 只瓶子（含 2 只空瓶），把每只瓶子都变成空瓶或同色满瓶即过关。

## 结构

- `Assets/Scripts/Core/`：纯规则（常量、事件日志、BFS 求解器、棋局状态机），`noEngineReferences` 程序集。
- `Assets/Scripts/Unity/`：`WaterSortDemoBootstrap`（装配、光标与按键映射、测试入口 `Select`/`Undo`/`ResetGame` 与核心属性）、`WaterSortDemoPanel`（IMGUI 瓶排、HUD、通关提示与事件日志）。
- `Assets/Tests/EditMode/`、`Assets/Tests/PlayMode/`：Unity 测试工程。
- `Core/`、`Tests/`：dotnet 侧纯逻辑工程与 xunit 测试（与 Unity asmdef 并存，源码引用同一批文件）。

## 玩法要素

- 谜题生成：颜色 1..N 各 4 层共 N×4 层，Fisher-Yates 洗牌后依序装入前 N 瓶并附 2 只空瓶；随机布局不保证可解，生成后用内置 BFS 求解器验证，不可解则换种重试（上限 64 次），同一种子生成的谜题完全一致。
- 倒水规则：源瓶非空、目标瓶未满且（目标为空或目标顶层与源顶层同色）时合法；一次倒出源顶部连续同色层，直到目标瓶满或源瓶该色层倒完；其余情形拒绝并记录事件。
- 两段式选择：第一次选中（高亮金边），再次点击同一瓶取消，选中其他瓶即尝试倒入，被拒时保持选中便于换目标重试。
- 每次成功倒水步数 +1 并入栈记录，`Undo` 按记录层数倒回并恢复步数；`Reset` 回到当前谜题初始布局并清空撤销栈。
- 全部瓶子要么为空要么同色满即通关，通关后拒绝继续倒水；完成瓶（同色满）在面板上画盖描金。
- 事件日志（新谜题、选中、取消、倒入带层数、拒绝、撤销、重置、通关）封顶 128 条，供面板展示与测试断言。

## 纯逻辑验证

```powershell
dotnet build Client/WaterSort/Lyking.WaterSort.slnx
dotnet test Client/WaterSort/Tests/Lyking.WaterSort.Core.Tests.csproj
```

Unity EditMode/PlayMode 测试与运行截图见根目录 `docs/changes/` 的证据记录。动画补间、关卡编辑器、音效、移动端触屏与网络对战不在第一版范围内。
