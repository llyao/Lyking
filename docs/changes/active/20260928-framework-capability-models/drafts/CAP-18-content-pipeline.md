# CAP-18 内容管线 · 设计草稿

> 状态：**待审核** | 能力域：D 平台适配（工具链侧） | 目标工程：`Tool/` 生成器 + `GameFramework.Adapters.Content` | 前置：CAP-03、CAP-09
> 审核方式：逐条回答"审核决策点"；批准后本行改为"已审核（2026-XX-XX）"。

## 1. 解决什么问题

策划内容（表、配置、关卡、协议描述）从编辑到运行时的路径要**可验证、可回滚**。手工拷文件的世界里：客户端和服务端内容版本对不上（同一段逻辑两边表不同）、发布一半失败留下半套数据、回滚靠备份文件碰运气。

**MMO 驱动力**：
- 双端内容一致是 MMO 生死线：服务端按表结算、客户端按表表现，不一致即漏洞（客户端显示 100 血、服务端判定 80）。
- 不停服更新内容：current projection（运行时指向当前包）+ 原子切换。
- 表系统所有权：Luban（表）与内容编译器（协议/关卡）必须划清各族所有权，禁止双重生成（顶层任务清单既定）。

**通用性**：单机游戏 = 打包期产出内容包（无运行时切换）；竞技/单机在线 = 双端 revision 校验；MMO = 完整发布门。管线按阶段启用，不强制全量。

## 2. 术语与核心对象

| 对象 | 含义 |
|---|---|
| authoring 源 | 人类编辑格式（JSON/表格/编辑器工程），唯一编辑入口 |
| compiler | authoring → 运行时格式的编译器（Tool 侧，含校验规则） |
| pack | 编译产物容器：分块数据 + manifest |
| `ContentManifest` | 包清单：`ContentRevision`、各内容块哈希、schema 版本表、依赖 |
| `ContentRevision` | 包版本：单调递增 + 哈希锚定（不是时间戳） |
| current projection | 运行时读到的"当前包"指针；原子切换的对象 |
| 内容族 | 同一生成器所有权下的内容集合（协议族/表族/关卡族），所有权登记防双重生成 |

## 3. 合同形状

```csharp
/// <summary>编译器输入输出（Tool 侧进程执行；运行时不接触 authoring）。</summary>
public interface IContentCompiler
{
    string Family { get; }                            // 内容族名（所有权登记）
    ValueTask<CompileResult> CompileAsync(CompileInput input, CancellationToken ct);
}

public sealed record CompileInput(string SourceRoot, IReadOnlyDictionary<string, string> Options);
public sealed record CompileResult(
    ContentManifest Manifest,                         // 编译即出 manifest（含全部块哈希）
    IReadOnlyList<ContentChunk> Chunks,
    IReadOnlyList<CompileDiagnostic> Diagnostics);    // 警告不阻断；error 阻断发布

/// <summary>运行时内容面：只读，指向当前包。</summary>
public interface IContentProvider
{
    ContentManifest Current { get; }                  // 原子替换的引用读取
    /// <summary>按块读取（配合 CAP-14 资源租约挂 scope）。</summary>
    ValueTask<ReadOnlyMemory<byte>> ReadChunkAsync(string chunkId, AssetScope scope, CancellationToken ct);
    /// <summary>切换到新包：校验通过才替换 Current；失败保留旧包。</summary>
    ValueTask<ContentSwitchResult> SwitchAsync(ContentManifest candidate, CancellationToken ct);
}

public sealed record ContentSwitchResult(bool Success, string? Error, ContentRevision? RolledBackTo);
```

**发布流程（固化）**：

```text
authoring 编辑 → compiler 编译 + 校验（error 阻断）
  → 出包（manifest + chunks，全部带哈希）
  → 发布门：schema 版本兼容校验（CAP-03）、双端 revision 预检、灰度目标标记
  → 原子切换 current projection（单指针替换，服务端热切换即此）
  → 失败：保留旧 current，报告首个失败块 → 回滚 = 切回旧 manifest（数据未删）
```

## 4. 关键设计决策（含推荐）

**D1｜manifest 先行、哈希锚定（推荐）**：一切校验围绕 manifest：块哈希防传输损坏，`ContentRevision` 做双端一致判据（快照元数据引用它，CAP-02/09 已接线）。revision 用"计数 + 顶层哈希"双字段：计数可比新旧，哈希防碰撞歧义。

