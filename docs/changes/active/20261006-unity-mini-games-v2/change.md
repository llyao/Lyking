<!-- lyking-ai
{"schemaVersion":1,"id":"20261006-unity-mini-games-v2","profile":"standard","status":"active","areas":["Client"],"paths":["docs/changes/active/20261006-unity-mini-games-v2/","Client/Asteroids/","Client/DoodleJumper/","Client/Frogger/","Client/BattleCity/","Client/MazeChase/","Client/Bomberman/","Client/WhackAMole/","Client/SlidingPuzzle/","Client/LightsOut/","Client/FloodIt/","Client/Match3/","Client/Sudoku/","Client/WaterSort/","Client/ConnectFour/","Client/Reversi/","Client/Gomoku/","Client/Blackjack/","Client/Wordle/","Client/Simon/","Client/GameOfLife/"],"riskSignals":[]}
-->

# Unity 小型游戏合集第二批（20 个独立工程）

## 解决什么问题

在第一批 10 个小游戏工程（`20261006-unity-mini-games-v1`）之后，再补齐 20 个可独立打开、可完整游玩的 Unity 2022.3 小型游戏工程，覆盖街机、解谜、棋类、牌类、文字、记忆与元胞自动机演示等类型，全部沿用仓库既定分层（Core 纯 C# 规则、Unity 引导与 IMGUI 呈现、三层测试工程、dotnet 侧纯逻辑工程）。

## 二十个工程与玩法

| 工程 | 类型 | 玩法要点 |
|---|---|---|
| `Client/Asteroids/` | 街机 | 小行星：环形环绕、惯性漂移、三段碎裂计分、重生无敌、逐波提速 |
| `Client/DoodleJumper/` | 街机 | 涂鸦跳跃：下落碰撞弹跳、易碎/移动平台、镜头只升不降、坠落判负 |
| `Client/Frogger/` | 街机 | 青蛙过河：车道车流与河道木排滚动、溺水/撞车判负、五荷花位过关 |
| `Client/BattleCity/` | 街机 | 坦克大战：砖墙可毁钢墙不可、敌我子弹对消、基地防守、全歼过关 |
| `Client/MazeChase/` | 街机 | 吃豆迷宫：转角缓冲连续移动、惊惧期反杀幽灵、清豆过关 |
| `Client/Bomberman/` | 街机 | 炸弹人：十字火焰炸墙、连锁引爆、敌人全灭寻出口 |
| `Client/WhackAMole/` | 街机 | 打地鼠：限时计分、连击加成、炸弹地鼠扣分、停留时长递减 |
| `Client/SlidingPuzzle/` | 解谜 | 数字华容道：3x3/4x4、反向随机打乱保证可解、撤销与多格滑入 |
| `Client/LightsOut/` | 解谜 | 点灯：十字翻转、反向生成保证可解、GF(2) 口径验证 |
| `Client/FloodIt/` | 解谜 | 色彩淹没：BFS 洪水填充扩张、步数限制、同色选择拒绝 |
| `Client/Match3/` | 解谜 | 三消：无效交换回退、组内计分、连锁倍率、重力填充、死局重排 |
| `Client/Sudoku/` | 解谜 | 数独：回溯生成与求解、三档难度挖洞、冲突检测与提示 |
| `Client/WaterSort/` | 解谜 | 水体排序：同色顶层部分倒水、撤销栈、生成谜题 BFS 验证可解 |
| `Client/ConnectFour/` | 棋类 | 四子连珠：重力落子、四向四连、minimax 深度 4 AI |
| `Client/Reversi/` | 棋类 | 黑白棋：8 方向夹翻、过手规则、深度 3 minimax AI 抢角 |
| `Client/Gomoku/` | 棋类 | 五子棋：四向五连、启发式评分 AI（冲四活三优先级） |
| `Client/Blackjack/` | 牌类 | 21 点：软硬点数转换、庄家 17 停牌、加倍与 3:2 赔付、筹码管理 |
| `Client/Wordle/` | 文字 | 猜单词：两趟判定处理双字母、词库校验、QWERTY 键盘染色 |
| `Client/Simon/` | 记忆 | 西蒙：序列播放/复述状态机、前缀稳定序列、最长纪录 |
| `Client/GameOfLife/` | 演示 | 生命游戏：B3/S23 环形网格、滑翔机/滑翔枪图案、暂停单步与绘制 |

## 当前状态

20 个工程全部实现完成；dotnet 侧构建全部 0 警告 0 错误、纯逻辑测试 351 个全部通过、结构一致性检查全部通过（口径与第一批一致，并经既有工程对照校准）。Unity 编辑器侧（导入、EditMode/PlayMode 测试、真实运行截图）因本机无可用 Unity 2022.3 编辑器未执行，与第一批及 Game2048/DungeonFighter 变更记录中的环境缺口一致。

## 完成后应看到什么

每个工程打开 `Assets/Scenes/<游戏名>Demo.unity` 进入 Play 模式即可游玩：顶部状态栏、中央游戏视图、底部事件日志，`R` 键重开；具体按键与规则见各工程 README。纯规则全部在 `Assets/Scripts/Core/` 中，可用 dotnet 直接测试。

## 范围与非目标

- 范围：仅新增上述 20 个工程目录与本变更记录；更新 `Client/README.md` 与 `Client/CONTEXT.md` 的项目登记与验证入口。统一用 IMGUI 与纯代码装配，不引入场景资产。
- 非目标：不修改既有工程（第一批小游戏、GasUnity、GameFramework 等）与其他领域路径（工作区中的其他脏文件均为本任务开始前已存在）；不做音效、精灵动画、存档、最高分持久化、网络与移动端触屏适配。

## 验收方式

- 每个工程 `dotnet build Client/<Game>/Lyking.<Game>.slnx` 0 警告 0 错误；`dotnet test Client/<Game>/Tests/Lyking.<Game>.Core.Tests.csproj` 全绿（20 个工程合计 351 个测试）。
- 一次性 Python 结构检查（8 项：meta 双向完整、GUID 唯一、场景 YAML 可解析、场景脚本/组件引用自洽、EditorBuildSettings 一致、ProjectVersion 2022.3.40f1、asmdef 引用、dotnet 工程齐全）对 20 个工程全部通过。
- Unity EditMode/PlayMode 测试与真实运行截图待主机具备 Unity 2022.3 编辑器后补充。

## 风险与回滚

风险与第一批相同：Unity 内置资源 API 在补丁版本间的差异、手写场景 YAML 的导入兼容性；通过只使用稳定 API（IMGUI + `Texture2D.whiteTexture`）、复用同一场景模板与结构检查缓解。回滚时删除 20 个新工程目录与本变更记录即可，无共享路径影响。
