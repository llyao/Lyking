# Unity 坦克大战

Unity 2022.3 LTS 的单机坦克大战小游戏，复用本仓库既有分层：核心规则在 `Assets/Scripts/Core/` 中保持纯 C#（无 Unity 依赖、注入种子确定性随机），Unity 层只负责按键采集、按固定 1/120 秒子步长驱动模拟与 IMGUI 呈现。打开 `Assets/Scenes/BattleCityDemo.unity` 进入 Play 模式即可游玩：方向键或 WASD 四向移动，空格或 J 射击，`R` 键重开；玩家子弹命中敌人 +100 分，敌人子弹命中玩家扣 1 命并回出生点获得 2 秒无敌，砖墙可被炮火摧毁而钢墙不可，基地被击中或 3 命用尽游戏结束，全歼本关 6 名敌人过关（砖墙重置、敌速 +10%）。

## 结构

- `Assets/Scripts/Core/`：纯规则（坐标、方向、输入、规则常量、网格地图、事件日志、实体与战局模拟），`noEngineReferences` 程序集。
- `Assets/Scripts/Unity/`：`BattleCityDemoBootstrap`（装配、按键映射、模拟入口 `Step`）、`BattleCityDemoPanel`（IMGUI 网格战场、坦克、子弹、HUD、结束提示与事件日志）。
- `Assets/Tests/EditMode/`、`Assets/Tests/PlayMode/`：Unity 测试工程。
- `Core/`、`Tests/`：dotnet 侧纯逻辑工程与 xunit 测试（与 Unity asmdef 并存，源码引用同一批文件）。

## 玩法要素

- 13x13 网格地图（`#` 钢墙、`B` 砖墙、`.` 空地、`E` 基地、`P` 玩家出生），坦克 1 格体宽、连续坐标移动、四向转向：玩家速度 3.2，敌人 2.6 起步、每关提速 10%。
- 玩家射击冷却 0.5 秒、子弹速度 6；敌人每 3 秒从顶部三个出生格轮换入场（种子决定起点，并决定追玩家还是追基地），转向在格心按"朝目标方向 + 种子随机平局"的纯函数决策，撞墙贴停的位置恰为格心、天然触发重选；坦克互撞时停在原地等待通路。
- 固定 1/120 秒子步长推进，运算顺序固定，同种子同输入的整局模拟完全一致；事件日志保留最近 128 条。

## 纯逻辑验证

```powershell
dotnet build Client/BattleCity/Lyking.BattleCity.slnx
dotnet test Client/BattleCity/Tests/Lyking.BattleCity.Core.Tests.csproj
```

Unity EditMode/PlayMode 测试与运行截图见根目录 `docs/changes/active/20261006-unity-mini-games-v1/` 的证据记录。多关卡地图、道具、双打、音效与最高分持久化不在第一版范围内。
