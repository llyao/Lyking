# 证据

## 2026-10-03

已读取 `CONTEXT-MAP.md`、`Client/CONTEXT.md`、`docs/ai/workflow.md` 和 `docs/ai/verification.md`。使用工作流路由确认本变更主领域为 `Client`，路径为 `Client/GasUnity/` 与本变更文档目录；由于元数据声明架构选择和运行时集成风险，变更档位为 `controlled`。

### 已通过

- `dotnet build Client/GasUnity/Core/Lyking.GameplayAbilities.Core.csproj --configuration Release --nologo`：退出码 0，0 个警告，0 个错误。
- `dotnet test Client/GasUnity/Tests/Lyking.GameplayAbilities.Core.Tests.csproj --configuration Release --nologo`：退出码 0，26 个通过，0 个失败，0 个跳过。覆盖资源/冷却查询、动态伤害、必需标签全部匹配、目标溢出、空能力 ID、观察者异常、属性事件唯一性、零伤害拒绝、不可变标签、效果规格数据流和时钟边界。
- `dotnet run --project Tool/AiWorkflow/Lyking.AiWorkflow.csproj -- route --text "在 Client/GasUnity 中实现模仿 Unreal GAS 的 Unity 示例" --path Client/GasUnity --path docs/changes/active/20261003-gas-unity-v1`：退出码 0，主领域 `Client`，风险信号为空，文本和显式路径均指向 `Client`。
- `dotnet run --project Tool/AiWorkflow/Lyking.AiWorkflow.csproj -- validate --change docs/changes/active/20261003-gas-unity-v1`：退出码 0，`issues: []`、`isValid: true`。
- `git diff --check`：退出码 0，无空白错误。
- `rg "UnityEngine|UnityEditor" Client/GasUnity/Assets/Scripts/Core`：无匹配，核心源码保持 Unity 无关。
- 二次审查静态计数：`Client/GasUnity/Assets` 下源文件、程序集、场景和资产均已存在同名 `.meta`，目录元数据也已补齐。
- 原子提交测试已覆盖 `AttributeSet.AttributeChanged` 外部订阅者异常和目标属性数值溢出；Unity EditMode/PlayMode 仍未在目标编辑器执行。
- Unity 资产接入检查：`Assets/Definitions/FireballAbility.asset` 已创建并被 `GasDemo.unity` 引用；EditMode 测试增加了序列化资产加载。PlayMode 输入测试通过 `ProcessInput(KeyCode.Space)` 覆盖与 `Update` 相同的输入分发入口。
- Unity 适配编译面审查发现 `GasActor`/`GasDemoPanel` 使用的 `GetCooldownRemaining` 原先缺失；已在核心组件补充只读查询并新增冷却剩余时间测试。后续 Unity 2022.3.59f1 临时副本已编译适配程序集并通过 EditMode/PlayMode。
- 效果数据流检查：`FireballAbility.asset` 引用 `FireballDamageEffect.asset`，`GameplayAbilityDefinition.ToRuntimeAbility()` 生成带 `GameplayEffectSpec` 的运行时能力，核心测试覆盖效果默认增量与 `AttackPower` 覆盖。
- 手动时钟边界检查：`Schedule` 和 `Advance` 拒绝负数、`NaN`、正负无穷，参数化测试覆盖三类非有限输入。
- 手动时钟溢出检查：`Now + seconds` 与 `Now + delaySeconds` 的非有限结果会被拒绝，溢出测试确认时钟状态和定时器列表不发生变化。
- 设计口径复核：已将“修饰器叠加”明确收敛为本版的单一瞬时效果修饰器，持续效果生命周期和通用多修饰器应用列为后续独立范围。

### 当前缺口

- 初次只检查了 Unity Hub 默认目录，遗漏自定义安装路径；后续确认主机有 Unity 2022.3.59f1，工程 `ProjectVersion.txt` 固定为 2022.3.40f1。
- 尚未在固定补丁版本 2022.3.40f1 打开原工程，也尚未保存 `GasDemo.unity` 的真实运行截图或验证真实 `Space` 键盘输入。

### 审查修复

