# Unity 地下城格斗（2D DNF 类）

Unity 2022.3 LTS 的单机 2D DNF 类横版动作游戏，沿用本仓库既有分层：核心战斗规则在 `Assets/Scripts/Core/` 中保持纯 C#（无 Unity 依赖、无随机数、确定性模拟），Unity 层只负责按键采集、帧驱动与 IMGUI 呈现。打开 `Assets/Scenes/DungeonFighterDemo.unity` 进入 Play 模式即可游玩：WASD/方向键在地下城地面区域 8 方向移动，`Space` 跳跃（空中免疫怪物近战），`J` 三段普攻连击（带前冲与连击窗口），`K` 上挑（高伤害长硬直），`L` 烈焰波（穿透弹道）；清空当前房间后右侧门变绿，走进门区进入下一层，击败哥布林之王通关；生命归零失败。`R` 键随时重开。

## 玩法要素

- 三层地下城：格兰之森入口（史莱姆）→ 幽暗洞窟（史莱姆 + 哥布林）→ 哥布林王座（Boss 房）。
- 三段普攻：命中后 0.9 秒内再按进入下一段，第三段伤害与击退显著提升。
- 上挑：15 MP / 5 秒冷却，命中造成 1.4 秒长硬直；烈焰波：25 MP / 8 秒冷却，穿透直线多个目标。
- 怪物 AI：待机 → 仇恨追击 → 黄色预警前摇 → 挥击 → 恢复；跳跃可躲挥击；受击有无敌帧与硬直。
- MP 每秒回复 6 点；技能在冷却或法力不足时被拒绝并写入事件日志。

## 结构

- `Assets/Scripts/Core/`：纯规则（Vec2、单位、连击与技能规格、怪物 AI、弹道、房间流程、事件日志），`noEngineReferences` 程序集。
- `Assets/Scripts/Unity/`：`DungeonFighterBootstrap`（装配、按键映射、Tick 驱动、测试入口 `Step`）、`DungeonFighterPanel`（IMGUI 战场、单位、HUD、冷却与日志）。
- `Assets/Tests/EditMode/`、`Assets/Tests/PlayMode/`：Unity 测试工程。
- `Core/`、`Tests/`：dotnet 侧纯逻辑工程与 xunit 测试（与 Unity asmdef 并存，源码引用同一批文件）。

## 纯逻辑验证

```powershell
dotnet build Client/DungeonFighter/Lyking.DungeonFighter.slnx
dotnet test Client/DungeonFighter/Tests/Lyking.DungeonFighter.Core.Tests.csproj
```

Unity EditMode/PlayMode 测试与运行截图见根目录 `docs/changes/active/20261004-dungeon-fighter-v1/` 的证据记录。精灵动画、音效、飘字、多职业、装备、存档、网络与排行榜不在第一版范围内。
