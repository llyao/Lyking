<!-- lyking-ai
{"schemaVersion":1,"id":"20261004-game2048-v1","profile":"standard","status":"active","areas":["Client"],"paths":["docs/changes/active/20261004-game2048-v1/","Client/Game2048/"],"riskSignals":[]}
-->

# Unity 2048 单机小游戏

## 解决什么问题

在客户端仓库中提供一个可独立打开、可完整游玩的 Unity 2048 工程：4x4 棋盘、方向键/WASD 滑动、经典合并计分、随机新块、胜负判定和重新开始，作为继 GasUnity 之后第二个纯逻辑与 Unity 表现分离的演示工程。

## 当前状态

核心逻辑与 dotnet 测试已实现并通过；Unity 工程骨架与场景、Bootstrap、uGUI 视图已就位，等待目标 Unity 环境验证。

## 完成后应看到什么

打开 `Client/Game2048/Assets/Scenes/Game2048.unity` 后直接进入 Play 模式即可游玩：方向键或 WASD 滑动合并方块，界面显示当前分数、步数状态与胜利/结束提示，`R` 键或按钮重新开始。滑动合并、生成与胜负判定全部来自 `Assets/Scripts/Core/` 的纯 C# 逻辑，dotnet 测试可直接运行。

## 范围与非目标

- 新建独立 Unity 2022.3 工程 `Client/Game2048/`，复用 GasUnity 的分层：Core 纯 C# + Unity 适配层 + EditMode 测试。
- 纯逻辑：棋盘、四方向滑动合并（每块每次移动只合并一次）、90%/10% 随机新块、得分、可玩/胜利/结束状态。
- Unity 层：代码构建 uGUI（棋盘、数字、分数、提示、重开按钮）、键盘输入映射、视图刷新。
- 纯逻辑 dotnet xunit 测试与 Unity EditMode 测试。

非目标：不做动画/移动补间、触屏手势输入、最高分持久化、存档、网络、Undo，不修改现有 `Client/GasUnity`、`Client/GameFramework` 与其他目录。

## 验收方式

- `dotnet build Client/Game2048/Lyking.Game2048.slnx`、`dotnet test Client/Game2048/Tests/Lyking.Game2048.Core.Tests.csproj` 真实运行通过。
- 工作流 `validate`、`git diff --check` 通过。
- Unity EditMode 测试在目标编辑器（2022.3.40f1 或当前主机可用 2022.3.59f1）执行通过；真实运行截图待主机条件满足后补充。

## 风险与回滚

主要风险是 Unity 内置字体/资源 API 在补丁版本间的差异导致视图构建失败；通过只用稳定 API（uGUI Image/Text + 动态白图纹理）降低风险。回滚时删除 `Client/Game2048/` 与本变更文档即可，无共享路径影响。
