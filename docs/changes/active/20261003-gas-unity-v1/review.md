# 独立审查

独立只读审查已完成。审查范围包含相对 `227a270` 的累计差异、场景修正和当前实现，结论如下：

- 首轮发现的无效属性名部分提交、必需标签任一匹配、静态伤害未使用 `AttackPower`、跨 ASC 事件顺序重复、面板缺少冷却和事件未退订问题已修复，并补充了 5 个核心测试。
- 核心程序集保持 `noEngineReferences=true`，源码不引用 `UnityEngine`；表现层只读取属性/冷却并订阅事件，不直接写核心属性。
- `GameplayEffectSpec` 现在复制并只读保存修改器，拒绝空属性名和非有限增量。
- 剩余验证缺口是环境性的：当前主机没有 Unity 2022.3 编辑器和有效 Unity 批处理许可证，无法执行目标 EditMode/PlayMode 测试，也没有真实运行截图。PlayMode 仍通过直接调用演示入口覆盖核心循环，`Space` 输入链路需要在目标编辑器中手动确认。

### 二次深度审查补充

- **高：激活事务仍非原子。** `AbilitySystemComponent.TryActivate` 在 `AbilitySystemComponent.cs:89-107` 先扣除 Mana，再应用目标效果。`AttributeSet.AttributeChanged` 的外部订阅者异常不会被 `Publish` 隔离；目标属性计算溢出时，`AttributeSet.Set` 也会抛异常。两种情况都会留下已扣成本、已发布部分事件、未应用伤害且未建立冷却的半提交状态。现有 12 个测试没有覆盖该失败路径。
- **中：Unity 资产元数据不完整。** `Client/GasUnity/Assets` 下 21 个 `.cs/.asmdef` 源文件中有 15 个缺少同名 `.meta`，并且场景、脚本、测试目录缺少目录元数据。清洁检出后 Unity 会自动生成新 GUID，可能造成场景/程序集引用漂移和 CI 工作区污染。
- **中：输入链路没有自动覆盖。** `GasDemoPlayModeTests.cs:24-30` 直接调用 `TryCastFireball`，没有驱动 `Input.GetKeyDown(KeyCode.Space)`；输入桥仍需目标编辑器中的手动验收。
- **中：数据驱动定义没有接入真实场景。** `GasDemo.unity:148` 的 `fireballDefinition` 为 null，场景走硬编码能力；仓库也没有能力或效果 `.asset`。当前 EditMode 测试只验证 `CreateInstance` 默认值，不能证明序列化资源导入和字段修改有效。
- **低：公共 API 对 null 能力 ID 未定义。** `TryActivate` 直接把 `abilityId` 传给字典查询，null 会抛 `ArgumentNullException`，而其他未知 ID 返回结构化拒绝。
- **低：成功提示硬编码 25 点伤害。** `GasDemoBootstrap.cs:72-74` 的文本没有使用实际 `AttackPower` 或定义值，修改配置后会与 Health 变化不一致。

因此上一节“未发现阻塞问题”仅适用于首轮修复后的静态核心路径，已被本次二次审查补充覆盖；在修复事务原子性、补齐 `.meta`、接入真实定义资源并完成 Unity 2022.3 验证前，变更必须保持 `active`。

