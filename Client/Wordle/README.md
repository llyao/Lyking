# Unity 猜单词（Wordle）

Unity 2022.3 LTS 的猜单词小游戏，复用本仓库既有分层：核心规则在 `Assets/Scripts/Core/` 中保持纯 C#（无 Unity 依赖、注入种子选词、完全确定），Unity 层只负责键盘采集与 IMGUI 呈现。打开 `Assets/Scenes/WordleDemo.unity` 进入 Play 模式即可游玩：六次机会猜一个 5 字母英文小写词，a-z 逐个输入、退格删除、回车提交；绿格为位置正确、黄格为字母存在但位置不对、灰格为字母不存在；第六次仍未猜中判负并公布答案；终局后按 `R` 开新局，连胜与局数统计跨局保留。

## 结构

- `Assets/Scripts/Core/`：纯规则（词库与常量、两趟字母判定、事件日志、对局状态机），`noEngineReferences` 程序集。
- `Assets/Scripts/Unity/`：`WordleDemoBootstrap`（装配、按键采集、测试入口）、`WordleDemoPanel`（IMGUI 字母格、键盘染色、统计、终局提示与事件日志）。
- `Assets/Tests/EditMode/`、`Assets/Tests/PlayMode/`：Unity 测试工程。
- `Core/`、`Tests/`：dotnet 侧纯逻辑工程与 xunit 测试（与 Unity asmdef 并存，源码引用同一批文件）。

## 玩法要素

- 内置词库为 79 个互不重复的常见 5 字母小写词，答案词库与可输入词库共用同一份，判定规则写死一致；答案由注入种子从词库确定性选出。
- 字母判定用两趟算法：第一趟按位置标绿并扣减答案字母配额，第二趟按剩余配额标黄、配额用尽标灰，正确处理重复字母（先绿后黄贪心匹配）。
- 离散回合制无时间累加器：不足 5 字母或不在词库的提交被直接拒绝且不消耗尝试；猜中转胜连胜加一，第 6 次未中转负连胜清零。
- 顶部统计局数/连胜/状态；屏下 QWERTY 三行提示键盘按历史判定聚合染色（绿>黄>灰）且可点击输入；底部滚动事件日志（提交带判定、拒绝非词、胜负带答案、新局、重置），封顶 128 条裁剪。

## 纯逻辑验证

```powershell
dotnet build Client/Wordle/Lyking.Wordle.slnx
dotnet test Client/Wordle/Tests/Lyking.Wordle.Core.Tests.csproj
```

测试覆盖词库规模与格式、固定种子选词可复现、输入/退格边界、提交拒绝、两趟判定（全绿、黄灰混合、重复字母配额封顶）、六次判负公布答案、猜中获胜与剩余行数无关、连胜跨局累计、重开清盘保留统计、日志封顶与同种子确定性。Unity EditMode/PlayMode 测试需在 Unity 2022.3 LTS 编辑器中运行验证；界面与交互保存真实运行截图。音效、在线词库扩展、每日挑战与排行榜不在第一版范围内。
