# 验证证据

## 2026-10-04

### 路由与范围

- 命令：`dotnet run --project Tool/AiWorkflow/Lyking.AiWorkflow.csproj -- route --text "用 Unity 做一个 2048 单机小游戏" --path "Client/"`
- 退出码：`0`；主领域为 `Client`，无风险信号，建议档位为 `standard`。
- 范围仅新增 `Client/Game2048/` 与本变更目录；未修改 `Client/GasUnity`、`Client/GameFramework` 及其他脏文件。

### 核心逻辑测试（真实运行）

- 命令：`dotnet test Client/Game2048/Tests/Lyking.Game2048.Core.Tests.csproj`
- 首轮：`失败 3，通过 28`；失败集中于"无效移动后未刷新结束状态"，源于通过直接填满棋盘构造的已终局面在无效移动时不重算 `IsGameOver`。
- 修复：`Game2048Game.Move` 在移动无变化时也检查结束状态（不生成新块，不加分）。
- 复跑：`已通过! - 失败: 0，通过: 31，已跳过: 0`（GameRulesTests、GameBoardTests、Game2048GameTests）。

### 编译（真实运行）

- 命令：`dotnet build Client/Game2048/Lyking.Game2048.slnx`
- 结果：`0 个警告，0 个错误`。

### 工作流与结构

- `git diff --check`：退出码 `0`，无空白错误（仅存量脏文件的行尾转换警告，不属于本变更）。
- 变更内 GUID 一致性：场景 `Game2048.unity` 中 Bootstrap 脚本引用 `c77df7e2f2847a0e5b62ae99142021a6` 与 `Game2048Bootstrap.cs.meta` 一致；`EditorBuildSettings.asset` 场景引用 `607517e86ecd4f90b12a9112cfdaa5cb` 与 `Game2048.unity.meta` 一致。
- 新增 `Client/Game2048/.gitignore` 覆盖 Library/Temp/Logs/UserSettings 等 Unity 生成目录。

### 结构一致性烟测（真实运行）

一次性 Python 脚本（PyYAML 逐文档解析，存于会话临时目录，不入仓库）对 `Client/Game2048/` 执行六项检查：Assets 内文件与 meta 双向完整、GUID 唯一、场景 YAML 全部 7 个文档可解析、组件 fileID 与 `m_Script` guid 引用自洽、EditorBuildSettings 场景 guid/路径一致、ProjectVersion 为 2022.3.40f1、asmdef 引用完整。

- `python g2048_struct_check.py Client/Game2048`：退出码 `0`，`OK: 23 个 meta、场景 7 个 YAML 文档、7 个 fileID、构建设置与版本一致`。
- 对照组 `python g2048_struct_check.py Client/GasUnity GasDemo.unity`：退出码 `0`（36 个 meta、16 个文档、16 个 fileID）；该工程曾通过 Unity 导入，证明检查口径与可导入格式一致。

### Unity 环境缺口（含本机搜索结论）

- 工程固定 Unity 2022.3.40f1。编辑器搜索结论：Unity Hub `secondaryInstallPath.json` 为空（使用默认路径）；默认安装目录 `C:\Program Files\Unity\Hub\Editor\` 仅有 `6000.4.0f1`；`C:`、`D:` 全盘 `find` 无其他 `Unity.exe`；Hub 日志无 2022.3.59f1 安装记录。GasUnity 上下文提到的 2022.3.59f1 已不在本机。
- 许可证探测：`Unity.exe -batchmode -nographics -quit`，输出 `No valid Unity Editor license found. Please activate your license.`（授权查询返回 `Found 0 entitlement groups and 0 free entitlements`），6000.4.0f1 编辑器也不可用。
- 因此本日未执行工程导入、EditMode（`Game2048EditModeTests`）、PlayMode（`Game2048PlayModeTests`）测试与真实运行截图。
- PlayMode 测试通过 `SceneManager.LoadSceneAsync("Game2048")` 依赖 Build Settings（`EditorBuildSettings.asset` 已包含该场景）。

### 当前状态

变更保持 `active`：纯逻辑已验证，Unity 编辑器验证与运行截图为后续步骤。
