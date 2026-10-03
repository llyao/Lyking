<!-- lyking-ai
{"schemaVersion":1,"id":"20261003-gas-unity-v1","profile":"controlled","status":"active","areas":["Client"],"paths":["docs/changes/active/20261003-gas-unity-v1/","Client/GasUnity/","Client/CONTEXT.md","Client/README.md"],"riskSignals":["architecture-choice","runtime-integration"]}
-->

# Unity GAS 模拟项目第一版

## 解决什么问题

在 `Client/` 建立一个 Unity 2022.3 LTS 可运行示例，模拟 Unreal Gameplay Ability System 的核心工作流：能力激活、标签约束、属性修改、资源消耗、冷却和伤害效果。

## 当前状态

设计和实施计划已获用户确认。纯 C# 核心、Unity 2022.3 工程骨架、演示场景、输入桥、状态面板和测试入口已实现；当前进入验证阶段。

## 完成后应看到什么

一个可在 Unity 2022.3 LTS 打开的 `Client/GasUnity/` 工程。运行 `GasDemo.unity` 后，玩家按 `Space` 可以消耗 Mana 对训练假人造成伤害；冷却期间再次施法会被拒绝，冷却结束后恢复。场景面板显示属性、冷却和事件日志。

## 验收方式

纯逻辑运行现有 .NET 测试入口；Unity 工程运行 EditMode 和 PlayMode 测试；手动运行场景并保存真实截图。所有命令、退出码、通过数量和 Unity 验证缺口记录到本目录 `evidence.md`。

## 范围

- 新建 `Client/GasUnity/` Unity 2022.3 LTS 示例工程。
- 新建无 `UnityEngine` 依赖的 GAS 核心：GameplayTag、AttributeSet、GameplayEffect、GameplayAbility、AbilitySystemComponent。
- 新建 Unity 适配：场景宿主、输入桥、状态面板和事件日志。
- 覆盖玩家施法、Mana 成本、Cooldown、AttackPower 伤害和假人 Health。
- 增加纯逻辑测试、Unity EditMode/PlayMode 测试入口和运行截图证据。

## 范围与非目标

网络复制、预测回滚、存档迁移、ECS、Addressables、复杂 GameplayCue 编辑器、生产级 UI 和多人同步。

## 风险与回滚

核心规则与 Unity 生命周期边界失守会导致不可测或表现层反写状态；通过程序集边界、只读事件订阅和纯逻辑测试约束。变更仅新增 `Client/GasUnity/` 与相关文档，回滚可删除新增目录和文档记录。