- 能力构造和属性/效果规格现在拒绝空属性名、负数或非有限数值，并在提交前完成伤害来源校验。
- 必需标签改为全部满足；火球伤害优先读取施法者 `AttackPower`，未配置时回退到能力伤害值。
- Mana 成本提交后会发布来源属性变更，目标 Health 在效果事件之后由目标 ASC 发布一次；移除了来源 ASC 对目标属性的重复转发。事件观察者异常不会中断已提交状态。
- 演示面板显示 Fireball 剩余冷却，Bootstrap 在销毁时退订事件。

### 本轮修复后的复跑

- `dotnet build Client/GasUnity/Core/Lyking.GameplayAbilities.Core.csproj --configuration Release --nologo`：退出码 0，0 个警告，0 个错误。
- `dotnet test Client/GasUnity/Tests/Lyking.GameplayAbilities.Core.Tests.csproj --configuration Release --nologo`：退出码 0，25 个通过，0 个失败，0 个跳过。
- `dotnet run --project Tool/AiWorkflow/Lyking.AiWorkflow.csproj -- route --text "在 Client/GasUnity 中继续修复效果数据流和逻辑时钟边界" --path Client/GasUnity --path docs/changes/active/20261003-gas-unity-v1`：退出码 0，主领域 `Client`，风险信号为空。
- `dotnet run --project Tool/AiWorkflow/Lyking.AiWorkflow.csproj -- validate --change docs/changes/active/20261003-gas-unity-v1`：退出码 0，`issues: []`、`isValid: true`。
- `git diff --check`：退出码 0；`Client/GasUnity/Assets` 同名 `.meta` 检查 `MISSING_META=0`；核心源码 Unity 引用检查 `CORE_UNITY_REFERENCES=0`。

### 当前设计口径复核

- `dotnet build Client/GasUnity/Core/Lyking.GameplayAbilities.Core.csproj --configuration Release --nologo`：退出码 0，0 个警告，0 个错误。
- `dotnet test Client/GasUnity/Tests/Lyking.GameplayAbilities.Core.Tests.csproj --configuration Release --nologo`：先与 build 并行时因同一 DLL 同时写入报 `CS2012`；确认构建已退出后串行重跑，退出码 0，25 个通过，0 个失败，0 个跳过。
- `dotnet run --project Tool/AiWorkflow/Lyking.AiWorkflow.csproj -- validate --change docs/changes/active/20261003-gas-unity-v1`：退出码 0，`issues: []`、`isValid: true`。
- 修正设计文档中的目录/API 描述为当前实际路径 `Assets/Scripts/*` 和 `TryActivate`；Unity 2022.3 运行时验证仍缺失。

### 属性通知与伤害配置复核

- 新增跨施法者/目标 ASC 的事件顺序测试；旧实现复现 Mana 变更通知缺失与目标 Health 变更重复发布，修复后顺序为激活、成本提交、Mana 属性变化、效果应用、目标 Health 变化、冷却开始。
- 新增零增量伤害与 `AttackPower` 同时存在的测试；旧实现会成功施法并造成伤害，修复后返回 `InvalidTarget` 且 Mana/Health 不变。
- `dotnet build Client/GasUnity/Core/Lyking.GameplayAbilities.Core.csproj --configuration Release --nologo`：退出码 0，0 个警告，0 个错误。
- `dotnet test Client/GasUnity/Tests/Lyking.GameplayAbilities.Core.Tests.csproj --configuration Release --nologo`：退出码 0，26 个通过，0 个失败，0 个跳过。
- `dotnet run --project Tool/AiWorkflow/Lyking.AiWorkflow.csproj -- validate --change docs/changes/active/20261003-gas-unity-v1`：退出码 0，`issues: []`、`isValid: true`。
- `git diff --check`：退出码 0，无空白错误。

### 核心边界加固

