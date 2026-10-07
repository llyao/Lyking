# 独立审查

## 2026-10-04 只读独立审查

- 审查者：独立会话的只读审查代理，未参与实现，未修改文件，未运行测试；审查对象为当时工作区的实现、三套测试、场景 YAML、`EditorBuildSettings.asset`、全部 `.meta` GUID 接线，并与 `Client/GasUnity` 既有工程约定逐项对照。
- 覆盖：核心 5 个源文件（布雷排除集收缩算法、部分洗牌、洪水 BFS、胜负判定、事件发布与观察者异常隔离）、Unity 适配 2 个源文件（生命周期订阅/退订配对、IMGUI 网格与右键命中、计时语义、序列化字段与场景 YAML 一致性）、dotnet xunit 42 例与 EditMode/PlayMode 测试的断言有效性、固定种子探针法布局一致性、asmdef/csproj/slnx/manifest/ProjectVersion 与 GasUnity 惯例的一致性。

### 结论

- 严重（S1）：未发现。中级（S2）：未发现。
- 低级（S3）6 项：`AlreadyRevealed` 路径零覆盖；IMGUI 命中映射无自动化测试；change.md 固定版本与临时副本版本表述需读证据文件才能自洽且范围小节重复；`Start` 前调用 `Restart`/`SelectDifficulty` 会被覆盖；两处弱断言（重开计时不重置也恒为 0、Beginner 雷数恒真断言）；表现层瑕疵（错误旗与雷同色、数字 7/8 落默认色、测试名"双人小棋盘"误导）。
- 审查确认通过项：排除集收缩循环终止性、洪水展开无重复计数、事件顺序（MineHit→Lost、Revealed→FloodRevealed→Won）、观察者异常隔离、GUID 接线自洽、订阅退订配对、探针法交叉校验。
- 最终结论：**通过**（无 S1/S2）。

### S3 处置

- 已修复并复验：补 `重复翻开已开格返回已翻开且无事件` dotnet 测试；新增 EditMode `面板坐标命中测试使用默认网格布局`（格中心命中、间隙未命中、越界未命中的表驱动断言）；`Start` 在 `HasBoard` 为真时跳过重建，保留外部难度选择；重开测试在 `Restart` 前插入真实帧推进并断言计时先累计后归零，删除恒真雷数断言；`MineColor` 改为深灰与红色错误旗 "X" 区分；数字 7/8 显式配色；测试名改为"二二小棋盘"。
- 按审查建议如实记录、暂不处理：change.md 的版本表述与范围章节已在同轮修订中收敛；左键/右键到 `RevealCell`/`ToggleFlagCell` 的真实鼠标接线仍属人工验收项，记录于 `evidence.md` 缺口。
- 修复后复验：`dotnet test` 43/43、Unity EditMode 4/4、PlayMode 6/6、编译 0 警告 0 错误，详见 `evidence.md`。
