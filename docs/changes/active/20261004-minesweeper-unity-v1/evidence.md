# 验证证据

## 2026-10-04

### 路由与范围

- 命令：`dotnet Tool/AiWorkflow/bin/Release/net10.0/Lyking.AiWorkflow.dll route --text "用Unity做一个扫雷项目" --path Client/Minesweeper --path docs/changes/active/20261004-minesweeper-unity-v1`
- 退出码：`0`；主领域 `Client`，建议档位 `standard`，风险信号为空。变更元数据声明 `architecture-choice`、`runtime-integration`，按风险标准变更执行独立审查。

### 纯逻辑构建与测试

- `dotnet build Client/Minesweeper/Core/Lyking.Minesweeper.Core.csproj --configuration Release --nologo`：退出码 `0`，0 个警告，0 个错误。
- `dotnet test Client/Minesweeper/Tests/Lyking.Minesweeper.Core.Tests.csproj --configuration Release --nologo`：独立审查修复后最终复跑退出码 `0`，43 个通过，0 个失败，0 个跳过（首轮 42 个通过，审查后新增 `重复翻开已开格返回已翻开且无事件`）。覆盖难度预设与非法规格拒绝、初始状态、越界快速失败、重复翻开拒绝、首击安全布雷与邻域排除、固定种子雷区复现、邻雷计数一致性、洪水展开与数字格行为、插旗往返与旗格阻挡、首击前插旗、剩余雷数为负、胜利与失败判定、结束后操作拒绝、失败后错误旗识别、观察者异常隔离、事件序列、小棋盘排除集收缩（2x2/3x3）和固定种子随机序列稳定。
- 测试探针法说明：被测棋盘与探针棋盘使用相同种子并执行完全相同的首击序列，保证雷区布局一致；洪水区域期望值用独立 BFS 计算后与核心实现比对。
- `grep -rlE '^using (UnityEngine|UnityEditor);' Client/Minesweeper/Assets/Scripts/Core --include='*.cs'`：退出码 `1`（无匹配），核心源码保持引擎无关。

### 结构与工作区

- 资产 `.meta` 完整性：`Client/Minesweeper/Assets` 下全部文件与子目录（不含 `Assets` 根，沿用 GasUnity 惯例）均有同名 `.meta`，`MISSING_META=0`。
- GUID 唯一性：21 个新资产 GUID 由脚本生成并与仓库全部现存 `.meta`/`.unity` GUID 查重无冲突；场景 `MinesweeperDemo.unity` 引用的脚本 GUID 与对应 `.cs.meta` 一致，`EditorBuildSettings.asset` 场景 GUID 与 `MinesweeperDemo.unity.meta` 一致。
- `git diff --check`：退出码 `0`，无空白错误；Git 输出既有 LF/CRLF 换行提示不影响检查结果。
- `git check-ignore` 核对：`Core/bin`、`Tests/obj` 命中仓库全局忽略（`[Bb]in/`、`[Oo]bj/`）；`Library/`、`Logs/`、`Temp/`、`UserSettings/`、`TestResults/` 由新增的 `Client/Minesweeper/.gitignore` 覆盖（沿用 `Client/UnityEcsDemos/.gitignore` 内容），Unity 生成目录不会进入版本控制。

### Unity 2022.3 临时副本烟测（审查修复后最终轮）

- 使用 Unity `2022.3.59f1`（`E:\hydra\unity-editor\Unity 2022.3.59f1\Editor\Unity.exe`）对 `Client/Minesweeper` 临时副本（`C:\Users\yaolinglong\AppData\Local\Temp\Lyking-Minesweeper-Smoke-1600`，复制 `Assets`、`Packages`、`ProjectSettings`）运行测试；未在原项目打开编辑器，原工程 `ProjectVersion.txt` 仍为 `2022.3.40f1`。
- 首轮 EditMode 调用退出码 `0` 但无结果 XML：PlayMode 测试源码缺少 `using Lyking.Minesweeper.Unity;` 导致 `Lyking.Minesweeper.PlayModeTests.dll` 编译失败（`CS0246`），该次不计为通过。补齐 using 后重新同步临时副本并复跑。
- 独立审查修复（`Start` 保留已建棋盘、重开测试插入帧推进、新增面板命中测试等）同步到临时副本后进行最终轮复跑。
- EditMode 最终命令：`& "E:\hydra\unity-editor\Unity 2022.3.59f1\Editor\Unity.exe" -runTests -batchmode -nographics -projectPath "C:\Users\yaolinglong\AppData\Local\Temp\Lyking-Minesweeper-Smoke-1600" -testResults "...\TestResults\editmode-r2.xml" -testPlatform EditMode -logFile "...\Logs\editmode-r2.log"`：退出码 `0`；结果 XML `Passed`，4 个通过、0 失败（难度预设与最小规格、固定种子首击安全与踩雷判负、演示场景资产存在、面板坐标命中测试）。
- PlayMode 最终命令：同上，`-testResults "...\TestResults\playmode-r2.xml" -testPlatform PlayMode -logFile "...\Logs\playmode-r2.log"`：退出码 `0`；结果 XML `Passed`，6 个通过、0 失败（场景首击计时、插旗与重开含真实帧推进的计时归零断言、非法坐标、固定种子判负、固定种子判胜、场景销毁无异常日志）。
- 两轮日志均确认由 `2022.3.59f1` 执行；最终轮 `warning CS` 与 `error CS` 计数均为 `0`。日志含许可客户端更新失败（`Bad ULF signature`、`Unable to update licenses`）与退出阶段噪音，与 GasUnity 既有记录一致，测试 XML 均为 `Passed` 且 Unity 进程退出码为 `0`。
- 场景 `MinesweeperDemo.unity` 在 PlayMode 中由 `SceneManager.LoadSceneAsync` 真实加载 3 次，面板与引导脚本反序列化正常，无 `LogAssert` 意外记录。

### 独立审查

- 只读独立审查代理完成审查：无 S1/S2，6 项 S3 建议全部处置（修复或在证据中如实记录），结论通过；全过程与处置见本目录 `review.md`。

### 当前缺口

- 未在工程固定的 `2022.3.40f1` 打开原工程；本机无该补丁版本，临时副本验证不能证明固定版本的导入与测试差异。
- 未验证真实鼠标输入（左键翻开、右键插旗经 `OnGUI` 事件流的接线）与真实 Game 视图渲染；自动化覆盖到 `TryGetCellIndex` 命中映射与公共动作入口，真实交互与运行截图需人工在 Unity 编辑器中验收。
- 变更保持 `active`：上述人工验收项完成后方可进入 `verified`。
