<!-- lyking-ai
{}
-->

# 证据

环境：Windows 10 / .NET SDK 10.0.201（global.json 固定）/ 日期 2026-10-03。以下命令均为本会话在仓库根目录真实执行，无平台检查缺口（Unity 适配层验证缺口见文末）。

## 路由与工作区

- `dotnet run --project Tool/AiWorkflow/Lyking.AiWorkflow.csproj -- route --text "新建 Unity 客户端基础框架：分层、生命周期、UI、资源、网络、事件" --path Client/`：退出码 0，主领域 Client，`riskSignals: []`，`suggestedProfile: standard`。
- `check-workspace --change docs/changes/active/20261003-client-framework-v1`：范围外警告均为 `20260928-module-manager-tidy`、`20260928-framework-capability-models` 与 `Share/GameFramework.PluginHost` 的在途改动，保持原状未触碰。

## 状态转换

- `validate`（draft）：`isValid: true`。
- `validate --transition draft:ready`：`isValid: true`。
- `validate --transition ready:active`：`isValid: true`。

## 构建

- 命令：`dotnet build Client/GameFramework/Lyking.ClientFramework.slnx --nologo`
- 退出码 0；`已成功生成`，0 警告，0 错误（Core/Services/UI 为 netstandard2.1，Demo/Tests 为 net10.0）。

## 测试

- 命令：`dotnet test Client/GameFramework/Tests/Lyking.GameFramework.Client.Tests.csproj --no-build`
- 退出码 0；`已通过! - 失败: 0，通过: 167，已跳过: 0，总计: 167`。
- 第一版 100 用例：核心层（事件总线 7、服务注册 6、模块宿主 10、计时 6、对象池/状态机 12、GameClient 6）；服务层（资源 7、网络 10、配置/本地化/场景/音频/设置 17）；界面层（窗口 11、绑定/Presenter 8）。中途曾 10 项失败，其中 1 项为框架缺陷（`BindableProperty.Bind` 未订阅 Changed 事件）已修复，其余为测试断言口径错误已修正，修复后全绿。
- 第二阶段新增 67 用例：FSM/流程（Fsm 7、FsmService 1、ProcedureModule 3）；引用池/ListPool（8）；数据结点（8）；Builder/特性/Profiling（4+2）；数据表（8）；实体（10）；下载（8）；UI 模态与 UIFormLogic（5+3）。期间发现并修复的框架缺陷：`BindableProperty.Bind` 漏订阅、`NetworkService` 放弃重连后事件重复触发、`DownloadService.Find` 完成后查不到任务、`DataNode.Depth` 差一、Builder 显式登记内置计时器与内置实例重名冲突、`ModuleMetadataAdapter` 下模块环境注入时机（现在由宿主在回调前注入）、`FsmState` 生命周期钩子访问级别（protected，附内部分发器）；其余失败均为测试断言口径错误。

## 演示

- 命令：`dotnet run --project Client/GameFramework/Demo/Lyking.GameFramework.Client.Demo.csproj --no-build`
- 退出码 0；输出完整轨迹：GameClientBuilder 装配 7 个模块（Timer、Procedure、GamePlay、Loader、UiDemo、NetDemo、MetricsDemo）按拓扑序初始化 → 流程机 Splash→Loading→MainCity → 数据表加载 3 行道具 → 实体展示/关闭时隐藏 → 模态公告弹窗打开与计时关闭 → 环回网络收到 ping → 跨模块事件 → 逆序关闭（MetricsDemo → NetDemo → UiDemo → Loader → GamePlay → Procedure → Timer）→ `客户端已关闭`。

## 结构与范围

- `validate`（active，最终）：`isValid: true`，无 issues。
- `git diff --check`：退出码 0，无空白错误；仅有范围外在途 Share 文件的换行符提示（非本变更文件，未触碰）。

## 平台检查缺口

- Unity 适配层（`Client/GameFramework/Unity/`：GameEntry、UnityLogSink、ResourcesAssetProvider、4 个 asmdef）不在 dotnet 编译范围，本变更未运行 Unity 编辑器验证；仓库尚无 Unity 工程。保持 `active`，接入 Unity 工程后按 `Client/CONTEXT.md` 补充编辑模式/运行模式验证再申请 `verified`。
