# Unity GAS 模拟项目设计

## 1. 目标与决策

首版使用 Unity 2022.3 LTS，采用内置渲染管线和最少外部包，构建一个单机战斗演示。演示核心循环是：玩家按 `Space` 激活火球能力，检查标签、法力和冷却，扣除法力，向训练假人应用伤害，展示结果和拒绝原因。

采用“纯 C# 核心 + Unity 适配层”方案。核心不引用 `UnityEngine`，Unity 只负责场景、输入、视图和生命周期桥接。

## 2. 架构

```text
Client/GasUnity/
  Runtime/Core/       纯 C#：标签、属性、效果、能力、ASC、时钟
  Runtime/Unity/      MonoBehaviour、输入、UI、演示装配
  Tests/Core/         可由 dotnet 或 Unity EditMode 执行的规则测试
  Tests/PlayMode/     场景生命周期与真实输入链路
  Scenes/             GasDemo.unity
  ProjectSettings/    Unity 2022.3 项目设置
  Packages/           锁定 Unity Test Framework 等最小依赖
```

核心对象职责：

- `GameplayTag` / `GameplayTagContainer`：标签层级匹配、拥有、需求和阻断。
- `AttributeSet`：Health、Mana、AttackPower 等基础值与当前值。
- `GameplayEffectSpec`：瞬时修改、成本、冷却和来源目标的不可变描述。
- `GameplayAbility`：激活检查、成本提交、效果生成和事件发布。
- `AbilitySystemComponent`：能力授予/移除、激活入口、效果应用、冷却计时和事件顺序。
- `IGameplayClock`：提供可控逻辑时间，演示实现由 Unity `Update` 驱动，核心不读取墙钟。

Unity 层的 `GasActor` 只持有 ASC 和展示引用；输入通过 `TryActivateAbility` 进入核心，UI 订阅事件并读取只读快照，不直接写属性。

## 3. 运行时数据流

1. 场景启动时创建玩家与假人 ASC，注册属性并授予 `Fireball`。
2. 输入桥收到 `Space`，调用 `TryActivateAbility("Fireball", target)`。
3. ASC 检查激活标签、阻断标签、目标有效性、冷却和 Mana 成本。
4. 成功时先提交成本，再应用以 AttackPower 计算的 Health 瞬时效果，并启动冷却。
5. 发布 `AbilityActivated`、`CostCommitted`、`EffectApplied`、`AttributeChanged`、`CooldownStarted` 事件。
6. 失败时返回 `AbilityRejected` 和明确原因，属性、冷却和事件状态不产生部分修改。

事件顺序是确定的；同一输入与初始状态得到相同结果。本版不做网络预测、回滚、存档或跨进程复制。

## 4. 验收与验证

纯逻辑验收覆盖：标签匹配、单一瞬时效果修饰器、动态伤害、资源成本、冷却锁定与恢复、阻断标签、失败不改状态、事件顺序。多属性修饰器、持续时间和授予标签的通用效果生命周期不属于本版验收，后续单独设计。

Unity EditMode 验收覆盖：定义对象转换为运行时规格，场景装配找到玩家和目标 ASC。PlayMode 验收覆盖：按键施法、法力下降、生命下降、冷却拒绝、冷却恢复和对象销毁无异常。演示保留真实运行截图。

验证入口在工程创建后补充到 `Client/CONTEXT.md`。若当前机器没有 Unity 2022.3 编辑器，只记录可执行的 dotnet 验证和 Unity 验证缺口，不把静态文件检查当作运行时通过证据。

## 5. 回滚与后续扩展

本变更不修改 `Share/`、`Server/` 或现有 GameFramework 在途文件。回滚删除 `Client/GasUnity/` 以及本变更目录即可。后续可在不改变核心接口的前提下增加 Buff/Debuff、GameplayCue、编辑器校验和网络适配。
