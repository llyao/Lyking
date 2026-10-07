# Unity 21 点（Blackjack）

Unity 2022.3 LTS 的单玩家 21 点，复用本仓库既有分层：核心规则在 `Assets/Scripts/Core/` 中保持纯 C#（无 Unity 依赖、随机性全部来自注入种子的 Fisher-Yates 洗牌、完全确定），Unity 层只负责键盘采集、下注按钮与 IMGUI 呈现。打开 `Assets/Scenes/BlackjackDemo.unity` 进入 Play 模式即可游玩：下注阶段点击底部按钮下注发牌（50 / 100 / 200 / 全押），玩家回合内 `H` 要牌、`S` 停牌、`D` 加倍（翻倍赌注且只再补一张）、`R` 以新种子重开一局并保留筹码。庄家不足 17 点必须要牌、到 17 点及以上停牌；玩家天牌（首两张 21）按 3:2 赔付、庄家天牌玩家输注、爆牌即输、比点相同退注。

## 结构

- `Assets/Scripts/Core/`：纯规则（牌与点数估值、规则常量、注入种子的发牌堆、事件日志、对局状态机），`noEngineReferences` 程序集。
- `Assets/Scripts/Unity/`：`BlackjackDemoBootstrap`（装配、按键映射、公开测试入口）、`BlackjackDemoPanel`（IMGUI 手牌、状态、下注按钮与事件日志）。
- `Assets/Tests/EditMode/`、`Assets/Tests/PlayMode/`：Unity 测试工程。
- `Core/`、`Tests/`：dotnet 侧纯逻辑工程与 xunit 测试（与 Unity asmdef 并存，源码引用同一批文件）。

## 玩法要素

- 规则常量集中在 `BlackjackRules`：初始筹码 1000、最小注 50、庄家 17 点停牌、天牌赔 3:2、单副 52 张、牌堆不足 15 张时下一局发牌前整副重洗。
- `HandEvaluator` 点数估值为纯函数：J/Q/K 记 10，A 先按 11 计、总和超 21 时逐张降为 1（如 A+9+9=19、A+A+9=21）；天牌仅指首两张 21。
- 对局状态机 `BlackjackGame`：下注（`Deal`，注额不足最小注或超出筹码被拒绝）→ 玩家回合（`Hit`/`Double`/`Stand`，加倍仅限首两张）→ 庄家回合（补牌到 17 点及以上）→ 结算（天牌 3:2、爆牌、比点、平局退注）。筹码下注时扣减、结算时返还，跨局与重开保留。
- 牌面展示用字母表示花色避开字体缺字：**S=黑桃、H=红心、D=方块、C=梅花**；庄家暗牌在玩家回合绘制为背面色块，停牌或结算后揭开（明牌阶段庄家点数只按第一张计）。
- 底部滚动显示最近对局事件（下注、发牌、要牌、加倍、爆牌、庄家补牌、结算带金额、新一局、重洗），核心事件日志按 128 条封顶裁剪。

## 纯逻辑验证

```powershell
dotnet build Client/Blackjack/Lyking.Blackjack.slnx
dotnet test Client/Blackjack/Tests/Lyking.Blackjack.Core.Tests.csproj
```

测试覆盖 52 张牌生成与同种子洗牌确定性、发牌顺序（玩家-庄家-玩家-庄家）、点数软硬转换与天牌判定、爆牌判负、庄家补牌规则、庄家爆牌、天牌 3:2 与庄家天牌、比点与平局退注、下注拒绝（低于最小注/超出筹码）、加倍（翻倍赌注、只补一张、强制停牌）、下一局复位与筹码保留、牌堆不足 15 张自动重洗、事件日志封顶 128、同种子对局完全可复现。Unity EditMode/PlayMode 测试需在 Unity 2022.3 LTS 编辑器中运行验证；界面与交互保存真实运行截图。庄家 AI 策略分级、多副牌、保险与分牌（Split）、网络对战不在第一版范围内。