- 默认 `GameplayTag` 哈希与层级查询、容器空标签处理及点分层级空段测试先于实现运行：旧实现出现 `ArgumentNullException`、`NullReferenceException`，并未拒绝 `.State`、`State.`、`State..Stunned`。
- 零冷却重入测试先于实现运行：事件观察者在 `AbilityActivated` 中递归激活零成本、零伤害能力时，嵌套激活旧实现仍返回成功。
- 效果元数据测试先于实现运行：包含非零持续时间或授予标签的规格旧实现均返回成功，但不处理对应语义。
- 修复后 `dotnet build Client/GasUnity/Core/Lyking.GameplayAbilities.Core.csproj --configuration Release --nologo`：退出码 0，0 个警告，0 个错误。
- 修复后 `dotnet test Client/GasUnity/Tests/Lyking.GameplayAbilities.Core.Tests.csproj --configuration Release --nologo`：退出码 0，34 个通过，0 个失败，0 个跳过。
- `GameplayEffectSpec` 的 v1 使用边界现在是：一个负向瞬时修饰器、无授予标签、持续时间为 0；不符合约束时返回 `InvalidEffect`，不会扣除 Mana 或启动冷却。
- ASC 激活同步过程增加重入保护，回调内嵌套激活返回 `ActivationInProgress`，避免零冷却能力递归提交。
- 当时尚未执行 Unity 编辑器验证、真实键盘输入和场景截图；后续已在 2022.3.59f1 临时副本完成编辑器烟测，固定版本 2022.3.40f1、真实键盘输入和场景截图仍待验证。

当前变更保持 `active`；缺少固定版本 2022.3.40f1、真实键盘输入和运行截图的验收证据，不能转换为 `verified`。

### 属性事件来源与拒绝事件契约复审

- 独立复审发现能力激活触发的 Mana 和目标 Health `AttributeChanged` 事件丢失来源 `AbilityId`，同时指出效果拒绝与递归激活测试缺少事件数量断言。
- 先扩展事件顺序测试，确认修复前 Mana 与 Health 通知的 `AbilityId` 均为空；随后保留 `AttributeSet.AttributeChanged` 的公开接口，为 ASC 增加内部来源通知，能力提交传递 `ability.Id`，直接属性写入继续使用空来源。
- 补充断言：带持续时间或授予标签的效果只发布一次 `InvalidEffect` 拒绝事件，不发布成功阶段事件；激活回调内递归调用返回 `ActivationInProgress`，不发布嵌套拒绝或第二个激活事件。
- `dotnet test Client/GasUnity/Tests/Lyking.GameplayAbilities.Core.Tests.csproj --configuration Release --nologo`：修复前退出码 1，34 项中 1 项失败，差异为预期能力 ID `Fireball` 与实际空字符串；修复后退出码 0，35 个通过、0 个失败、0 个跳过，另覆盖直接 `AttributeSet.Set` 的事件来源为空。
- 独立审查结论：未发现新增中高严重度缺陷；后续 Unity 2022.3.59f1 临时副本测试结果和剩余人工验收见下节。
- 修复后的独立只读复审检查了来源 ID、目标 ASC 通知、公开属性通知顺序及观察者异常隔离，未发现可复现问题；复审者未运行测试，验证结果以本会话的 Release 构建和核心测试为准。
- 最新 Release 构建：`dotnet build Client/GasUnity/Core/Lyking.GameplayAbilities.Core.csproj --configuration Release --nologo` 退出码 0，0 个警告、0 个错误。
- 最新核心测试：`dotnet test Client/GasUnity/Tests/Lyking.GameplayAbilities.Core.Tests.csproj --configuration Release --nologo` 退出码 0，35 个通过、0 个失败、0 个跳过。
- 最新工作流路由：`dotnet run --project Tool/AiWorkflow/Lyking.AiWorkflow.csproj -- route --text "继续审查 Client/GasUnity 的 v1 核心边界加固" --path Client/GasUnity --path docs/changes/active/20261003-gas-unity-v1` 退出码 0，主领域 `Client`。
- 最新结构校验：`dotnet run --project Tool/AiWorkflow/Lyking.AiWorkflow.csproj -- validate --change docs/changes/active/20261003-gas-unity-v1` 退出码 0，`issues: []`、`isValid: true`。
- `git diff --check` 退出码 0，无空白错误；Git 输出的 LF/CRLF 换行提示不影响检查结果。
- `dotnet run --project Tool/AiWorkflow/Lyking.AiWorkflow.csproj -- check-workspace --change docs/changes/active/20261003-gas-unity-v1` 退出码 0，`canProceed: true`，150 条工作区变更中有 135 条范围外未暂存警告、0 个错误；这些范围外文件未被本任务改动。

### Unity 2022.3 临时副本烟测

