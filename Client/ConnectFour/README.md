# Unity 四子连珠

Unity 2022.3 LTS 的人机对战四子连珠，复用本仓库既有分层：核心规则在 `Assets/Scripts/Core/` 中保持纯 C#（无 Unity 依赖、无随机数、完全确定），Unity 层只负责列光标按键采集、鼠标点击换算、AI 回合驱动与 IMGUI 呈现。打开 `Assets/Scenes/ConnectFourDemo.unity` 进入 Play 模式即可游玩：`←→` 移动列光标，回车或空格往光标列落子，也可以直接鼠标点击任意列（玩家执红先手，棋子受重力落到该列最低空位）；AI 执黄用 minimax（深度 4）立即应答；`R` 键重开一局并保留比分累计；任一方横、纵或斜向连成四子即胜，42 格全满无胜为平局，获胜四连会以白框高亮。

## 结构

- `Assets/Scripts/Core/`：纯规则（重力落子棋盘状态机、规则常量与四连扫描、minimax AI、事件日志、对局与比分），`noEngineReferences` 程序集。
- `Assets/Scripts/Unity/`：`ConnectFourDemoBootstrap`（装配、列光标与按键映射、AI 回合驱动）、`ConnectFourDemoPanel`（IMGUI 蓝色底板棋盘、列光标、比分、终局提示与事件日志）。
- `Assets/Tests/EditMode/`、`Assets/Tests/PlayMode/`：Unity 测试工程。
- `Core/`、`Tests/`：dotnet 侧纯逻辑工程与 xunit 测试（与 Unity asmdef 并存，源码引用同一批文件）。

## 玩法要素

- 7 列 6 行棋盘共 69 个四连窗口；AI 用 minimax 限定深度 4，评估函数统计各方向窗口内己方二连/三连权重减去对手权重（终局 ±大分并偏好更快取胜），并列最优取最小列索引，决策完全确定。
- 会立即抓住自己的即胜手、封堵对手的即胜手，并避开垫出对手即胜点的陷阱列；顶部比分累计玩家胜/AI 胜/平局并跨局保留，底部滚动显示最近对局事件（落子、胜负、平局、重开、非法落点拒绝）。
- 终局后棋盘保持终局形状并弹出提示，按 R 再来一局；非法落点（越界列、满列、终局、非玩家回合）由核心直接拒绝并记入事件日志。

## 纯逻辑验证

```powershell
dotnet build Client/ConnectFour/Lyking.ConnectFour.slnx
dotnet test Client/ConnectFour/Tests/Lyking.ConnectFour.Core.Tests.csproj
```

测试覆盖重力落子（落底、堆叠、满列与越界拒绝）、横/纵/双对角线四连与获胜坐标、42 步棋满平局、AI 即胜/封堵/避陷阱与决策确定性、比分跨局累计与事件封顶。Unity EditMode/PlayMode 测试需在 Unity 2022.3 LTS 编辑器中运行验证；界面与交互保存真实运行截图。音效、皮肤、双人本地对战、难度分级与网络对战不在第一版范围内。
