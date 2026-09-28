# 基于 Hydra 的通用游戏框架任务清单

## 设计阶段

- [x] 读取 `CONTEXT-MAP.md` 和 Client、Server、Share、Tool 上下文。
- [x] 使用 `Tool/AiWorkflow route` 确认主领域、关联领域和风险。
- [x] 检查用户给出的 Hydra 路径，并找到实际可访问的 `E:\hydra_git2\hydra`。
- [x] 阅读 Hydra 系统概览、三界架构、World 核心合同、玩法上下文、表系统、Runtime Content、HyFlow、客户端生命周期和 MMO 框架设计。
- [x] 记录 Hydra 目标架构与实际实现之间的区别。
- [x] 形成 Lyking 的通用内核、运行配置、适配器、工具链和首条垂直链路方案。
- [x] 写入 `design.md`、`plan.md`、`review.md` 和 `evidence.md`。
- [x] 根据通用性复审补充依赖 DAG、禁止引用、时间/随机/快照/回放合同、元数据分层、可选 PresentationSession、两个非 MMO 样例和 profile 兼容矩阵。
- [x] 分析 GameFrameX 聚合仓库，并确定 Actor、网络、MongoDB、Hotfix、Luban、多端客户端和监控只作为可选在线适配层。
- [x] 2026-09-26 用户确认：模块管理先行，物化层选用 VContainer（NuGet 包不可用，按源码引入 `Share/Vendor/VContainer`）；其余选择（Unity 版本、样例、ECS）保持待定，见 `design.md` 3.9 节。
- [x] 2026-09-26 实施模块管理中间件第一版：`Share/GameFramework.PluginAbstractions`、`Share/GameFramework.PluginHost`、`Share/GameFramework.PluginHost.VContainer`、`Share/GameFramework.PluginHost.Tests`（22 用例全绿）、`Share/GameFramework.ModuleDemo` 控制台全流程演示。
- [ ] 用户确认 Unity 版本、LocalProfile/AuthoritativeProfile 起点和 ECS 策略。

## 契约阶段

- [ ] 建立 `Share/GameFramework.Contracts` 项目。
- [ ] 建立不可互换通用 ID、分层 command/event/snapshot 元数据、版本和投影合同。
- [ ] 增加 schema 追加兼容、旧 generation 拒绝和核心禁止引用测试。
- [ ] 运行共享契约测试并记录证据。

## 运行内核阶段

- [ ] 建立 `Share/GameFramework.Kernel`、`Share/GameFramework.Simulation` 项目。
- [ ] 建立 `GameFramework.PluginAbstractions` 和 `GameFramework.PluginHost`，完成 manifest、依赖 DAG、能力注册、生命周期、Health 和降级策略。
- [ ] 实现 GameInstance、SimulationState、Module、HostAdapter、可选时钟、authority、快照、状态哈希、回放、状态容器、FIFO 队列和可扩展生命周期状态机。
- [ ] 测试重复、迟到、过期、队列满、关闭后输入、快照恢复、跨 host 哈希和回放分歧。
- [ ] 运行运行内核测试并记录证据。

## 通用运行配置与垂直玩法阶段

- [ ] 建立 LocalProfile 和 AuthoritativeProfile，验证同一 Simulation 可替换 HostAdapter 和 AuthorityModel。
- [ ] 将 HydraOnlineProfile 作为可选组合，映射 Meta/Live/Show 和 RuntimeSlice。
- [ ] 建立 GameFrameXAdapter，验证 Actor、ProtoBuf、Session、MongoDB、Hotfix 与通用 Simulation 的边界。
- [ ] 实现 `Samples/MinimalAction` 和 `Samples/TurnBased`，不把 Player、Combat、Objective 放进核心验收。
- [ ] 完成 Simulation → Projection → Presentation → command route 闭环。
- [ ] 完成两个样例的 LocalProfile Headless 测试、snapshot/replay 和状态哈希比较。

## 内容与表阶段

- [ ] 建立 schema、authoring、compiler、runtime delivery、manifest 和 current projection。
- [ ] 明确 Luban 与 Hydra compiler 的内容族所有权，禁止同一内容族双重生成。
- [ ] 建立表快照、内容快照和原子发布门。
- [ ] 测试内容哈希、字段追加、失败不发布和回滚。

## Unity 与服务端阶段

- [ ] 按确认的 Unity 版本建立 `Client/Unity` 工程和适配层。
- [ ] 建立 Server Host、transport adapter、readiness、drain 和 stop。
- [ ] 通过真实入口验证可选表现会话、会话清理、输入拒绝、恢复和保存 barrier。

## 审查与验收阶段

- [ ] 运行所有受影响的 .NET 测试和 Unity 测试。
- [ ] 运行结构化工具门禁和生成物新鲜度检查。
- [ ] 运行依赖 DAG、禁止引用和 profile 兼容矩阵门禁。
- [ ] 完成 GameFrameX/Hydra 许可证、版本、私有依赖和聚合仓同步边界审查。
- [ ] 运行插件兼容、依赖循环、状态命名空间、启动失败清理、Drain 和可热更回退测试。
- [ ] 完成不同执行上下文的只读独立审查。
- [ ] 关闭高严重度发现和未覆盖验收项。
- [ ] 更新 `evidence.md`，确认当前版本真实证据。
- [ ] 仅在满足工作流门禁后转换状态。