- 使用 Unity `2022.3.59f1` 对 `Client/GasUnity` 临时副本运行测试，未在原项目打开编辑器；原工程版本仍为 `2022.3.40f1`。
- 首次带 `-quit` 且 `-testPlatform` 使用小写的调用退出码为 0，但没有产生 XML 结果，不计为测试通过。成功调用使用 `-runTests -batchmode -nographics`、`EditMode`/`PlayMode` 平台名，省略 `-quit`。
- EditMode：结果 `Passed`，3 项通过，0 失败、0 未决、0 跳过；XML 为临时副本 `TestResults/editmode-final2.xml`。PlayMode：结果 `Passed`，3 项通过，0 失败、0 未决、0 跳过；XML 为 `TestResults/playmode-final2.xml`。
- EditMode 实际命令：`& "E:\hydra\unity-editor\Unity 2022.3.59f1\Editor\Unity.exe" -runTests -batchmode -nographics -projectPath "C:\Users\yaolinglong\AppData\Local\Temp\Lyking-GasUnity-Smoke-49174c3425f14a88a2b5196a190bd093" -testResults "C:\Users\yaolinglong\AppData\Local\Temp\Lyking-GasUnity-Smoke-49174c3425f14a88a2b5196a190bd093\TestResults\editmode-final2.xml" -testPlatform EditMode -logFile "C:\Users\yaolinglong\AppData\Local\Temp\Lyking-GasUnity-Smoke-49174c3425f14a88a2b5196a190bd093\Logs\editmode-final2.log"`：退出码 0。
- PlayMode 实际命令：`& "E:\hydra\unity-editor\Unity 2022.3.59f1\Editor\Unity.exe" -runTests -batchmode -nographics -projectPath "C:\Users\yaolinglong\AppData\Local\Temp\Lyking-GasUnity-Smoke-49174c3425f14a88a2b5196a190bd093" -testResults "C:\Users\yaolinglong\AppData\Local\Temp\Lyking-GasUnity-Smoke-49174c3425f14a88a2b5196a190bd093\TestResults\playmode-final2.xml" -testPlatform PlayMode -logFile "C:\Users\yaolinglong\AppData\Local\Temp\Lyking-GasUnity-Smoke-49174c3425f14a88a2b5196a190bd093\Logs\playmode-final2.log"`：退出码 0。Unity 日志确认两次均由 2022.3.59f1 执行，结果 XML 均为 `Passed`。
- 为消除 Unity asmdef 编译中 68 条 `CS8632` 注解上下文警告，在 12 个使用或导出 nullable 引用契约的脚本启用 `#nullable`。复跑后 C# 脚本编译诊断为 0 警告、0 错误；Unity 日志仍含许可客户端更新失败及退出阶段 `abort_threads` 信息，但测试 XML 均成功且 Unity 进程退出码为 0。核心 Release 构建为 0 警告、0 错误，.NET 核心测试为 35/35。
- Unity 在临时副本将 `ProjectVersion` 从 `.40f1` 更新到 `.59f1`，并更新了 Collab Proxy、Rider、TextMesh Pro 包锁；这些变化未写入原项目。
- 尚未在固定补丁版本 2022.3.40f1 打开原工程，也未验证真实 `Space` 键盘输入或保存场景截图。

### 本轮文档与工作区复核

- `dotnet .\Tool\AiWorkflow\bin\Release\net10.0\Lyking.AiWorkflow.dll route --text "继续完成 Client/GasUnity v1 的验证证据核对" --path Client/GasUnity --path docs/changes/active/20261003-gas-unity-v1`：退出码 0，主领域 `Client`，风险信号为空。
- `dotnet .\Tool\AiWorkflow\bin\Release\net10.0\Lyking.AiWorkflow.dll check-workspace --change docs/changes/active/20261003-gas-unity-v1`：退出码 0，`canProceed: true`，179 条工作区变更中有 158 条范围外未暂存警告、0 个错误；范围外文件不属于本任务。
- `dotnet .\Tool\AiWorkflow\bin\Release\net10.0\Lyking.AiWorkflow.dll validate --change docs/changes/active/20261003-gas-unity-v1`：退出码 0，`issues: []`、`isValid: true`。
- `git diff --check`：退出码 0，无空白错误；Git 输出既有 LF/CRLF 换行提示。
- 独立复审确认 `#nullable enable` 已覆盖 `GameplayTagContainer` 新增的 nullable 公共签名；将 review 中 Unity 尚未导入编译的文字限定为早期静态审查，并为两组临时副本 Unity 测试补记了完整命令和退出码。
