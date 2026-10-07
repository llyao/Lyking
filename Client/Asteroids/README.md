# Unity 小行星

Unity 2022.3 LTS 的单机小行星射击小游戏，复用本仓库既有分层：核心规则在 `Assets/Scripts/Core/` 中保持纯 C#（无 Unity 依赖、注入种子确定性随机），Unity 层只负责按键采集、按固定 1/120 秒子步长驱动模拟与 IMGUI 呈现。打开 `Assets/Scenes/AsteroidsDemo.unity` 进入 Play 模式即可游玩：方向键或 A/D 旋转，方向键上或 W 推进，空格或 J 射击，`R` 键重开；大档小行星 20 分、中档 50 分、小档 100 分，大档碎成 2 个中档、中档碎成 2 个小档、小档直接消灭，飞船被撞扣 1 命并场心重生（无敌 2.5 秒，表现层闪烁），3 命用尽游戏结束，全灭进入下一波（数量 +1、速度 +10%）。

## 结构

- `Assets/Scripts/Core/`：纯规则（坐标、输入、规则常量、事件日志、实体与战局模拟），`noEngineReferences` 程序集。
- `Assets/Scripts/Unity/`：`AsteroidsDemoBootstrap`（装配、按键映射、模拟入口 `Step`）、`AsteroidsDemoPanel`（IMGUI 星空场地、飞船与小行星色块、HUD、无敌闪烁、结束提示与事件日志）。
- `Assets/Tests/EditMode/`、`Assets/Tests/PlayMode/`：Unity 测试工程。
- `Core/`、`Tests/`：dotnet 侧纯逻辑工程与 xunit 测试（与 Unity asmdef 并存，源码引用同一批文件）。

## 玩法要素

- 24x16 逻辑场地（Y 向上），全场环形环绕：飞船、子弹与小行星越界即从对侧穿出（纯模运算，确定可测）。
- 飞船场心出生朝上，转向 4 弧度/秒，推进加速度 10、最大速度 9、空间阻尼每秒 0.45，射击冷却 0.3 秒、子弹速度 16、寿命 1.1 秒且不继承船速。
- 小行星三档半径 1.6/1.0/0.55，漂移方向与自旋由注入种子决定，出生点避开飞船；子弹与飞船均按圆-圆相交判定。
- 固定 1/120 秒子步长推进，运算顺序固定，同种子同输入的整局模拟完全一致；事件日志保留最近 128 条。

## 纯逻辑验证

```powershell
dotnet build Client/Asteroids/Lyking.Asteroids.slnx
dotnet test Client/Asteroids/Tests/Lyking.Asteroids.Core.Tests.csproj
```

Unity EditMode/PlayMode 测试与运行截图见根目录 `docs/changes/active/20261006-unity-mini-games-v1/` 的证据记录。音效、飞碟敌机、道具、最高分持久化、手柄与触屏操作不在第一版范围内。
