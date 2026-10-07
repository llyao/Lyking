<!-- lyking-ai
{"schemaVersion":1,"id":"20261004-dungeon-fighter-v1","profile":"standard","status":"active","areas":["Client"],"paths":["docs/changes/active/20261004-dungeon-fighter-v1/","Client/DungeonFighter/"],"riskSignals":[]}
-->

# 2D DNF 类横版动作游戏第一版

## 解决什么问题

在客户端仓库中提供一个可独立打开、可完整游玩的 2D DNF 类横版动作游戏：8 方向移动、跳跃、三段普攻连击、带 MP 与冷却的技能、怪物 AI 与三房间加 Boss 的地下城流程，作为继 GasUnity、Minesweeper、Game2048 之后又一个纯逻辑与 Unity 表现分离的可玩演示工程。

## 当前状态

核心逻辑、dotnet 测试、Unity 工程骨架、演示场景、Bootstrap 与 IMGUI 界面已实现；dotnet 侧验证已通过，等待目标 Unity 2022.3 环境执行 EditMode/PlayMode 测试与运行截图。

## 完成后应看到什么

打开 `Client/DungeonFighter/Assets/Scenes/DungeonFighterDemo.unity` 进入 Play 模式即可游玩：WASD/方向键在地下城地面区域 8 方向移动，`Space` 跳跃，`J` 三段普攻连击，`K` 上挑（高伤害长硬直），`L` 烈焰波（穿透弹道），清空当前房间怪物后右侧门开启，走到门区进入下一房间，击败 Boss 房显示胜利；玩家生命归零显示失败。`R` 键随时重新开始。界面显示房间进度、生命/法力条、连击段数、技能冷却和事件日志。

## 范围

- 新建独立 Unity 2022.3 工程 `Client/DungeonFighter/`，沿用既有分层：Core 纯 C#（无 Unity 依赖）+ Unity 适配层 + EditMode/PlayMode 测试。
- 纯逻辑核心：固定时间步长模拟，包含玩家移动/跳跃/朝向、三段普攻（连击窗口、前冲、第三段高击退）、上挑与烈焰波技能（MP 消耗、冷却、技能拒绝事件）、近战判定（X 距离 + Y 纵深容差、空中目标免伤）、穿透弹道、怪物 AI（巡逻/追击/前摇/恢复、攻击冷却、同屏分离）、伤害/击退/受击硬直/受击无敌帧、三房间怪物配置与开门进房流程、胜负阶段与事件日志；全程无随机数，同输入序列结果确定。
- Unity 层：`DungeonFighterBootstrap`（装配、按键映射、驱动 Tick）、`DungeonFighterPanel`（IMGUI 绘制战场、单位、门、HUD、冷却与日志）。
- 纯逻辑 dotnet xunit 测试与 Unity EditMode/PlayMode 测试。

## 范围与非目标

不做精灵动画与美术资源、不做伤害飘字与音效、不做存档/排行榜/网络/多职业/装备系统、不修改现有 `Client/GasUnity`、`Client/Game2048`、`Client/GameFramework`、`Client/Minesweeper` 等无关目录；不触碰并行变更已持有的脏文件（含 `Client/README.md`、`Client/CONTEXT.md`）。

## 风险与回滚

核心规则与 Unity 生命周期边界失守会导致不可测或表现层反写状态；通过 `noEngineReferences` 程序集边界、只读事件订阅、无随机数确定性模拟与纯逻辑测试约束。战斗数值失衡或 AI 时序问题由确定性 xunit 测试覆盖回归。变更仅新增 `Client/DungeonFighter/` 与本变更目录，回滚可直接删除这两个新增目录，不影响其他工程。

## 验收方式

- `dotnet build Client/DungeonFighter/Lyking.DungeonFighter.slnx`、`dotnet test Client/DungeonFighter/Tests/Lyking.DungeonFighter.Core.Tests.csproj` 真实运行通过。
- Unity 2022.3 打开工程执行 EditMode/PlayMode 测试并保存运行截图（待目标环境，缺口记录到 `evidence.md`）。
