# GAS 活动效果生命周期实施计划

> **面向代理开发者：** 使用 `superpowers:subagent-driven-development` 或 `superpowers:executing-plans` 按任务执行。每个步骤用复选框跟踪。

**目标：** 在现有 Unity GAS v1 上增加持续/无限活动效果、属性贡献账本、授予标签生命周期和结构化事件，同时保持火球激活路径兼容。

**架构：** `GameplayEffectSpec` 继续是不变描述；目标 `AbilitySystemComponent` 持有带句柄的活动效果实例。`AttributeSet` 保存基础值和按句柄分组的活动加法贡献，`Get` 计算有效值；活动标签通过句柄引用计数聚合。活动效果使用目标 ASC 的时钟，应用、标签、贡献和定时器安排作为一个可回滚事务。

**技术栈：** C# 9、`netstandard2.1`、xUnit、Unity 2022.3 EditMode/PlayMode、现有 `ManualGameplayClock`。

---

## 文件边界

- 新建 `Client/GasUnity/Assets/Scripts/Core/ActiveGameplayEffectHandle.cs`：句柄值类型。
- 新建 `Client/GasUnity/Assets/Scripts/Core/ActiveGameplayEffect.cs`：只读快照。
- 新建 `Client/GasUnity/Assets/Scripts/Core/GameplayEffectApplicationResult.cs`：结果和失败原因。
- 修改 `Client/GasUnity/Assets/Scripts/Core/GameplayEffectSpec.cs`：显式无限工厂。
- 修改 `Client/GasUnity/Assets/Scripts/Core/AttributeSet.cs`：基础值、有效值、贡献账本。
- 修改 `Client/GasUnity/Assets/Scripts/Core/AbilitySystemComponent.cs`：应用、到期、移除和标签索引。
- 修改 `Client/GasUnity/Assets/Scripts/Core/GameplayEvent.cs`：活动效果事件字段。
- 修改 `Client/GasUnity/Assets/Scripts/Core/GameplayTagContainer.cs`：合并查询和只读快照辅助。
- 修改 `Client/GasUnity/Assets/Scripts/Definitions/GameplayEffectDefinition.cs`：持续/无限资产转换。
- 修改 `Client/GasUnity/Assets/Tests/EditMode/DefinitionConversionTests.cs`：资产转换测试。
- 修改 `Client/GasUnity/Assets/Tests/PlayMode/GasDemoPlayModeTests.cs`：场景生命周期测试。
- 新建 `Client/GasUnity/Tests/Core/ActiveGameplayEffectTests.cs`：核心生命周期测试。
- 更新 `docs/changes/active/20261003-gas-unity-active-effects-v1/tasks.md`、`evidence.md`、`review.md`。

### Task 1：值对象和规格边界

**文件：** 创建三个 Core 类型和 `ActiveGameplayEffectTests.cs`，修改 `GameplayEffectSpec.cs`。

- [ ] 写失败测试：句柄 `IsValid`/相等比较；`GameplayEffectSpec.CreateInfinite` 返回 `IsInfinite == true` 且 `DurationSeconds == 0`；失败结果保留 `ApplicationInProgress` 和无效句柄。
- [ ] 运行：`dotnet test Client/GasUnity/Tests/Lyking.GameplayAbilities.Core.Tests.csproj --filter FullyQualifiedName~ActiveGameplayEffectTests --no-restore`。预期因类型缺失失败。
- [ ] 实现 `ActiveGameplayEffectHandle` 的正整数 ID、相等和哈希；实现 `GameplayEffectFailureReason`、`GameplayEffectApplicationResult` 的成功/失败工厂；给 `GameplayEffectSpec` 增加显式无限标记与工厂，保留现有有限值校验。
- [ ] 重跑同一测试和核心全量测试，预期全部通过。

### Task 2：属性基础值和活动贡献账本

**文件：** 修改 `AttributeSet.cs`，扩展 `ActiveGameplayEffectTests.cs`。

