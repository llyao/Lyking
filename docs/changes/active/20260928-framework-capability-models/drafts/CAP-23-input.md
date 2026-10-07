# CAP-23 输入系统 · 设计草稿

> 状态：**待审核** | 能力域：F 客户端核心模块 | 目标工程：合同进 `GameFramework.Contracts`，实现进 `GameFramework.Adapters.Client.*` | 前置：CAP-02、CAP-07、CAP-17
> 审核方式：逐条回答"审核决策点"；批准后本行改为"已审核（2026-XX-XX）"。

## 1. 解决什么问题

设备输入（键鼠/手柄/触屏）如何变成模拟命令。没有这层：输入代码直接戳状态（回放失效）、设备差异散落业务、作弊边界模糊。

**MMO 驱动力**：
- 输入 = 建议，权威 = 真相：客户端输入只产生命令提交（CAP-08 模型），预测手感由 CAP-08/17 兜底——输入层必须为此让路（可丢弃、可乱序到达服务器）。
- `InputSequence`：来源内单调序号（CAP-02），服务器据此做乱序检测与预测对账（CAP-08 `IPredictionTracker`）。
- 反外挂基础：服务器看到的输入流是唯一事实，输入层的录制/校验让"人还是脚本"可分析。

**通用性**：动作/回合/放置游戏的输入节奏差异巨大，但"设备事件 → 逻辑动作 → 命令"三层模型通用；回合制只是动作频率低。

## 2. 术语与核心对象

| 对象 | 含义 |
|---|---|
| 设备层 | 原始输入事件（按键/指针/手柄轴），平台 adapter 供给 |
| `InputAction` | 逻辑动作（"Move"、"Skill1"），与设备解耦（可重绑定） |
| `IInputActionMap` | 动作映射表：设备事件 → 动作；上下文切换（战斗/聊天中） |
| `ActionToCommandBridge` | 动作 → `CommandEnvelope` 提交（带 InputSequence） |
| 输入采样节奏 | 表现帧采样（CAP-21 帧泵），命令提交频率可配 |

## 3. 合同形状

```csharp
public readonly record struct InputActionId(string Value);   // "move"、"skill-1"、"confirm"

/// <summary>设备无关的动作状态（表现帧采样结果）。</summary>
public readonly record struct InputActionState(
    InputActionId Action,
    bool Started,           // 本帧开始按下
    bool Held,              // 持续按住
    bool Ended,             // 本帧释放
    Vector2Like Axis);      // 轴类动作（移动方向、镜头）；Vector2Like 为合同内最小结构

public interface IInputActionMap
{
    string Name { get; }                                  // "world"、"battle"、"chat"
    /// <summary>本帧采样：从设备层读出全部相关动作状态（实现按平台）。</summary>
    void Sample(InputActionBuffer buffer);
}

/// <summary>映射注册与上下文切换（栈式：聊天打开时压入 "chat" 屏蔽 world 动作）。</summary>
public interface IInputContextStack
{
    void Push(string mapName);
    void Pop(string mapName);
    ReadOnlySpan<InputActionState> SampleCurrent();
}

/// <summary>动作 → 命令桥：业务组装时注册（动作语义属玩法，框架不内置动作表）。</summary>
public interface IInputCommandBridge
{
    /// <summary>表现帧回调：把动作翻译为命令提交。inputSequence 单调递增。</summary>
    /// <remarks>高频动作（移动）按节流配置合并；桥内禁止直接改任何状态。</remarks>
    ValueTask DispatchAsync(ReadOnlySpan<InputActionState> actions, CancellationToken ct);
    long InputSequence { get; }        // CAP-02 CommandMetadata.InputSequence 的来源
}

public sealed record InputThrottle(int MaxCommandsPerFrame = 4, int MergeWindowFrames = 1);
```

**数据流**：

```text
设备事件（平台 adapter：键鼠/触屏/手柄）
  → IInputActionMap.Sample（设备→动作，含重绑定表）
  → IInputContextStack（上下文屏蔽：UI 打开/聊天中）
  → IInputCommandBridge.DispatchAsync（动作→CommandEnvelope + InputSequence 递增）
  → ICommandSubmitter.Submit（CAP-17 降权通道）→ 管线（CAP-07）
```

