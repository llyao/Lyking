<!-- lyking-ai
{"schemaVersion":1,"id":"20261003-client-framework-v1","profile":"standard","status":"active","areas":["Client"],"paths":["docs/changes/active/20261003-client-framework-v1/","Client/GameFramework/","Client/README.md","Client/CONTEXT.md"],"riskSignals":[]}
-->

# 客户端基础框架第一版（Unity 技术）

## 解决什么问题

`Client/` 目录目前只有说明文档，没有任何可运行代码。本变更落地 Unity 技术栈的游戏客户端基础框架：确定分层结构，提供客户端启动与运行所需的全部基础子系统，并把其中不依赖 UnityEngine 的纯逻辑放进 `dotnet` 可测范围（仓库当前没有 Unity 工程，Unity 侧验证入口随后续工程补充）。

第一版交付后，用户要求升级到商业级并参考 Hydra、GameFramework（Ellan Jiang 系）等成熟框架。据此在同一分层骨架上扩展第二阶段：类状态机 Fsm 与游戏流程 ProcedureModule、状态机服务、引用池 ReferencePool/ListPool、数据结点 DataNode、数据表 DataTable、实体系统 EntityService（分组容量 + 实例池）、下载器 DownloadService（并发/重试/进度）、UI 模态暂停与 UIFormLogic、模块元数据特性与 GameClientBuilder 流式装配、模块级 Profiling；文档升级为 README（架构图 + 快速上手 + 子系统目录）+ architecture.md + module-guide.md 三篇。

## 分层设计

依赖方向自上而下单向，下层不感知上层：

| 层 | 工程 | 职责 |
|---|---|---|
| Unity 适配层 | `Lyking.GameFramework.Client.Unity` | MonoBehaviour 引导（GameEntry）、Unity 日志与 Resources 资源实现、PlayerLoop 驱动；仅 Unity 编译 |
| 界面层 | `Lyking.GameFramework.Client.UI` | 窗口栈与层级、焦点、BindableProperty、Presenter |
| 基础服务层 | `Lyking.GameFramework.Client.Services` | 资源、网络、配置、本地化、场景、音频、设置的抽象与可测模拟实现 |
| 核心层 | `Lyking.GameFramework.Client.Core` | 模块生命周期、事件总线、服务注册、计时调度、对象池、状态机、日志、GameClient 装配门面 |

- 核心层与界面层、基础服务层均为 `netstandard2.1` + C# 9，不引用 UnityEngine 与第三方容器，与 `Share/` 框架工程的编译约定一致；Unity 适配层随 `.asmdef` 提供，源文件排除在 dotnet 编译范围外（同 `Share/Vendor/VContainer` 的 `Unity/` 子目录排除模式）。
- 客户端框架第一版不依赖 `Share/GameFramework.PluginHost`：该工程当前存在其他变更的在途改动，且其 VContainer 组合模型面向宿主插件；客户端核心采用自身的轻量模块模型，后续如需统一再引入。

## 当前状态

本变更处于 `active`。实施已完成：`dotnet build` 0 警告 0 错误，`dotnet test` 100 用例全绿，Demo 输出完整启动-帧循环-关闭轨迹，证据见 `evidence.md`。保持 `active` 而非 `verified`：Unity 适配层需 Unity 编辑器验证（仓库尚无 Unity 工程），且独立审查未进行；两项完成后按流程申请 `verified`。

## 完成后应看到什么

`Client/GameFramework/` 下有 `Lyking.ClientFramework.slnx` 与四个框架工程 + 测试工程 + 演示工程；`dotnet build` 0 错误 0 警告；`dotnet test` 纯逻辑用例全绿（167 个，覆盖全部子系统语义含失败回滚与异常隔离）；演示工程以 Builder 装配 7 个模块，输出 流程机（Splash→Loading→MainCity）、数据表、实体、模态窗口、环回网络与逆序关闭的完整轨迹；`README.md` + `docs/architecture.md` + `docs/module-guide.md` 三篇文档；Unity 适配层源文件就位并有 `.asmdef`，文档明确其需 Unity 编辑器验证。

## 验收方式

运行 `route`、`validate`、`dotnet build Client/GameFramework/Lyking.ClientFramework.slnx`、`dotnet test Client/GameFramework/Tests/Lyking.GameFramework.Client.Tests.csproj`（167 用例）、`dotnet run --project Client/GameFramework/Demo/Lyking.GameFramework.Client.Demo.csproj`、`git diff --check`，证据（日期、命令、退出码、通过数量）记录在 `evidence.md`。不运行不存在的 Unity 测试，不把文档结构检查当作运行时能力证明。

## 范围与非目标

范围：`Client/GameFramework/` 全部新文件、`Client/README.md` 与 `Client/CONTEXT.md` 验证入口更新、本变更目录文档。

非目标：不实现具体网络协议与序列化格式（协议契约属 `Share/`，客户端只留 `IMessageCodec` 扩展点）；不实现真实存档格式与迁移（存档属受控风险，本版只提供内存 `ISettingsStore` 与接口）；不做热更管线、AssetBundle/Addressables 真实加载、UGUI 控件绑定、WebRequest（README 路线图列明）；不修改 `Share/` 与 `Server/`、`Art/`；不引入第三方包（xunit 与测试 SDK 除外）。

## 风险与回滚

风险：分层边界失守导致 UI/服务反向依赖（以工程引用单向约束规避）；模拟实现被误当真实平台能力（在类型注释与文档中标明"模拟，Unity 适配层替换"）。全部为新增文件，回滚按目录删除即可，不涉及数据、协议和持久化。
