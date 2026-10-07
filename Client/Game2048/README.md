# Unity 2048

Unity 2022.3 LTS 的单机 2048 小游戏，复用 GasUnity 的分层方式：核心规则在 `Assets/Scripts/Core/` 中保持纯 C#（无 Unity 依赖），Unity 层只负责键盘输入、uGUI 构建与刷新。打开 `Assets/Scenes/Game2048.unity` 进入 Play 模式即可游玩：方向键或 WASD 滑动合并，`R` 键或界面按钮重新开始，合成 2048 显示胜利，棋盘满且无可合并时结束。

## 结构

- `Assets/Scripts/Core/`：纯规则（棋盘、滑动合并、随机生成、胜负判定），`noEngineReferences` 程序集。
- `Assets/Scripts/Unity/`：`Game2048Bootstrap`（装配与输入）、`Game2048View`（uGUI 构建与刷新）、`Game2048InputMap`（键位映射）。
- `Assets/Tests/EditMode/`、`Assets/Tests/PlayMode/`：Unity 测试工程。
- `Core/`、`Tests/`：dotnet 侧纯逻辑工程与 xunit 测试（与 Unity asmdef 并存，源码引用同一批文件）。

## 纯逻辑验证

```powershell
dotnet test Client/Game2048/Tests/Lyking.Game2048.Core.Tests.csproj
```

Unity EditMode/PlayMode 测试与运行截图见根目录 `docs/changes/active/20261004-game2048-v1/` 的证据记录。网络、存档、动画补间、触屏手势和最高分持久化不在第一版范围内。