- [ ] 写失败测试：活动贡献使 `Get` 低于 `GetBase`；活动期间 `Set` 修改基础值；移除贡献后保留新基础值；任一属性溢出时所有属性不变。
- [ ] 运行：`dotnet test Client/GasUnity/Tests/Lyking.GameplayAbilities.Core.Tests.csproj --filter FullyQualifiedName~ActiveGameplayEffectTests --no-restore`。预期因没有 `GetBase` 和贡献层失败。
- [ ] 将值表拆为基础值表和 `ActiveGameplayEffectHandle -> modifiers` 账本。实现 `GetBase`、贡献添加/移除、有限值预检；`Set`/`ApplyDelta` 只改基础值，`Get` 返回基础值加贡献，`Contains` 覆盖两者。内部返回按属性名字典序排列的前后值列表。
- [ ] 运行核心全量：`dotnet test Client/GasUnity/Tests/Lyking.GameplayAbilities.Core.Tests.csproj --configuration Release --nologo`。预期零失败、零跳过。

### Task 3：活动标签引用和事件模型

**文件：** 修改 `GameplayTagContainer.cs`、`GameplayEvent.cs`、`AbilitySystemComponent.cs` 和核心测试。

- [ ] 写失败测试：两个效果授予同一标签时移除一个仍保留；静态标签不被活动效果移除；事件带句柄和目标时钟时间；标签变化不重复发布。
- [ ] 运行筛选测试，预期因活动标签查询和事件字段缺失失败。
- [ ] 增加 `ActiveEffectApplied`、`ActiveEffectExpired`、`ActiveEffectRemoved`、`TagChanged`；给 `GameplayEvent` 增加可选 `EffectHandleId`、`Timestamp`，保持旧构造器兼容。ASC 维护活动标签句柄集合，提供合并后的 `HasTag/HasAny/HasAll` 查询。
- [ ] 运行核心全量测试，预期 v1 事件顺序和新增标签测试均通过。

### Task 4：ASC 活动效果事务、到期和移除

**文件：** 修改 `AbilitySystemComponent.cs`，必要时修改 `ManualGameplayClock.cs`，扩展 `ActiveGameplayEffectTests.cs`。

- [ ] 写失败测试：持续效果立即贡献并在精确到期边界撤销；无限效果只能显式移除；移除幂等；基础值变化不被覆盖；调度失败完全回滚；事件回调递归应用返回 `ApplicationInProgress`。
- [ ] 运行：`dotnet test Client/GasUnity/Tests/Lyking.GameplayAbilities.Core.Tests.csproj --filter FullyQualifiedName~ActiveGameplayEffectTests --no-restore`。预期因 ASC API 未实现失败。
- [ ] 实现 `TryApplyEffect(GameplayEffectSpec,string?)`、`TryRemoveEffect(ActiveGameplayEffectHandle)`、`GetActiveEffects()`。应用时先预检全部有效值，再登记句柄、贡献和标签，最后由目标 `_clock.Schedule` 安排有限效果清理；任一步骤抛错都撤销内部表。成功后按设计顺序发布事件。到期与显式移除共用幂等清理路径。
- [ ] 运行核心全量测试，预期既有 v1 测试和活动效果测试全部通过。

### Task 5：异常隔离、稳定顺序和回归

**文件：** 修改 `ActiveGameplayEffectTests.cs`、`AbilityFailureTests.cs`、新变更 `evidence.md`。

- [ ] 锁定成功应用、属性、标签、过期/移除的事件顺序；观察者抛异常后状态仍提交；溢出、非法规格、调度异常无句柄/贡献/标签/成功事件。
- [ ] 运行：`dotnet test Client/GasUnity/Tests/Lyking.GameplayAbilities.Core.Tests.csproj --configuration Release --nologo --filter "FullyQualifiedName~ActiveGameplayEffectTests|FullyQualifiedName~AbilityFailureTests"`。预期无重复事件。
- [ ] 运行核心全量测试并把实际日期、退出码、通过/失败/跳过数量写入新变更 `evidence.md`，不覆盖 v1 历史证据。

