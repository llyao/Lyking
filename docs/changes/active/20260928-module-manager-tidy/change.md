<!-- lyking-ai
{"schemaVersion":1,"id":"20260928-module-manager-tidy","profile":"controlled","status":"verified","areas":["Share"],"paths":["docs/changes/active/20260928-module-manager-tidy/","Share/GameFramework.PluginAbstractions/","Share/GameFramework.PluginHost/","Share/GameFramework.PluginHost.Tests/","Share/GameFramework.ModuleDemo/"],"riskSignals":["shared-contract"]}
-->

# 模块管理器重新整理

## 解决什么问题

模块管理中间件第一版（2026-09-26 落地于 `20260919-hydra-game-framework`，22 个 headless 测试全绿）已可运行，但代码组织与文档口径存在四类真实问题：

1. 宿主状态机枚举 `HostState` 定义在 `Share/GameFramework.PluginHost/Hosting/ModuleHealthRegistry.cs` 内部，核心状态机没有自己的文件；`PlannedModuleRef` 嵌套在健康登记表内部，名称与 `Planning/PlannedModule` 概念混淆。
2. `ModuleCompositionException` 位于 `GameFramework.PluginAbstractions` 根目录，与按职责分目录（`Hosting/`、`Modules/`、`Planning/`、`Registrar/`、`Validation/`）的其余合同不一致。
3. 生命周期轨迹口径不一：`20260919-hydra-game-framework/design.md` 3.9 节写 `Describe → Contribute → Plan → Validate → Apply → Start → Ready → Drain → Stop → Dispose`，而 `IGameModule.cs` 注释写不含 Ready 的版本。Ready 是宿主状态而非模块回调阶段，两处表述需统一。
4. `ModuleComposer` 同时暴露 `Compose`（返回计划、丢失模块实例对齐）与 `ComposeWithModules` 两个公共入口，依赖注释提示区分，存在误用空间。

同时，本次变更是用户要求的 AI 工作流完整交互演练：从 `draft` 起，经设计确认、实施、验证、独立审查到交付，每个环节产出受控工件与真实证据。

## 当前状态

本变更处于 `verified`。实施与验证完成（选择 A，公共 API 与行为不变；22 用例全绿，演示轨迹一致），独立审查五项全过、非阻塞观察已修订，证据见 `evidence.md`，审查结论见 `review.md`。验证均为本会话真实执行的 .NET 命令，无平台检查缺口。提交与推送待用户明确授权。

## 完成后应看到什么

模块管理器代码按职责归位：`HostState` 有独立文件，组合异常与合同目录组织一致，生命周期口径在代码注释与变更文档中统一，`ModuleComposer` 入口语义清晰。公共 API 与行为保持第一版不变（选择 A），`Share/GameFramework.slnx` 全量构建 0 错误（触及工程 0 警告；`Share/Vendor/VContainer/` 存在 1 个上游既有警告，不在本变更范围），`GameFramework.PluginHost.Tests` 22 用例全绿，`ModuleDemo` 控制台输出同一生命周期轨迹。

## 验收方式

运行 `route`、`validate`、`dotnet build Share/GameFramework.slnx`、`dotnet test Share/GameFramework.PluginHost.Tests/GameFramework.PluginHost.Tests.csproj`（22 用例）、`dotnet run --project Share/GameFramework.ModuleDemo`、`git diff --check`，证据（日期、命令、退出码、通过数量）记录在 `evidence.md`。本轮不运行不存在的 Unity 或服务端测试，不把文档结构检查当作运行时能力证明。

## 范围与非目标

范围：`Share/GameFramework.PluginAbstractions/`、`Share/GameFramework.PluginHost/` 的文件组织与注释口径，`Share/GameFramework.PluginHost.Tests/`、`Share/GameFramework.ModuleDemo/` 的同步适配，以及本变更目录文档。

非目标：不引入文件 manifest、热更边界、逆拓扑 Drain 安全点（归属 `20260919-hydra-game-framework` 后续任务）；不改变生命周期语义、失败语义和合同类型成员；不修改 `Share/Vendor/VContainer/`；不增删 `Share/GameFramework.slnx` 工程。

## 风险与回滚

风险：文件移动可能引入命名空间漂移（通过保持命名空间不变规避）；文档改写可能引入新的口径矛盾（以 `20260919-hydra-game-framework/design.md` 3.9 节为权威口径）。若用户选择收缩公共 API，下游 `ModuleDemo` 与测试需同步适配，回归面扩大。

回滚：所有改动为单文件粒度，按文件回滚即可；不涉及数据、协议和持久化，无兼容窗口要求。
