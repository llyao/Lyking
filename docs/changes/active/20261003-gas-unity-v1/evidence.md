# 证据

## 2026-10-03

已读取 `CONTEXT-MAP.md`、`Client/CONTEXT.md`、`docs/ai/workflow.md` 和 `docs/ai/verification.md`。使用工作流路由确认本变更主领域为 `Client`，路径为 `Client/GasUnity/` 与本变更文档目录；由于元数据声明架构选择和运行时集成风险，变更档位为 `controlled`。

### 已通过

- `dotnet build Client/GasUnity/Core/Lyking.GameplayAbilities.Core.csproj --configuration Release --nologo`：退出码 0，0 个警告，0 个错误。
- `dotnet test Client/GasUnity/Tests/Lyking.GameplayAbilities.Core.Tests.csproj --configuration Release --nologo`：退出码 0，7 个通过，0 个失败，0 个跳过。
- `dotnet run --project Tool/AiWorkflow/Lyking.AiWorkflow.csproj -- route --text "在 Client/GasUnity 中实现模仿 Unreal GAS 的 Unity 示例" --path Client/GasUnity --path docs/changes/active/20261003-gas-unity-v1`：退出码 0，主领域 `Client`，风险信号为空，文本和显式路径均指向 `Client`。

### 当前缺口

- 目标 Unity 编辑器为 Unity 2022.3 LTS。当前主机只发现 `C:\Program Files\Unity\Hub\Editor\6000.4.0f1\Editor\Unity.exe`，没有 Unity 2022.3 可执行文件，因此未把 Unity 6 的结果当作目标版本证据。
- 以下目标命令尚未执行：
  - `Unity.exe -batchmode -quit -projectPath Client/GasUnity -runTests -testPlatform editmode -testResults Client/GasUnity/TestResults/editmode.xml`
  - `Unity.exe -batchmode -quit -projectPath Client/GasUnity -runTests -testPlatform playmode -testResults Client/GasUnity/TestResults/playmode.xml`
- 尚未保存 `GasDemo.unity` 的真实运行截图；需要在 Unity 2022.3 编辑器中确认玩家 Mana、训练假人 Health、冷却拒绝和事件日志。

变更保持 `active`，不能在缺少目标 Unity 编辑器和真实运行截图时转换为 `verified`。

