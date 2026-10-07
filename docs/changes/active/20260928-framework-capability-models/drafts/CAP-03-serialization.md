# CAP-03 序列化与 schema 演进 · 设计草稿

> 状态：**待审核** | 能力域：A 契约与身份 | 目标工程：`Share/GameFramework.Contracts` | 前置：CAP-01、CAP-02
> 审核方式：逐条回答"审核决策点"；批准后本行改为"已审核（2026-XX-XX）"。

## 1. 解决什么问题

存档、快照、网络包都要跨版本可读；协议、表、代码三类生成物要在客户端、服务端、工具之间保持一致。MMO 的现实约束：

- 服务器不停服更新，老客户端与新服务端共存——协议必须协商与兼容。
- 存档跨月甚至跨年——状态 schema 必须能迁移。
- 生成物漂移是最常见事故：客户端重新生成协议而服务端没跟上，双方"各自正确"地对不上。

**通用性**：单机游戏没有混服问题，但仍要存档迁移；框架把"演进规则"做成能力而不是各项目自觉。

## 2. 术语与核心对象

| 对象 | 含义 |
|---|---|
| `TypeId` | 消息/状态类型的稳定逻辑名（不是 CLR 类型名） |
| `SchemaVersion` | 类型 schema 的主版本号；同主版本内演进受兼容规则约束 |
| `ICodec<T>` | 类型的编解码器：读写字节 + 声明版本范围 |
| `ICodecRegistry` | TypeId/版本 → 编解码器的注册表；由模块经 CAP-00 Contribute 贡献 |
| `IStateMigrator` | 存档/快照迁移：旧版本状态 → 新版本状态 |
| Golden 基线 | 锁定字节序列的基线测试集，防序列化格式意外漂移 |
| 内容族 | 一组由同一生成器产生的内容（协议族、表族）；同族禁止双重生成 |

## 3. 合同形状

```csharp
/// <summary>稳定类型逻辑名。命名空间式字符串，全局唯一，注册时查重。</summary>
public sealed record TypeId(string Namespace, string Name)
{
    public override string ToString() => $"{Namespace}.{Name}";
}

public interface ICodec<T>
{
    TypeId Id { get; }
    int MinSchemaVersion { get; }   // 本编解码器可读的最低版本
    int CurrentSchemaVersion { get; }
    void Write(IBufferWriter<byte> writer, in T value);
    T Read(ref ReadOnlySequence<byte> reader);   // 越界/坏数据抛 CodecException
}

public interface ICodecRegistry
{
    void Register<T>(ICodec<T> codec);          // 冲突即抛：同 TypeId 同版本重复
    ICodec<T> Resolve<T>(TypeId id, int schemaVersion);
    bool TryResolve(TypeId id, int schemaVersion, out ICodec codec);
}

/// <summary>状态迁移：按版本链执行。链不完整时迁移失败，不猜测。</summary>
public interface IStateMigrator
{
    int CurrentVersion { get; }
    /// <summary>把 from 版本的状态迁移到 CurrentVersion；from > Current 报错。</summary>
    MigrationResult Migrate(MigrationInput input);
}

public sealed record MigrationResult(int FromVersion, int ToVersion, bool ChangedAny);
```

## 4. 关键设计决策（含推荐）

**D1｜TypeId：字符串命名空间式（推荐）vs 整数注册表**
- 整数紧凑、传输省字节，但分配表成为新的协调负担（两个模块撞了 int 才发现）。
- 推荐：核心用字符串 `TypeId`（`"lyking.combat"/"skill-cast"`），注册时查重；**传输格式**是否压缩成 int 由 CAP-16 的连接握手协商映射，核心不管。
- 字符串在日志、Golden 基线、跨工具链（表编辑器引用协议类型）中可读，这点对 MMO 工具链价值高。

**D2｜线格式：自带二进制格式（推荐）vs JSON vs 直接用 MessagePack/Protobuf 库**
- 直接用库：省事但把库焊进核心合同（违反 G1），且浮点/字典排序行为随库版本漂移。
- JSON：人类可读适合 authoring 与调试，但体积与速度不适合热路径。
- 推荐：核心定义 `IBufferWriter` 抽象上的**自有最小二进制格式**（变长整数、zigzag、固定排序列表），写法参考 Protobuf wire format 但不承诺互操作；Protobuf/MessagePack 只出现在 CAP-16 的具体 transport adapter 里做透传。authoring/编辑器文件用 JSON，属 CAP-18 编译期。

