# Unity 黑白棋

Unity 2022.3 LTS 的人机对战黑白棋（Reversi/Othello），复用本仓库既有分层：核心规则在 `Assets/Scripts/Core/` 中保持纯 C#（无 Unity 依赖、无随机数、完全确定），Unity 层只负责光标按键与鼠标点击采集、AI 回合驱动与 IMGUI 呈现。打开 `Assets/Scenes/ReversiDemo.unity` 进入 Play 模式即可游玩：玩家执黑先手，方向键或 WASD 移动光标、回车在光标处落子，也可以直接鼠标点击棋盘落子；AI 执白用深度 3 的加权 minimax 立即应答；`R` 键重开一局并保留比分累计；一方无合法步时自动过手，双方均无步或棋盘下满即按盘面子数判定胜负。

## 结构

- `Assets/Scripts/Core/`：纯规则（棋盘状态机、规则常量与角/边权重表、夹击翻转计算、minimax AI、事件日志、对局与比分），`noEngineReferences` 程序集。
- `Assets/Scripts/Unity/`：`ReversiDemoBootstrap`（装配、光标与按键映射、AI 回合驱动、测试入口 `PlayAt`/`PlayAtCursor`/`MoveCursor`）、`ReversiDemoPanel`（IMGUI 棋盘、合法步提示、最后落子标记、光标高亮、比分、终局提示与事件日志）。
- `Assets/Tests/EditMode/`、`Assets/Tests/PlayMode/`：Unity 测试工程。
- `Core/`、`Tests/`：dotnet 侧纯逻辑工程与 xunit 测试（与 Unity asmdef 并存，源码引用同一批文件）。

## 玩法要素

- 8x8 标准开局（d4/e5 白子、d5/e4 黑子），初始合法步恰为 d3、c4、f5、e6 四步；每步必须至少翻转一枚对方棋子，越界、已占、无翻转或终局后的落子由核心直接拒绝并记录事件。
- 面板实时提示黑方合法步（黄点）、最后落子（橙框）与光标位置（青色高亮）；顶部显示累计比分与当前手，底部滚动最近事件（落子带翻转数、过手、终局、非法拒绝）。
- AI 搜索深度 3，叶子局面按角 100、边 10、其余 1 的加权子差评估，并列最优取行列字典序最小落点，完全确定；黑方无步时 AI 自动连续应答。
- 终局后棋盘保持终局形状并弹出提示，按 R 再来一局；比分跨局累计，重开只清空棋盘。

## 纯逻辑验证

```powershell
dotnet build Client/Reversi/Lyking.Reversi.slnx
dotnet test Client/Reversi/Tests/Lyking.Reversi.Core.Tests.csproj
```

测试覆盖规则常量与权重表、初始局面与合法步计算、单/多方向翻转、无翻转与占位拒绝、自动过手、双无步与棋满终局、AI 确定性与抢角、比分跨局累计、重开清盘保留比分与事件日志封顶裁剪。Unity EditMode/PlayMode 测试需在 Unity 2022.3 LTS 编辑器中运行验证；界面与交互保存真实运行截图。音效、皮肤、双人本地对战、难度分级与网络对战不在第一版范围内。
