# Unity 推箱子

Unity 2022.3 LTS 的单机推箱子小游戏，复用本仓库既有分层：核心规则在 `Assets/Scripts/Core/` 中保持纯 C#（无 Unity 依赖、完全确定性、无随机数），Unity 层只负责按键采集与 IMGUI 呈现。打开 `Assets/Scenes/SokobanDemo.unity` 进入 Play 模式即可游玩：方向键或 WASD 走一步（目标格是墙、箱子前方是墙或另一箱时被拒绝），`R` 重玩当前关，`N` 进入下一关（仅当前关全部箱子就位时生效），`Z` 撤销上一步；内置 5 关，把全部箱子推上目标点即过关。

## 结构

- `Assets/Scripts/Core/`：纯规则（坐标、方向、事件日志、关卡解析、内置关卡表、棋局状态机），`noEngineReferences` 程序集。
- `Assets/Scripts/Unity/`：`SokobanDemoBootstrap`（装配、按键映射、测试入口 `Move` 与核心属性）、`SokobanDemoPanel`（IMGUI 棋盘、HUD、通关提示与事件日志）。
- `Assets/Tests/EditMode/`、`Assets/Tests/PlayMode/`：Unity 测试工程。
- `Core/`、`Tests/`：dotnet 侧纯逻辑工程与 xunit 测试（与 Unity asmdef 并存，源码引用同一批文件）。

## 玩法要素

- 关卡字符：`#` 墙、`.` 目标、`$` 箱子、`*` 箱子在目标上、`@` 玩家、`+` 玩家在目标上、空格地板；行宽不一致、无玩家、箱子数与目标数不等的关卡在解析时直接抛异常。
- 每成功移动一步步数 +1，推动箱子另计推动数 +1；箱子全部在目标上即过关，最后一关通关显示全部通关。
- 每步记录撤销（玩家原位与被推箱子原位/新位），`Undo` 逐步恢复步数、推数与局面；`Reset` 回到当前关初始状态并清空撤销栈。
- 事件日志（切换关卡、移动、推箱、被拒、撤销、重置、过关）封顶 128 条，供面板展示与测试断言。

## 纯逻辑验证

```powershell
dotnet build Client/Sokoban/Lyking.Sokoban.slnx
dotnet test Client/Sokoban/Tests/Lyking.Sokoban.Core.Tests.csproj
```

Unity EditMode/PlayMode 测试与运行截图见根目录 `docs/changes/` 的证据记录。动画补间、关卡编辑器、音效、移动端触屏与网络对战不在第一版范围内。