**D3｜兼容规则（推荐集合）**：
- 同主版本内：允许**追加可选字段**（旧读端跳过未知字段，新读端给默认值）；允许追加新 TypeId。
- 禁止：改字段类型、改字段语义、删除字段（删除必须先停用一个主版本）、改列表排序规则。
- 跨主版本：必须显式 `IStateMigrator`；消息类跨主版本由连接握手协商（CAP-16），协商失败拒绝连接并给出双方版本诊断。

**D4｜生成物策略（推荐）**：核心只手写合同类型与少量编解码器；协议族、表族的编解码器由 Tool 生成（CAP-18），同族禁止双重生成（顶层任务清单既定）。`TypeId.Namespace` 保留字 `lyking.core` 只允许核心合同使用。

**D5｜浮点与哈希关联（推荐）**：二进制格式支持 float/double，但**进快照哈希的状态**（CAP-09）要求模块声明浮点字段的确定性等级（IEEE 确定性仅在同 CPU 指令集内成立）；跨异构宿主的 MMO 模拟状态用定点/整数。这是声明义务，不是框架强制。

## 5. 不变量

1. 所有进传输/存档的类型必须有稳定 `TypeId` + `SchemaVersion`；匿名类型不允许出现在合同边界。
2. 编解码器注册冲突（同 TypeId 同版本）在 Contribute 后的 Validate 阶段失败——启动即报，不允许运行期覆盖。
3. 读端遇到未知字段：同主版本内跳过（写入诊断计数器）；跨主版本拒绝。
4. 迁移链必须从任意受支持历史版本连续可达当前版本；断链在启动校验时暴露（对存档做扫描测试）。
5. 禁止手改生成代码（顶层 6 规则 5）；生成物带 header 注释与输入哈希。

## 6. 失败语义

| 场景 | 行为 |
|---|---|
| 解码越界/坏数据 | `CodecException` 带字节偏移与 TypeId 诊断；传输层据此断开连接（不静默截断） |
| 版本不兼容 | 消息：握手/提交边界拒绝；状态：要求迁移，无迁移链则失败并列出缺失区间 |
| 注册冲突 | Validate 阶段启动失败（复用 CAP-00 失败语义：收集全部诊断一次性报告） |
| 存档迁移中途失败 | 原档不动（迁移先写临时文件/内存副本），报告首个失败版本 |

## 7. 使用方案

- **U1 Headless 测试**：Golden 基线测试——每条核心消息类型锁定一段字节；版本兼容矩阵扫描测试（v1↔v2 互读）。
- **U2 单机**：存档头记录 `SchemaVersion`，读旧档自动走 `IStateMigrator`；迁移结果写日志。
- **U3 联机权威**：连接握手交换协议版本表；不兼容即拒绝并提示最低客户端版本。混服（旧客户端）由"只追加演进"支撑。
- **U4 在线多 World**：内容包 `ContentRevision` 与 schema 版本共同构成发布一致性门（CAP-18）：客户端 revision ≠ 服务端即踢出重下。

## 8. 与其他能力的关系

- 依赖：CAP-00（注册与 Validate 时机）、CAP-01/02（被编码的类型）。
- 下游：CAP-06/09（状态可序列化）、CAP-12/16（握手与传输）、CAP-15（存档迁移）、CAP-18（生成器输出编解码器）。

## 9. 非目标

- 不做 ORM/文档数据库映射（CAP-15 adapter 职责）。
- 不定义 UI/编辑器资源格式（CAP-18 authoring 层）。
- 不承诺与 Protobuf/JSON 的互操作（各自出现在 adapter/工具边界）。

## 10. 验收证据

- 追加兼容测试：v1 数据被 v2 读端读取，未知字段跳过且计数。
- 旧版本拒绝测试：跨主版本消息被拒并带版本诊断。
- Golden 字节测试：核心消息字节锁定，任何格式改动导致测试失败（有意的格式演进需重新生成基线并记录）。
- 迁移链测试：构造 v1→v3 断链存档，启动校验报缺失区间。

## 11. 审核决策点

- **Q1**：D1 TypeId 用字符串命名空间式（传输层再协商压缩）——同意？
- **Q2**：D2 核心自带最小二进制格式、不承诺外部互操作——同意，还是倾向核心直接绑定某具体库（放弃 G1 中立性换取省工）？
- **Q3**：D3 兼容规则集（追加允许、删除走主版本、跨主版本必迁移）——同意？
- **Q4**：D5 浮点声明义务——MMO 模拟状态要求定点/整数，是否接受把这条写进 Profile 兼容矩阵作为硬规则？
