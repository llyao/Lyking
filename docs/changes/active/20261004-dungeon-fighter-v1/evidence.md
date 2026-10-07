# 证据记录

## 环境

- 主机：Windows 10 Professional x64，已安装 dotnet SDK（net10.0 可用）与 Unity Hub。
- Unity 编辑器：本机无固定补丁版本 2022.3.40f1；实际使用 `C:\Program Files\Unity 2022.3.59f1\Editor\Unity.exe` 在临时副本（`%LOCALAPPDATA%\Temp\LykingDungeonFighterUnityTest`）中执行测试，正式工程 `ProjectVersion.txt` 保持 2022.3.40f1 未被触碰。

## dotnet 纯逻辑验证（本会话真实运行）

命令与结果（2026-10-04，仓库根目录执行）：

```text
$ dotnet build Client/DungeonFighter/Lyking.DungeonFighter.slnx
已成功生成。 0 个警告 0 个错误

$ dotnet test Client/DungeonFighter/Tests/Lyking.DungeonFighter.Core.Tests.csproj
已通过! - 失败: 0，通过: 18，已跳过: 0，总计: 18
```

覆盖内容：初始状态与房间进度、怪物按延迟落地、8 方向移动边界、朝向翻转、胜负后停止推进、普攻判定伤害与击退、连击窗口推进与伤害累加、连击窗口过期重置、攻击缓冲接段、上挑 MP/冷却/拒绝、法力不足拒绝、烈焰波穿透多目标、跳跃躲攻击、受击无敌帧（总量守恒 + 相邻受伤间隔大于无敌帧）、同输入序列确定性、未清房不切层、清房开门进下层、末层清空通关。

## 工作区工具校验

```text
$ dotnet run --project Tool/AiWorkflow/Lyking.AiWorkflow.csproj -- route --text "写一个2d dnf 类的游戏" --path Client
primaryArea=Client，riskSignals=[]，suggestedProfile=standard

$ dotnet run --project Tool/AiWorkflow/Lyking.AiWorkflow.csproj -- validate --change docs/changes/active/20261004-dungeon-fighter-v1
（补齐风险与回滚章节与 evidence.md 后复验）
```

## Unity 验证（2022.3.59f1 临时副本）

- 第一次 EditMode 失败：`Packages/manifest.json` 误写了不存在的 `com.unity.modules.androidprofile` 模块，包解析报错退出；已从正式工程与副本移除该行后重跑。
- 最终结果见下节复验记录。

## 复验记录

- EditMode（2022.3.59f1，临时副本，本会话真实运行）：`test-run result="Passed" total="3" passed="3" failed="0"`，退出码 0。用例：对局在引擎外可直接创建并推进、三段普攻在引擎边界内同样推进连击、事件日志保留最近条目。
- PlayMode（2022.3.59f1，临时副本，本会话真实运行）：`test-run result="Passed" total="2" passed="2" failed="0"`，退出码 0。用例：场景启动后对局就绪且面板已绑定、模拟入口推进对局且重开复位。
- PlayMode 曾失败一轮：场景引用的 `DungeonFighterPanel` 脚本 GUID 与实际 `.meta` 不一致（生成 meta 时漏绑预定 GUID），Unity 报 "The referenced script (Unknown) on this Behaviour is missing!"；已把 `DungeonFighterPanel.cs.meta` 修正为场景引用的 `0147a0c6ed2d7725008d8a138363ec63` 后复跑通过。
- 运行截图：本会话为批处理无头模式运行，未产出人工游玩截图；打开 `Assets/Scenes/DungeonFighterDemo.unity` 人工游玩并截图仍待执行。

## 缺口

- 固定补丁版本 2022.3.40f1 的导入差异未验证（本机未安装该版本）。
- 真实 `Space`/`J`/`K`/`L` 键盘输入与画面观感的人工验证与截图未完成。
- `Client/README.md` 与 `Client/CONTEXT.md` 因并行变更持有脏文件，本次未修改；后续归档前需补充游戏条目。
