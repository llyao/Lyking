<!-- lyking-ai
{"schemaVersion":1,"id":"20261004-minesweeper-unity-v1","profile":"standard","status":"active","areas":["Client"],"paths":["docs/changes/active/20261004-minesweeper-unity-v1/","Client/Minesweeper/","Client/CONTEXT.md","Client/README.md"],"riskSignals":["architecture-choice","runtime-integration"]}
-->

# Unity 扫雷项目第一版

## 解决什么问题

在 `Client/` 建立一个独立可打开的 Unity 2022.3 LTS 扫雷游戏项目，覆盖经典扫雷的核心玩法：首击安全布雷、左键翻开、右键插旗、洪水展开、剩余雷数统计、胜负判定和重开。

## 当前状态

核心规则、Unity 适配层、演示场景和三套测试已实现并通过验证；独立只读审查通过（无严重与中级问题，低级建议已落实），变更保持 `active` 等待人工验收项。

## 完成后应看到什么

`Client/Minesweeper/` 是独立 Unity 2022.3 工程。打开 `Assets/Scenes/MinesweeperDemo.unity` 运行后，面板显示剩余雷数、计时和状态；左键翻开格子，右键插旗/拔旗；空白格自动洪水展开；踩雷显示全部地雷与错误旗；翻完所有安全格判胜；`R` 或按钮重开，`1`/`2`/`3` 切换初级/中级/高级难度；面板保留最近事件日志。

## 验收方式

纯逻辑运行 `dotnet test Client/Minesweeper/Tests/Lyking.Minesweeper.Core.Tests.csproj`；核心源码保持无 `UnityEngine` 引用；Unity 工程运行 EditMode 和 PlayMode 测试；`validate`、`git diff --check` 与资产 `.meta` 完整性检查通过。所有命令、退出码、通过数量与缺口记录到本目录 `evidence.md`。

Unity 验证在 2022.3.59f1 临时副本执行：本机未安装工程固定的 2022.3.40f1，为不污染原工程版本钉子，采用 GasUnity 既有做法——把工程复制到临时目录用 2022.3.59f1 运行测试，原工程版本保持不变；该差异与剩余人工验收项（固定补丁版本、真实鼠标输入、运行截图）记录在 `evidence.md`。

## 范围与非目标

- 新建 `Client/Minesweeper/` Unity 2022.3 LTS 工程（纯逻辑核心 + Unity 适配层 + 演示场景）。
- 核心规则：`BoardSpec` 棋盘规格与难度预设、首击安全布雷、旗格阻挡、洪水展开、胜利与失败判定、剩余雷数（可为负）。
- 核心事件：翻开、洪水展开、插旗/拔旗、踩雷、胜利、失败、新对局；观察者异常不破坏棋盘状态。
- Unity 适配：`MinesweeperDemoBootstrap`（棋盘生命周期、输入分发、计时）和 `MinesweeperDemoPanel`（IMGUI 状态区、可点击网格、事件日志）。
- 测试：dotnet xunit 核心测试、Unity EditMode 测试（含面板命中映射）和 PlayMode 场景测试；记录结构验证、纯逻辑验证和 Unity 验证缺口。
- 非目标：存档与排行榜、网络对战、音效美术资源、uGUI/TMP 界面、自动求解器、多语言、可自定义棋盘参数输入框。计时器属于表现层状态，不进入核心规则。

## 关键设计决策

- 目录与程序集命名沿用 `Client/GasUnity` 惯例：核心在 `Assets/Scripts/Core/`（`noEngineReferences` 程序集），Unity 侧在 `Assets/Scripts/Unity/`，同一份核心源码由 `Core/` 下的 netstandard2.1 工程编译并跑 dotnet 测试。
- 界面采用 IMGUI（`OnGUI`），避免引入 TextMesh Pro 资产依赖，网格与状态在运行时绘制。
- 随机源抽象为 `IMinesweeperRandom`，测试用固定种子获得确定性雷区；场景默认运行时种子，Bootstrap 提供固定种子开关供 PlayMode 测试复现。
- 首击安全：首次有效翻开时才布雷，排除首格及其邻域；棋盘过小时缩小排除集，保证 `MineCount <= 格数 - 1` 即可成局。
- 坐标越界、对已翻开格操作、游戏结束后操作都返回显式结果枚举，不抛异常不改变状态；只读 `CellAt` 越界抛参数异常快速失败。
- `Start` 会保留 `Start` 前通过 `Restart`/`SelectDifficulty` 已创建的棋盘，避免外部初始化被序列化默认值覆盖。

## 风险与回滚

风险：Unity 补丁版本不一致导致临时副本与固定版本 2022.3.40f1 行为差异；IMGUI 布局在小屏幕溢出。通过程序集边界、确定性随机测试和真实烟测约束；版本差异与截图缺口如实记录在 `evidence.md`。回滚时删除 `Client/Minesweeper/`、本变更目录及 `Client/CONTEXT.md`、`Client/README.md` 中的对应条目即可。
