# CAP-14 资源租约 · 设计草稿

> 状态：**待审核** | 能力域：D 平台适配 | 目标工程：合同进 `GameFramework.Contracts`，实现进 `GameFramework.Adapters.*` | 前置：CAP-00
> 审核方式：逐条回答"审核决策点"；批准后本行改为"已审核（2026-XX-XX）"。

## 1. 解决什么问题

资源加载后端（YooAsset、Addressables、文件系统、打包内嵌）各不相同；业务代码一旦直接触碰后端句柄（`AssetHandle`、`GameObject`、文件流），泄漏与平台绑定随之而来——最典型的是"忘记 Release"与"作用域销毁了句柄还活着"。

**MMO 驱动力**：
- RuntimeSlice（CAP-18 的运行时内容分片）按会话/World 加载与卸载——没有统一租约，"World 关闭后资源还挂着"在多 World 下是常态事故。
- 热更（CAP-20）：替换内容包前必须确认旧包资源租约全部归还——租约计数就是安全判据。

**通用性**：单机游戏同样受益（关卡卸载即 scope 释放）；租约模型对任何资源形态成立（UI 预制体、音频、配置表、甚至数据库连接池句柄）。

## 2. 术语与核心对象

| 对象 | 含义 |
|---|---|
| `IAssetProvider` | 资源加载门面（能力名 `asset-provider`，CAP-00 唯一提供者机制） |
| `AssetKey` | 资源逻辑键（稳定字符串，不暴露后端路径格式） |
| `AssetScope` | 租约作用域：App / Session / World / Custom；scope 销毁回收其全部租约 |
| `AssetLease<T>` | 租约：持有 `T`，释放必须走 lease 或 scope |
| 假加载器 | U1 用的内存实现（键 → 预置对象表） |

## 3. 合同形状

```csharp
public readonly record struct AssetKey(string Value)
{
    public bool IsValid => !string.IsNullOrWhiteSpace(Value);
}

public sealed class AssetScope : IAsyncDisposable
{
    public string Name { get; }                     // 诊断用（"world:42"、"session:abc"）
    /// <summary>创建子作用域；父释放时级联释放子。</summary>
    public AssetScope CreateChild(string name);
    public ValueTask DisposeAsync();                // 释放本 scope 全部租约（幂等）
    public event Action<LeaseLeakReport>? OnLeak;   // 双重释放/释放后访问的观测点
}

public interface IAssetProvider
{
    /// <summary>加载并挂到 scope。同一 key 同一 scope 内重复加载共享底层句柄（引用计数）。</summary>
    ValueTask<AssetLease<T>> LoadAsync<T>(AssetKey key, AssetScope scope, CancellationToken ct) where T : class;
    /// <summary>预加载提示（后端可自行决定是否真做，合同不承诺）。</summary>
    ValueTask WarmUpAsync(IEnumerable<AssetKey> keys, AssetScope scope, CancellationToken ct);
}

public sealed class AssetLease<T> : IDisposable where T : class
{
    public T Value { get; }                          // 释放后访问抛 ObjectDisposedException
    public AssetScope Scope { get; }
    public void Dispose();                           // 归还一份引用；scope 释放时统一兜底
}
```

**生命周期规则**：

```text
LoadAsync → lease 引用计数 +1（同 key 同 scope 共享底层句柄）
lease.Dispose() → 计数 −1；到 0 → 后端 Release
scope.DisposeAsync() → 该 scope 全部租约强制归还（业务忘记 Dispose 也不泄漏）
父 scope 释放 → 子 scope 级联释放
```

## 4. 关键设计决策（含推荐）

**D1｜租约持有对象不持有句柄（推荐）**：`AssetLease<T>.Value` 是反序列化/实例化后的对象，后端句柄藏在 adapter 内。业务不接触 `AssetHandle` 类型——平台中立由类型系统保证（核心合同无任何后端类型）。

**D2｜scope 是唯一释放兜底（推荐）**：不提供全局"按 key 卸载"——卸载语义必须挂在生命周期结构（scope 树）上，否则必然出现"还在用的资源被卸了"。泄漏兜底与级联都由 scope 完成。

**D3｜引用计数在 adapter 内（推荐）**：合同只定义行为语义（共享、计数、兜底），计数实现归 adapter（不同后端机制不同）。U1 假加载器用自己的字典计数即可，核心不提供通用计数器。

**D4｜加载失败语义：显式失败 + 可重试（推荐）**：返回失败（带 AssetKey、后端原因诊断），不返回 null、不返回占位对象。业务决定重试或降级（默认皮肤）。`WarmUpAsync` 失败不致命（只是预热）。

**D5｜异步加载、同步访问（推荐）**：`LoadAsync` 异步（IO），`lease.Value` 同步（对象已就绪）。禁止在 lease 上再暴露异步方法——使用期不该有 IO。

## 5. 不变量

1. 后端类型（YooAsset/Addressables/文件流）只出现在 adapter 程序集；核心与业务引用不到。
2. scope 释放后其租约全部失效；访问已释放 lease 抛 `ObjectDisposedException`（不静默返回旧对象）。
3. 同 `(key, scope)` 并发加载合并为一次后端加载（adapter 责任）。
4. `AssetScope` 树无环（子只能引用父已存在的 scope）；释放幂等。
5. 泄漏可观测：scope 释放时若有业务仍持 lease，不阻塞释放但触发 `OnLeak` 报告（诊断 + 计数）。

## 6. 失败语义

| 场景 | 行为 |
|---|---|
| key 不存在 / 后端加载失败 | `LoadAsync` 失败带诊断；业务重试或降级 |
| scope 释放时仍有活动 lease | 照常释放 + `OnLeak` 报告（含持lease 者的创建堆栈采样，Dev 构建） |
| 双重 Dispose lease | 幂等（第二次无操作）；Dev 构建额外警告 |
| scope 级联释放中途失败 | 收集全部失败一次性报告（对齐 CAP-00 Stop 语义），不留下半释放 scope |

## 7. 使用方案

- **U1 Headless 测试**：假加载器（字典预置）；断言计数归零、级联释放、泄漏报告。
- **U2 单机**：App scope（常驻 UI）+ 关卡 scope（进关卡开、出关卡关）。
- **U3 联机权威**：服务端一般无表现资源；协议/表数据可经此通道加载（key 即内容键）。
- **U4 在线多 World**：World scope 对应 RuntimeSlice（CAP-18 投递的内容分片）；World 关闭 = scope 释放 = 分片资源全回收。

## 8. 与其他能力的关系

- 依赖：CAP-00（capability 注册与唯一提供者）。
- 下游：CAP-18（RuntimeSlice 经 scope 投递）、CAP-20（热更前租约归零判据）、CAP-17（表现层加载 UI/特效走本合同）。

## 9. 非目标

- 不做资源版本协商与下载（CAP-18 内容管线职责）。
- 不做引用图分析/依赖预计算（后端细节）。
- 不定义场景/对象池语义（Unity adapter 的内部分层）。

## 10. 验收证据

- 假加载器闭环：共享计数、级联释放、泄漏报告、失败重试测试。
- 契约测试：核心程序集类型扫描（无后端类型泄漏进合同——编译门禁）。
- Demo：ModuleDemo 扩展 scope 树创建/释放轨迹。

## 11. 审核决策点

- **Q1**：D2 只经 scope 树释放、不提供按 key 全局卸载——同意？
- **Q2**：D5 释放后访问抛异常（而非静默返回）——同意？
- **Q3**：泄漏不阻塞释放、只报告（D6 语义）——同意，还是要求 Dev 构建直接 fail？
