# 证据

## 2026-10-03

已读取 `CONTEXT-MAP.md`、`Client/CONTEXT.md`、`docs/ai/workflow.md` 和 `docs/ai/verification.md`。使用工作流路由确认本变更主领域为 `Client`，路径为 `Client/GasUnity/` 与本变更文档目录；由于元数据声明架构选择和运行时集成风险，变更档位为 `controlled`。

### 已通过

- `dotnet build Client/GasUnity/Core/Lyking.GameplayAbilities.Core.csproj --configuration Release --nologo`：退出码 0，0 个警告，0 个错误。
- `dotnet test Client/GasUnity/Tests/Lyking.GameplayAbilities.Core.Tests.csproj --configuration Release --nologo`：退出码 0，12 个通过，0 个失败，0 个跳过。覆盖资源/冷却/伤害、必需标签全部匹配、无效配置原子性、事件顺序与观察者异常隔离。
- `dotnet run --project Tool/AiWorkflow/Lyking.AiWorkflow.csproj -- route --text "在 Client/GasUnity 中实现模仿 Unreal GAS 的 Unity 示例" --path Client/GasUnity --path docs/changes/active/20261003-gas-unity-v1`：退出码 0，主领域 `Client`，风险信号为空，文本和显式路径均指向 `Client`。
- `dotnet run --project Tool/AiWorkflow/Lyking.AiWorkflow.csproj -- validate --change docs/changes/active/20261003-gas-unity-v1`：退出码 0，`issues: []`、`isValid: true`。
- `git diff --check`：退出码 0，无空白错误。
- `rg "UnityEngine|UnityEditor" Client/GasUnity/Assets/Scripts/Core`：无匹配，核心源码保持 Unity 无关。
- 二次审查静态计数：`Client/GasUnity/Assets` 下 21 个 `.cs/.asmdef` 文件中 15 个缺少同名 `.meta`；场景、脚本和测试目录也缺少目录元数据。该问题尚未修复。
- 二次审查确认当前 12 个 .NET 测试没有覆盖 `AttributeSet.AttributeChanged` 外部订阅者抛异常、目标属性数值溢出、真实 `Space` 输入、序列化 `.asset` 定义加载等路径。

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

变更保持 `active`，不能在缺少目标 Unity 编辑器和真实运行截图时转换为 `verified`。