### Task 6：Unity 效果定义和 EditMode

**文件：** 修改 `GameplayEffectDefinition.cs`、`DefinitionConversionTests.cs`，必要时创建对应资产及 `.meta`。

- [ ] 写失败测试：有限持续时间、授予标签和无限标记通过 `ToRuntimeSpec` 保留；现有火球效果仍为瞬时、非无限。
- [ ] 在 Unity 2022.3 临时副本运行 EditMode，确认新增断言先失败。
- [ ] 增加 `durationSeconds`、`isInfinite` 和标签序列化字段；转换时选择有限构造或 `CreateInfinite`，对负数、非有限值和空属性名给出明确异常；保持火球资产数据不变。
- [ ] 重跑 EditMode，预期定义转换通过、编译零错误，新增资产 `.meta` 完整。

### Task 7：Unity PlayMode 生命周期入口

**文件：** 修改 `GasDemoPlayModeTests.cs`，必要时修改 `GasActor.cs`、`GasDemoPanel.cs`。

- [ ] 写失败场景测试：加载 `GasDemo`，直接对训练假人 ASC 应用持续效果，检查 Health、标签、活动效果数量和到期恢复；使用 `TrainingDummyActor.Tick` 推进目标时钟，不使用真实输入。
- [ ] 运行 Unity `-runTests -batchmode -nographics -testPlatform PlayMode`，预期新增测试先失败。
- [ ] 只增加 ASC 只读快照或测试所需桥接；面板仅显示事件，不反写属性/标签。若需区分来源，在 Unity 订阅闭包中标记 actor，不把 Unity 对象加入 Core 事件。
- [ ] 重跑 PlayMode，预期生命周期、冷却、非施法输入和销毁测试全部通过。

### Task 8：完整验证和收口

**文件：** 更新新变更的 `tasks.md`、`evidence.md`、`review.md`、`change.md`。

- [ ] 运行：`dotnet build Client/GasUnity/Core/Lyking.GameplayAbilities.Core.csproj --configuration Release --nologo` 和 `dotnet test Client/GasUnity/Tests/Lyking.GameplayAbilities.Core.Tests.csproj --configuration Release --nologo`；记录退出码、警告、错误和测试数量。
- [ ] 在独立临时副本串行运行 Unity 2022.3.59f1 EditMode/PlayMode；记录完整绝对命令、退出码、XML 通过数和日志编译诊断，不能将结果表述为固定 2022.3.40f1 已验证。
- [ ] 运行 `dotnet run --project Tool/AiWorkflow/Lyking.AiWorkflow.csproj -- validate --change docs/changes/active/20261003-gas-unity-active-effects-v1` 和 `git diff --check`，预期 `issues: []`、`isValid: true`、无空白错误。
- [ ] 完成独立只读复审，重点检查原子回滚、目标时钟、标签引用、事件重复和 v1 回归；问题修复后重新取证。
- [ ] 只有所有必需证据和复审通过后才完成任务清单；固定版本、真实 Space 输入或截图仍缺失时保持 `active`，不转换为 `verified`。

## 自审结果

- 规格覆盖：Task 1 覆盖值对象与无限语义，Task 2 覆盖账本，Task 3 覆盖标签和事件，Task 4 覆盖生命周期事务，Task 5 覆盖错误契约，Task 6/7 覆盖 Unity，Task 8 覆盖验证和审查。
- 一致性：`TryApplyEffect` 返回 `GameplayEffectApplicationResult`；句柄类型、无限工厂、`GetBase`、目标时钟和事件名称在所有任务中一致。
- 范围：周期效果、堆叠策略、网络、预测和 GameplayCue 没有被偷偷加入任务。
- 占位扫描：没有使用 `TBD`、`TODO` 或未定义的“适当处理”步骤；每个任务都有文件、测试入口和预期结果。
