# 证据

## 2026-10-03

已读取 `CONTEXT-MAP.md`、`Client/CONTEXT.md`、`docs/ai/workflow.md` 和 `docs/ai/verification.md`。使用工作流路由确认本变更主领域为 `Client`，路径为 `Client/GasUnity/` 与本变更文档目录；由于元数据声明架构选择和运行时集成风险，变更档位为 `controlled`。

### 已通过

- `dotnet build Client/GasUnity/Core/Lyking.GameplayAbilities.Core.csproj --configuration Release --nologo`：退出码 0，0 个警告，0 个错误。
- `dotnet test Client/GasUnity/Tests/Lyking.GameplayAbilities.Core.Tests.csproj --configuration Release --nologo`：退出码 0，25 个通过，0 个失败，0 个跳过。覆盖资源/冷却查询、动态伤害、必需标签全部匹配、目标溢出、空能力 ID、观察者异常、不可变标签、效果规格数据流和时钟边界。
- `dotnet run --project Tool/AiWorkflow/Lyking.AiWorkflow.csproj -- route --text "在 Client/GasUnity 中实现模仿 Unreal GAS 的 Unity 示例" --path Client/GasUnity --path docs/changes/active/20261003-gas-unity-v1`：退出码 0，主领域 `Client`，风险信号为空，文本和显式路径均指向 `Client`。
- `dotnet run --project Tool/AiWorkflow/Lyking.AiWorkflow.csproj -- validate --change docs/changes/active/20261003-gas-unity-v1`：退出码 0，`issues: []`、`isValid: true`。
- `git diff --check`：退出码 0，无空白错误。
- `rg "UnityEngine|UnityEditor" Client/GasUnity/Assets/Scripts/Core`：无匹配，核心源码保持 Unity 无关。
- 二次审查静态计数：`Client/GasUnity/Assets` 下源文件、程序集、场景和资产均已存在同名 `.meta`，目录元数据也已补齐。
- 原子提交测试已覆盖 `AttributeSet.AttributeChanged` 外部订阅者异常和目标属性数值溢出；Unity EditMode/PlayMode 仍未在目标编辑器执行。
- Unity 资产接入检查：`Assets/Definitions/FireballAbility.asset` 已创建并被 `GasDemo.unity` 引用；EditMode 测试增加了序列化资产加载。PlayMode 输入测试通过 `ProcessInput(KeyCode.Space)` 覆盖与 `Update` 相同的输入分发入口。
- Unity 适配编译面审查发现 `GasActor`/`GasDemoPanel` 使用的 `GetCooldownRemaining` 原先缺失；已在核心组件补充只读查询并新增冷却剩余时间测试，当前核心测试为 25/25。Unity 适配程序集仍未在目标编辑器实际编译。
- 效果数据流检查：`FireballAbility.asset` 引用 `FireballDamageEffect.asset`，`GameplayAbilityDefinition.ToRuntimeAbility()` 生成带 `GameplayEffectSpec` 的运行时能力，核心测试覆盖效果默认增量与 `AttackPower` 覆盖。
- 手动时钟边界检查：`Schedule` 和 `Advance` 拒绝负数、`NaN`、正负无穷，参数化测试覆盖三类非有限输入。
- 手动时钟溢出检查：`Now + seconds` 与 `Now + delaySeconds` 的非有限结果会被拒绝，溢出测试确认时钟状态和定时器列表不发生变化。
- 设计口径复核：已将“修饰器叠加”明确收敛为本版的单一瞬时效果修饰器，持续效果生命周期和通用多修饰器应用列为后续独立范围。

### 当前缺口

- 目标 Unity 编辑器为 Unity 2022.3 LTS。当前主机只发现 `C:\Program Files\Unity\Hub\Editor\6000.4.0f1\Editor\Unity.exe`，没有 Unity 2022.3 可执行文件，因此未把 Unity 6 的结果当作目标版本证据。
- 以下目标命令尚未执行：
  - `Unity.exe -batchmode -quit -projectPath Client/GasUnity -runTests -testPlatform editmode -testResults Client/GasUnity/TestResults/editmode.xml`
  - `Unity.exe -batchmode -quit -projectPath Client/GasUnity -runTests -testPlatform playmode -testResults Client/GasUnity/TestResults/playmode.xml`
- 尚未保存 `GasDemo.unity` 的真实运行截图；需要在 Unity 2022.3 编辑器中确认玩家 Mana、训练假人 Health、冷却拒绝和事件日志。

### 审查修复

- 能力构造和属性/效果规格现在拒绝空属性名、负数或非有限数值，并在提交前完成伤害来源校验。
- 必需标签改为全部满足；火球伤害优先读取施法者 `AttackPower`，未配置时回退到能力伤害值。
- 目标属性事件在效果事件之后发布，重复的施法者属性事件已移除；事件观察者异常不会中断已提交状态。
- 演示面板显示 Fireball 剩余冷却，Bootstrap 在销毁时退订事件。

### 本轮修复后的复跑

- `dotnet build Client/GasUnity/Core/Lyking.GameplayAbilities.Core.csproj --configuration Release --nologo`：退出码 0，0 个警告，0 个错误。
- `dotnet test Client/GasUnity/Tests/Lyking.GameplayAbilities.Core.Tests.csproj --configuration Release --nologo`：退出码 0，25 个通过，0 个失败，0 个跳过。
- `dotnet run --project Tool/AiWorkflow/Lyking.AiWorkflow.csproj -- route --text "在 Client/GasUnity 中继续修复效果数据流和逻辑时钟边界" --path Client/GasUnity --path docs/changes/active/20261003-gas-unity-v1`：退出码 0，主领域 `Client`，风险信号为空。
- `dotnet run --project Tool/AiWorkflow/Lyking.AiWorkflow.csproj -- validate --change docs/changes/active/20261003-gas-unity-v1`：退出码 0，`issues: []`、`isValid: true`。
- `git diff --check`：退出码 0；`Client/GasUnity/Assets` 同名 `.meta` 检查 `MISSING_META=0`；核心源码 Unity 引用检查 `CORE_UNITY_REFERENCES=0`。

变更保持 `active`，不能在缺少目标 Unity 编辑器和真实运行截图时转换为 `verified`。

