# GAS 活动效果生命周期设计

## 1. 设计决策

本变更采用贡献账本模型。`GameplayEffectSpec` 继续是不变的效果描述；运行时由目标 `AbilitySystemComponent` 创建活动效果实例。每个实例有单调递增的 `ActiveGameplayEffectHandle`，并在属性账本中登记自己的加法贡献。属性有效值由基础值与当前活动贡献之和得到。

效果到期时移除自己的贡献，而不是把施加前的数值快照写回。这样基础值变化、多个重叠效果和效果移除可以组合，且不会覆盖其他系统在效果期间写入的值。

活动授予标签不直接写入现有公开的 `OwnedTags`。ASC 维护静态拥有标签和活动效果标签的分离视图，并按标签记录活动句柄引用集合。移除一个效果只减少自己的引用；只有最后一个活动引用消失时，标签才从活动视图消失。

## 2. 核心对象与 API

### 2.1 `ActiveGameplayEffectHandle`

新增只读值类型，包含正整数 `Id`，支持相等比较、哈希和 `IsValid`。句柄只在创建活动效果的 ASC 内有意义，不能跨 ASC 直接使用。

### 2.2 `ActiveGameplayEffect`

新增只读运行时快照，包含：

- `Handle`；
- `SourceAbilityId`；
- `Spec` 的不可变引用；
- `StartTime`；
- 可选 `EndTime`；
- `IsInfinite`。

快照不暴露定时器 ID、可变贡献表或内部标签索引。

### 2.3 效果应用结果

新增 `GameplayEffectFailureReason`，至少包含 `None`、`InvalidSpec`、`InvalidAttribute`、`InvalidValue`、`SchedulingFailed` 和 `ApplicationInProgress`。新增只读 `GameplayEffectApplicationResult`，包含 `Succeeded`、`Reason` 和成功时的 `Handle`。这样同步递归应用可以返回明确原因，而不是只返回布尔值。

### 2.4 `AbilitySystemComponent`

新增以下 API，具体类型文件可在实施计划中按现有命名风格拆分：

```csharp
GameplayEffectApplicationResult TryApplyEffect(
    GameplayEffectSpec spec,
    string? sourceAbilityId);

bool TryRemoveEffect(ActiveGameplayEffectHandle handle);

IReadOnlyList<ActiveGameplayEffect> GetActiveEffects();
```

`TryApplyEffect` 只接受 `DurationSeconds > 0` 或 `GameplayEffectSpec.CreateInfinite(...)` 创建的无限效果；本阶段不把 `PositiveInfinity` 作为无限值，因为现有 `GameplayEffectSpec` 明确拒绝非有限持续时间。无限效果通过独立的 `IsInfinite` 语义表达，避免破坏原有数值校验。

效果应用阶段依次执行：

1. 校验规格、属性名称、增量和目标当前有效值均为有限数值；
2. 计算所有属性的新有效值，不写入状态；
3. 预计算标签引用变化和活动句柄；
4. 登记贡献、标签引用和句柄；
5. 安排目标 ASC 时钟的到期回调；
6. 任一步骤失败时撤销前面所有内部写入并返回失败，不发布成功事件；
7. 全部成功后发布活动效果和属性变化事件。

效果移除和到期处理都必须幂等：先标记活动实例失效并取消定时器，再删除贡献和标签引用，最后发布一次移除或过期事件。

公开的 `TryApplyEffect` 请求在活动效果事件发布期间禁止同步递归应用，返回 `ApplicationInProgress` 且不发布嵌套事件，沿用现有能力激活的同步不可重入策略。

## 3. 属性账本

`AttributeSet` 内部拆分为：

- 基础值表：由现有 `Set` 和 `ApplyDelta` 操作；
- 活动贡献表：按句柄记录属性到增量的映射；
- 有效值计算：基础值加所有活动贡献，计算结果必须有限。

新增 `GetBase` 返回基础值；现有 `Get` 返回有效值，现有 `Set` 设置基础值，现有 `ApplyDelta` 修改基础值而不是把活动贡献写入基础值；`Contains` 表示基础值或活动贡献存在。活动效果贡献的增删统一通过内部方法完成，并在有效值变化时以原有 `AttributeChanged` 和带来源的玩法事件通知观察者。观察者抛异常不能回滚已经提交的账本。

同一效果的多属性贡献按属性名的序号顺序预检和通知，保证事件顺序稳定。任一属性溢出时整个效果不提交。

## 4. 标签生命周期

`OwnedTags` 保留现有静态拥有标签的兼容访问。新增只读的活动标签查询，查询时将静态标签和活动标签合并，并继续支持父标签匹配、`HasAny` 和 `HasAll`。

授予标签索引使用 `GameplayTag -> HashSet<ActiveGameplayEffectHandle>`。应用效果时先计算新增引用，移除效果时删除对应句柄。标签只有在合并视图的真假状态发生变化时才发布 `TagChanged`，重复施加和手动静态标签不会产生错误的移除事件。

## 5. 时钟与事件

活动效果到期由目标 ASC 的 `IGameplayClock` 调度。施法者时钟和目标时钟不同步时，效果生命周期仍由状态所有者决定；场景层如需统一时间必须显式注入同一个时钟。

`GameplayEventType` 新增：

- `ActiveEffectApplied`；
- `ActiveEffectExpired`；
- `ActiveEffectRemoved`；
- `TagChanged`。

`GameplayEvent` 追加可选 `EffectHandleId` 和 `Timestamp`，旧构造器参数保持兼容。活动效果事件由目标 ASC 发布，`AbilityId` 保存来源能力 ID；目标属性事件只由目标 ASC 发布，来源 ASC 不重复转发。

一次成功应用的可观察顺序为：活动效果登记完成、`ActiveEffectApplied`、按属性名排序的 `AttributeChanged`、标签状态变化事件。过期顺序为：活动实例失效、`ActiveEffectExpired`、属性变化、标签变化。显式移除使用 `ActiveEffectRemoved`，其余顺序相同。

## 6. 测试与验收

核心测试必须覆盖：

- 持续效果应用后属性贡献和授予标签立即生效；
- 推进到 `EndTime` 时自动移除，属性和标签恢复到“移除自身贡献后”的有效值；
- 无限效果只能显式移除，重复移除不产生第二次事件；
- 两个效果授予同一标签时，移除一个仍保留标签；
- 活动期间修改基础值，效果移除后保留基础值的新状态；
- 多属性贡献按稳定顺序提交；任一属性溢出时全部不变；
- 调度失败、无效属性、无效增量和递归应用不会留下句柄、贡献、标签或定时器；
- 大步推进、精确到期边界和观察者异常不会破坏生命周期；
- v1 火球路径的资源、伤害、冷却、事件顺序和拒绝语义保持不变。

Unity EditMode 增加活动效果定义转换测试；PlayMode 只增加生命周期状态展示或手动时间推进测试，不把 `Input.GetKeyDown` 或截图缺口误写为已验证。

## 7. 后续拆分

周期性效果需要先定义 `ManualGameplayClock` 的大步追赶语义；刷新和堆叠需要独立的策略对象；GameplayCue 和有界事件日志需要独立于权威玩法事件另开变更。它们不属于本阶段实现。
