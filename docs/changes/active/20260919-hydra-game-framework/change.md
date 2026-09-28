<!-- lyking-ai
{"schemaVersion":1,"id":"20260919-hydra-game-framework","profile":"controlled","status":"active","areas":["Client","Server","Share","Tool"],"paths":["docs/changes/active/20260919-hydra-game-framework/","Share/CONTEXT.md","Share/Vendor/","Share/GameFramework.PluginAbstractions/","Share/GameFramework.PluginHost/","Share/GameFramework.PluginHost.VContainer/","Share/GameFramework.PluginHost.Tests/","Share/GameFramework.ModuleDemo/","Share/GameFramework.slnx"],"riskSignals":["shared-contract","multi-area","architecture-choice"]}
-->

# 基于 Hydra 的通用游戏框架方案

## 解决什么问题

在 Lyking 中建立一套可复用的通用游戏框架，使客户端、服务端和工具链共享稳定的游戏语义，同时吸收 Hydra 的 World/Simulation 设计和 GameFrameX 的在线基础设施能力，保留单机、本地预测、权威服务端和后续多人扩展的演进空间。

## 当前状态

本变更处于 `active`。设计阶段已完成并经复审；2026-09-26 用户确认模块管理先行、选定 VContainer 作为物化层（见 `design.md` 3.9 节决策记录），模块管理中间件第一版已实施并通过 22 个 headless 测试。Unity 版本、ECS 策略和 `MinimalAction`/`TurnBased` 样例仍待确认，只阻塞对应后续阶段；插件文件 manifest、热更边界和逆拓扑 Drain 的安全点细化属于插件宿主后续任务。

## 完成后应看到什么

应有一套经过评审的通用内核、插件合同、依赖边界、模块合同、内容生产链路、分阶段执行任务、验收门和回滚方式。第一阶段的可运行垂直链路由两个非 MMO 样例证明：加载配置 → 创建模拟实例 → 按 profile 时钟推进 → Projection → 可选 PresentationSession → 输入命令 → 事件、快照与状态哈希收敛；Hydra 的 Meta/Live/Show 三界和 GameFrameX 在线基础设施在后续插件/Profile 中验证。当前已落地其中模块管理层：`Describe → Contribute → Plan → Validate → Apply(VContainer) → Start → Drain → Stop` 全流程在 headless 下可运行。

## 验收方式

本轮验收运行 Lyking `route`、`validate`、`Share/GameFramework.slnx` 全量构建、`dotnet test Share/GameFramework.PluginHost.Tests`（22 用例）、`ModuleDemo` 控制台全流程演示、Markdown 占位符扫描和 `git diff --check`，证据记录在 `evidence.md`。后续阶段不运行不存在的 Unity 或游戏服务端测试，也不把文档结构检查当作运行时能力证明。

## 范围与非目标

范围是框架设计、契约设计、执行计划和验证计划。首版不直接搬运 Hydra 的 Orleans、DynamoDB、Valkey、Kubernetes、Character Save、完整 MMO 路由和全部玩法模块，也不直接复制 GameFrameX 的 MongoDB、Player Actor、ProtoBuf Handler 或聚合仓目录；这些能力只通过受控 adapter 评估。模块管理切片不包含 Player/Combat 等产品词汇，不引入文件 manifest 加载与热更新边界（属插件宿主后续任务）。

## 风险与回滚

方案文件均可独立回滚。代码阶段采用按阶段提交和 feature gate；协议、运行时内容和存档变更必须保留版本号、兼容窗口和回滚步骤。vendored VContainer（MIT）以源码引入并锁定上游提交，升级必须重新裁剪复制并重跑全部测试。
