<!-- lyking-ai
{"schemaVersion":1,"id":"20261003-gas-unity-active-effects-v1","profile":"controlled","status":"active","areas":["Client"],"paths":["docs/changes/active/20261003-gas-unity-active-effects-v1/","Client/GasUnity/"],"riskSignals":["architecture-choice","runtime-integration","public-api"]}
-->

# GAS 活动效果生命周期

## 解决什么问题

在现有 Unity GAS v1 的瞬时火球路径之上，增加可验证的活动 GameplayEffect 生命周期：效果句柄、持续时间、过期撤销、授予标签和多个属性贡献，同时保持 v1 的 `TryActivate` 行为兼容。

## 当前状态

设计已完成并经用户确认，实施计划已写入 `docs/superpowers/plans/2026-10-03-gas-active-effects.md`。实现、独立复审和当前可执行验证已完成；固定 Unity 补丁版本、真实 `Space` 输入和运行截图仍是验证缺口。

## 完成后应看到什么

目标 ASC 能原子地登记一个持续效果，读取到基础值加活动贡献的有效属性；效果到期或被显式移除后，只撤销自身贡献和自身标签引用。多个效果、基础值写入和观察者异常不会互相污染。

## 验收方式

先运行核心纯 C# 测试，再运行 Unity EditMode/PlayMode 测试；验证效果应用、过期、显式移除、标签引用计数、原子回滚和 v1 回归。所有命令、退出码、通过数量和缺口写入本目录 `evidence.md`。固定 Unity 版本、真实输入和截图要求沿用 v1 的验证边界。

## 范围与非目标

- 新增活动效果句柄和只读活动效果快照。
- 为属性增加基础值与活动贡献账本，支持多个属性的加法贡献。
- 支持有限持续时间、无限持续时间的显式移除和授予标签引用计数。
- 增加活动效果应用、移除、过期、属性变化和标签变化事件。
- 使用目标 ASC 的时钟调度到期，调度失败时完整回滚。
- 增加纯 C# 测试；Unity 适配只增加最小状态展示和测试入口，不引入表现层反写。
- 不包含周期性效果、刷新和堆叠策略、乘法或覆盖修饰器、SetByCaller、预测回滚、网络复制、GameplayCue、生产级 UI 和存档迁移。

## 风险与回滚

活动效果会扩大 `AttributeSet` 和 `AbilitySystemComponent` 的公共 API。实现必须保留现有构造器和 `TryActivate` 签名，失败时不提交任何贡献、标签、句柄或定时器。回滚方式是删除本变更新增文件并恢复本变更触碰的核心文件；不涉及已有 v1 变更目录以外的路径。
