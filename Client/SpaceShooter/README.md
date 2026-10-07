# Unity 太空射击

Unity 2022.3 LTS 的单机太空射击小游戏，复用本仓库既有分层：核心规则在 `Assets/Scripts/Core/` 中保持纯 C#（无 Unity 依赖、注入种子确定性随机），Unity 层只负责按键采集、按固定 1/120 秒子步长驱动模拟与 IMGUI 呈现。打开 `Assets/Scenes/SpaceShooterDemo.unity` 进入 Play 模式即可游玩：方向键或 A/D 横移，空格或 J 射击，`R` 键重开；子弹命中敌人 +100 分，敌人撞玩家扣 1 命，敌人落底逃逸即移除，3 命用尽游戏结束，每 10 次击杀升一波（生成间隔缩短、敌速 +10%）。

## 结构

- `Assets/Scripts/Core/`：纯规则（坐标、输入、规则常量、事件日志、实体与战局模拟），`noEngineReferences` 程序集。
- `Assets/Scripts/Unity/`：`SpaceShooterDemoBootstrap`（装配、按键映射、模拟入口 `Step`）、`SpaceShooterDemoPanel`（IMGUI 星空场地、HUD、结束提示与事件日志）。
- `Assets/Tests/EditMode/`、`Assets/Tests/PlayMode/`：Unity 测试工程。
- `Core/`、`Tests/`：dotnet 侧纯逻辑工程与 xunit 测试（与 Unity asmdef 并存，源码引用同一批文件）。

## 玩法要素

- 20x14 逻辑场地（Y 向上），玩家底部横移（速度 8，夹在场内），射击冷却 0.35 秒，子弹速度 12。
- 敌人从场顶外随机横坐标出生（由注入种子决定），以 2.2 单位/秒下落并绕出生锚点 0.5Hz 正弦横摆；撞玩家扣命，完整落出场地记逃逸。
- 固定 1/120 秒子步长推进，运算顺序固定，同种子同输入的整局模拟完全一致；事件日志保留最近 128 条。

## 纯逻辑验证

```powershell
dotnet build Client/SpaceShooter/Lyking.SpaceShooter.slnx
dotnet test Client/SpaceShooter/Tests/Lyking.SpaceShooter.Core.Tests.csproj
```

Unity EditMode/PlayMode 测试与运行截图见根目录 `docs/changes/active/20261006-unity-mini-games-v1/` 的证据记录。音效、敌机类型、道具、最高分持久化、手柄与触屏操作不在第一版范围内。