**D2｜authoring 与运行时格式彻底分离（推荐）**：运行时只读编译产物（紧凑二进制，CAP-03 编码）；JSON/表格仅存在于 Tool 侧。禁止运行时带 JSON 解析器"灵活读取"——体积、性能、校验三输。

**D3｜内容族所有权登记（推荐，顶层既定）**：`IContentCompiler.Family` 在 Tool 启动时登记（`Tool/Configuration`），同族两个编译器 = 启动失败。表族归 Luban、协议族归协议生成器、关卡族归关卡编译器——写进登记配置，冲突即刻暴露。

**D4｜校验规则内置于编译器（推荐）**：数值范围、引用完整性（表 A 的 id 必须存在于表 B）、本地化键存在性——编译期 error 阻断发布。运行时不重复校验（信任包）。规则本身是代码不是配置表（配置表校验配置表 = 循环依赖）。

**D5｜发布门与灰度（推荐最小版）**：第一版发布门 = 编译零 error + schema 兼容 + manifest 完整性；灰度（按 World/按玩家百分比切换 current）只留 `SwitchAsync` 的 candidate 通道设计，不实现灰度编排（运营系统范畴，接口已可承载）。

**D6｜回滚 = 切指针（推荐）**：旧包数据保留在存储中，回滚不发新包、不重新编译——切回旧 manifest。配合 CAP-09：运行中的快照带 `ContentRevision`，恢复旧包世界时校验 revision 匹配（不匹配 → 先迁移内容或拒绝恢复）。

## 5. 不变量

1. `ContentRevision` 全链路唯一真相：快照、日志、客户端版本协商都引用它；禁止另造版本字段。
2. current projection 切换原子：任何时刻 `Current` 都是一个完整可用包（要么旧要么新）。
3. 失败不发布：任何发布门失败，current 不动。
4. 运行时无 authoring 格式解析器；编译产物必须经 CAP-03 编解码。
5. 同一内容族单一编译器（所有权登记）；生成物禁止手改（顶层 6 规则 5）。

## 6. 失败语义

| 场景 | 行为 |
|---|---|
| 编译 error | 不出包；诊断带源文件定位 |
| 发布门 schema 不兼容 | 拒绝切换 + 指出类型与版本差异 |
| 切换后块校验失败 | 自动回滚旧包 + 报告（`RolledBackTo` 填旧 revision） |
| 快照恢复时 revision 不匹配 | 按 CAP-09/CAP-03 拒绝或要求内容迁移 |
| 客户端 revision ≠ 服务端 | 连接握手拒绝（CAP-16 握手）并提示更新 |

## 7. 使用方案

- **U1 Headless 测试**：编译期快照（小包内嵌）；样例测试读取内容块走 `IContentProvider`，不读源文件。
- **U2 单机**：打包期编译进资源包；无运行时切换。
- **U3 联机权威**：双端 revision 一致性门（握手）；服务端热切换内容（调表）。
- **U4 在线多 World**：按 World 灰度切换（candidate 通道）；RuntimeSlice 经 CAP-14 World scope 投递。

## 8. 与其他能力的关系

- 依赖：CAP-03（编码/兼容）、CAP-09（快照引用 revision）、CAP-14（块加载挂 scope）。
- 下游：CAP-16（握手 revision 校验）、CAP-20（内容包热更的安全点与回滚）、CAP-13（灰度与 World 编排）。

## 9. 非目标

- 不做编辑器 UI（authoring 用现成表格/JSON/后续专属编辑器）。
- 不做 CDN 与下载器实现（客户端更新器属部署层；合同提供 manifest 校验材料）。
- 不做实时热重载表数据到运行对象（切换 = 整包原子替换，块级热替换后续按需）。

## 10. 验收证据

- 编译-发布-读取闭环测试；引用完整性校验命中用例（坏 id 阻断）。
- 双端 revision 校验、失败不发布、原子回滚三组测试（顶层任务清单同款）。
- 内容族所有权冲突启动失败测试。
- 切换中途故障注入：current 恒完整。

## 11. 审核决策点

- **Q1**：D2 运行时彻底不带 authoring 解析器（JSON 只在 Tool 侧）——同意？
- **Q2**：D3 内容族所有权登记机制（表= Luban、协议= 协议生成器）——同意这个划分与登记做法？
- **Q3**：D5 第一版发布门最小集 + 灰度只留通道不实现编排——同意？
- **Q4**：D6 回滚 = 切回旧 manifest（保留旧包数据）——同意存储侧保留策略（保留几包可配）？
