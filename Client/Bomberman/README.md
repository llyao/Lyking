# Unity 炸弹人

Unity 2022.3 LTS 的单机炸弹人小游戏，复用本仓库既有分层：核心规则在 `Assets/Scripts/Core/` 中保持纯 C#（无 Unity 依赖、注入种子完全确定性），Unity 层只负责按键采集与 IMGUI 呈现。打开 `Assets/Scenes/BombermanDemo.unity` 进入 Play 模式即可游玩：方向键或 WASD 每按一次走一格（目标格是硬墙、软墙或炸弹时被拒绝），`Space` 在脚下放置炸弹，`R` 重开一局；炸弹引信 2.5 秒后爆出十字火焰（每个方向延伸 2 格、燃烧 0.5 秒），火焰摧毁软墙 +10、消灭敌人 +100，也会把范围内的其他炸弹立即连锁引爆；藏有出口的软墙被摧毁且敌人全灭后，走上出口进入软墙更密、敌人更多的下一关；3 条生命耗尽即失败。

## 结构

- `Assets/Scripts/Core/`：纯规则（坐标、方向、事件日志、规则表、关卡生成、战局状态机），`noEngineReferences` 程序集。
- `Assets/Scripts/Unity/`：`BombermanDemoBootstrap`（装配、按键映射、测试入口 `Step` 与核心属性）、`BombermanDemoPanel`（IMGUI 棋盘、炸弹倒计时、HUD、出口与结束提示、事件日志）。
- `Assets/Tests/EditMode/`、`Assets/Tests/PlayMode/`：Unity 测试工程。
- `Core/`、`Tests/`：dotnet 侧纯逻辑工程与 xunit 测试（与 Unity asmdef 并存，源码引用同一批文件）。

## 玩法要素

- 13x11 网格：边框与柱状硬墙（横纵坐标均为偶数的格子）固定；软墙按密度从候选空格中不放回抽取填充（第一关 60%、逐关 +1%、封顶 80%），玩家出生点及其右、上邻格保持空置；出口藏在其一软墙之下，同一参数的地图、出口与敌人出生完全一致。
- 炸弹同时最多 1 颗：放置后立即阻挡通行（玩家可从其上走离），引信 2.5 秒；爆炸瞬间结算十字伤害——硬墙挡住该方向，软墙被摧毁（可能显露出口）并挡住该方向，其他炸弹被引燃并入队连锁，范围内的敌人被消灭 +100、玩家被烧到失命。
- 敌人按格点随机游走（由种子驱动的确定性转向）：到格点时直行优先、遇阻在可走方向中随机挑选；与玩家同格即玩家失去一条生命并回到出生点，敌人位置不重置。
- 敌人全灭且出口显露后出口打开，走上出口过关：保留分数与生命，进入软墙密度与敌人数量递增的下一关。
- 事件日志（开局、移动、被拒、放弹、爆炸、摧墙、击杀、烧到、失命、露出口、过关、结束）封顶 128 条，供面板展示与测试断言。

## 纯逻辑验证

```powershell
dotnet build Client/Bomberman/Lyking.Bomberman.slnx
dotnet test Client/Bomberman/Tests/Lyking.Bomberman.Core.Tests.csproj
```

Unity EditMode/PlayMode 测试与运行截图见根目录 `docs/changes/` 的证据记录。关卡编辑器、道具（多弹、尾焰、穿墙）、音效、移动端触屏与网络对战不在第一版范围内。
