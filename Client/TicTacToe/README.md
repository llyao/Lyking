# Unity 井字棋

Unity 2022.3 LTS 的人机对战井字棋，复用本仓库既有分层：核心规则在 `Assets/Scripts/Core/` 中保持纯 C#（无 Unity 依赖、无随机数、完全确定），Unity 层只负责鼠标点击采集、AI 回合驱动与 IMGUI 呈现。打开 `Assets/Scenes/TicTacToeDemo.unity` 进入 Play 模式即可游玩：点击 3x3 棋盘任意空格落子（玩家执 X 先手），AI 执 O 用 minimax 立即应答；`R` 键重开一局并保留比分累计；任一方连成三连即胜，棋满无胜为平局。

## 结构

- `Assets/Scripts/Core/`：纯规则（棋盘状态机、规则常量与胜利线、minimax AI、事件日志、对局与比分），`noEngineReferences` 程序集。
- `Assets/Scripts/Unity/`：`TicTacToeDemoBootstrap`（装配、按键与点击映射、AI 回合驱动）、`TicTacToeDemoPanel`（IMGUI 棋盘、比分、终局提示与事件日志）。
- `Assets/Tests/EditMode/`、`Assets/Tests/PlayMode/`：Unity 测试工程。
- `Core/`、`Tests/`：dotnet 侧纯逻辑工程与 xunit 测试（与 Unity asmdef 并存，源码引用同一批文件）。

## 玩法要素

- 3x3 棋盘共 8 条胜利线；AI 用 minimax 深度折扣评分（胜 +10-深度、负 -10+深度、平 0）应答，并列最优取最小索引，完美防守下玩家至多逼平。
- 顶部比分累计玩家胜/AI 胜/平局并跨局保留；底部滚动显示最近对局事件（落子、胜负、平局、重开）。
- 终局后棋盘保持终局形状并弹出提示，按 R 再来一局；非法落点（越界、已占、终局、非玩家回合）由核心直接拒绝。

## 纯逻辑验证

```powershell
dotnet build Client/TicTacToe/Lyking.TicTacToe.slnx
dotnet test Client/TicTacToe/Tests/Lyking.TicTacToe.Core.Tests.csproj
```

测试覆盖胜负判定（行/列/双对角线）、平局、占位与终局拒绝，并穷举玩家全部开局与后续组合验证 AI 不败。Unity EditMode/PlayMode 测试需在 Unity 2022.3 LTS 编辑器中运行验证；界面与交互保存真实运行截图。音效、皮肤、双人本地对战、难度分级与网络对战不在第一版范围内。