## 4. 关键设计决策（含推荐）

**D1｜三层模型：设备/动作/命令（推荐）**：设备差异（触屏没有"按住"、手柄有死区）锁在设备层；动作语义（"跳"）锁在映射层；命令生成属玩法桥。重绑定 = 换映射表，业务零改动。

**D2｜桥是动作到命令的唯一通道（推荐）**：禁止"输入系统直接 Submit 裸命令"的旁路——所有命令都经桥（统一带 `InputSequence`、统一节流合并、统一可录制）。回放因此天然覆盖输入。

**D3｜节流合并为合同参数（推荐）**：高频动作（移动朝向）按 `MergeWindowFrames` 合并（窗口内取最新），`MaxCommandsPerFrame` 封顶——从源头控制命令洪峰（服务器限速 CAP-12 是第二道，客户端自律是第一道）。回合制游戏把两者调大即可，同一合同。

**D4｜UI 输入截断（推荐，衔接 CAP-22 D5）**：`IInputContextStack` 压栈即屏蔽——聊天窗口打开时 world 动作不采样。UI 命中的指针事件同样不进动作层。截断语义在栈，不在各窗口自查。

**D5｜不做输入预测与组合技引擎（推荐）**：预测在 CAP-08/17；连招/搓招是玩法层对动作序列的解释（桥内业务代码）。框架只保证动作流的稳定供给。

## 5. 不变量

1. 输入层只产生命令提交，零状态写入（与 CAP-17 D3 同源纪律）。
2. `InputSequence` 单调递增、按来源（Subject）独立；桥保证，不依赖业务自觉。
3. 动作与设备解耦：同一动作表在键鼠/手柄/触屏下语义一致（映射层责任）。
4. 输入采样在表现帧（CAP-21 帧泵），不在模拟 tick 内——模拟只消费命令队列。

## 6. 失败语义

| 场景 | 行为 |
|---|---|
| 映射表引用不存在动作 | 启动/换表时校验失败（映射完整性） |
| 桥提交被权威拒绝 | CAP-08 拒绝路径（表现层撤销预测）；输入层不重试——下次输入自会带新序号 |
| 设备层异常（手柄断连） | 映射层产出全 Release 事件（防按键卡死），设备标记离线 |
| 节流丢帧 | 合并语义内属正常（取最新），不报错；诊断计数 |

## 7. 使用方案

- **U1**：注入假设备事件流，断言动作→命令序列与 `InputSequence`（输入层全 headless 可测）。
- **U2 单机**：直接提交；本地即时反馈。
- **U3 联机权威**：节流合并 + 预测表现（CAP-08）；断线期间输入静默丢弃（服务器收不到，本地不假装成功）。
- **U4**：同 U3。

## 8. 与其他能力的关系

- 依赖：CAP-02（InputSequence）、CAP-07/17（提交通道）、CAP-21（采样节奏）。
- 下游：CAP-22（截断语义）、CAP-24（镜头控制类动作驱动表现）、CAP-08（预测对账的输入材料）。

## 9. 非目标

- 不做具体绑定 UI 与存档（重绑定设置界面属产品；绑定表持久化走普通存档）。
- 不做反作弊检测（输入模式分析属运营系统）。
- 不做语音/文字输入法管理（平台层）。

## 10. 验收证据

- 假设备驱动全链路测试：设备→动作→命令，含上下文屏蔽、节流合并、序号单调。
- 重绑定测试：换表后动作语义不变。
- 截断测试：UI 压栈后游戏动作静默。
- 性能：采样+桥每帧分配为零（结构体遍历基准）。

## 11. 审核决策点

- **Q1**：D2 所有命令必经桥（无裸提交旁路）——同意？
- **Q2**：D3 节流合并作为合同参数（而非 adapter 私有行为）——同意？
- **Q3**：D4 上下文压栈即屏蔽（UI/聊天打开时游戏动作静默）——同意？
- **Q4**：D5 连招/搓招归玩法桥、框架不做组合技引擎——确认？
