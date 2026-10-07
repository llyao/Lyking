# Unity 三消

Unity 2022.3 LTS 的单机回合制三消小游戏，复用本仓库既有分层：核心规则在 `Assets/Scripts/Core/` 中保持纯 C#（无 Unity 依赖、注入种子确定性生成与填充），Unity 层只负责键盘/鼠标输入采集与 IMGUI 呈现。打开 `Assets/Scenes/Match3Demo.unity` 进入 Play 模式即可游玩：方向键移动光标、回车选中两格相邻宝石交换（或鼠标直接点击两格），横向或纵向凑满三个同色即消除，上方宝石下落、顶部随机补位，连锁自动续算且倍率累加；`R` 键重开，`S` 键在死局时重排。每局 20 步，步数用尽即结束并显示最终分数。

## 结构

- `Assets/Scripts/Core/`：纯规则（规则常量、事件日志、棋局），`noEngineReferences` 程序集。
- `Assets/Scripts/Unity/`：`Match3DemoBootstrap`（装配、光标与选中状态、输入采集、测试入口 `Swap`/`Select`）、`Match3DemoPanel`（IMGUI 宝石网格、HUD、死局与结束提示、事件日志）。
- `Assets/Tests/EditMode/`、`Assets/Tests/PlayMode/`：Unity 测试工程。
- `Core/`、`Tests/`：dotnet 侧纯逻辑工程与 xunit 测试（与 Unity asmdef 并存，源码引用同一批文件）。

## 玩法要素

- 8x8 网格 6 种宝石；生成时逐格取随机种类并回填，保证初始无现成三连且有可行交换，同种子布局完全一致。
- 交换后一次离散结算：按组计分（3 连 60、4 连 90、5 连及以上 120，同波多组取和）、重力下落、顶部补位；补位后再成三连则连锁继续，第 n 波得分 ×n。
- 无消除的交换自动回退并记无效交换事件，不消耗步数；死局（无可行交换）时面板提示按 `S` 重排或按 `R` 重开，重排保持无三连且有可行解。
- 交换、消除（带组数、格数与连锁）、填充、无效交换、死局重排与对局结束均写入事件日志，日志封顶 128 条。

## 纯逻辑验证

```powershell
dotnet build Client/Match3/Lyking.Match3.slnx
dotnet test Client/Match3/Tests/Lyking.Match3.Core.Tests.csproj
```

Unity EditMode/PlayMode 测试需在 Unity 编辑器（固定 2022.3.40f1）中运行；运行 `Assets/Scenes/Match3Demo.unity` 后保存真实截图作为界面与交互证据。下落动画、特殊宝石、音效与排行榜不在第一版范围内。
