# Unity GAS 模拟项目设计

## 1. 目标与决策

首版使用 Unity 2022.3 LTS，采用内置渲染管线和最少外部包，构建一个单机战斗演示。演示核心循环是：玩家按 `Space` 激活火球能力，检查标签、法力和冷却，扣除法力，向训练假人应用伤害，展示结果和拒绝原因。

采用“纯 C# 核心 + Unity 适配层”方案。核心不引用 `UnityEngine`，Unity 只负责场景、输入、视图和生命周期桥接。

## 2. 架构

```text
Client/GasUnity/
  Assets/Scripts/Core/        纯 C#：标签、属性、效果、能力、ASC、时钟
  Assets/Scripts/Definitions/ Unity ScriptableObject 定义转换
  Assets/Scripts/Unity/       MonoBehaviour、输入、UI、演示装配
  Assets/Tests/EditMode/      Unity EditMode 定义和资产测试
  Assets/Tests/PlayMode/      场景生命周期与输入链路测试
  Assets/Scenes/              GasDemo.unity
  ProjectSettings/    Unity 2022.3 项目设置
  Packages/           锁定 Unity Test Framework 等最小依赖
```

核心对象职责：

- `GameplayTag` / `GameplayTagContainer`：标签层级匹配、拥有、需求和阻断；默认结构体态表示空标签，容器拒绝空标签，点分层级不允许空段。
- `AttributeSet`：Health、Mana、AttackPower 等基础值与当前值。
- `GameplayEffectSpec`：瞬时属性修饰器、授予标签和持续时间的不可变描述；v1 能力激活只接受一个负向瞬时修饰器，不接受持续时间或授予标签，其他规格通过 `InvalidEffect` 拒绝。
- `GameplayAbility`：激活检查、成本提交、效果生成和事件发布。
- `AbilitySystemComponent`：能力授予、激活入口、v1 伤害效果应用、冷却计时和事件顺序；能力移除留待后续版本。
- `IGameplayClock`：提供可控逻辑时间，演示实现由 Unity `Update` 驱动，核心不读取墙钟。

Unity 层的 `GasActor` 只持有 ASC 和展示引用；输入通过 `TryActivate` 进入核心，UI 订阅事件并读取只读快照，不直接写属性。

## 3. 运行时数据流

1. 场景启动时创建玩家与假人 ASC，注册属性并授予 `Fireball`。
2. 输入桥收到 `Space`，调用 `TryActivate("Fireball", target)`。
3. ASC 检查重入状态、激活标签、阻断标签、目标有效性、冷却、Mana 成本和效果规格是否符合 v1 约束。
4. 成功时先提交成本，再应用效果规格中的单一负属性修饰器；施法者存在 `AttackPower` 时覆盖默认伤害值，最后启动冷却。
5. 发布 `AbilityActivated`、`CostCommitted`、`EffectApplied`、`AttributeChanged`、`CooldownStarted` 事件。由能力提交引起的属性变化携带来源 `AbilityId`；直接调用 `AttributeSet.Set` 的变化没有能力来源，事件中的 `AbilityId` 为空字符串。
6. 失败时返回 `AbilityRejected` 和明确原因，属性、冷却和事件状态不产生部分修改；同步事件回调中递归激活同一 ASC 会直接返回 `ActivationInProgress`，不再递归发布事件。

事件顺序是确定的；同一输入与初始状态得到相同结果。本版不做网络预测、回滚、存档或跨进程复制，也不允许在事件回调中同步重入激活。

## 4. 验收与验证

纯逻辑验收覆盖：标签匹配与默认空值、单一瞬时效果修饰器、动态伤害、资源成本、冷却锁定与恢复、阻断标签、零冷却重入拒绝、未支持效果元数据拒绝、失败不改状态和事件顺序。多属性修饰器、持续时间和授予标签的通用效果生命周期不属于本版验收，后续单独设计。

Unity EditMode 验收覆盖：定义对象转换为运行时规格，场景装配找到玩家和目标 ASC。PlayMode 测试当前覆盖输入分发入口、法力下降、生命下降、冷却拒绝、冷却恢复和对象销毁；真实 `Space` 键盘路径仍需在 Unity 编辑器中人工验证。演示保留真实运行截图。

验证入口在工程创建后补充到 `Client/CONTEXT.md`。若当前机器没有 Unity 2022.3 编辑器，只记录可执行的 dotnet 验证和 Unity 验证缺口，不把静态文件检查当作运行时通过证据。

## 5. 回滚与后续扩展

本变更不修改 `Share/`、`Server/` 或现有 GameFramework 在途文件。回滚删除 `Client/GasUnity/` 以及本变更目录即可。后续可在不改变核心接口的前提下增加 Buff/Debuff、GameplayCue、编辑器校验和网络适配。
