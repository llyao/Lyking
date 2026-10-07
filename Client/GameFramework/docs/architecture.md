# 架构与分层

本文说明客户端框架的分层规则、失败语义、可测性设计与扩展点。使用方法见 [模块开发指南](module-guide.md)。

## 1. 分层与依赖方向

```text
┌─────────────────────────────────────────────────────┐
│  Unity 适配层   Unity/                              │  仅 Unity 编译
│  GameEntry · UnityLogSink · ResourcesAssetProvider  │
├─────────────────────────────────────────────────────┤
│  界面层         UI/                                 │
│  窗口栈/焦点/模态 · UIFormLogic · 绑定 · Presenter   │
├─────────────────────────────────────────────────────┤
│  基础服务层     Services/                           │
│  资源 · 网络 · 商业服务 · 存档/更新 · 输入 · 本地化… │
├─────────────────────────────────────────────────────┤
│  核心层         Core/                               │
│  模块宿主 · GameClient · FSM/流程 · 事件 · 池 · 日志 │
└─────────────────────────────────────────────────────┘
        依赖只允许自上而下；同层之间通过 Core 的服务注册协作
```

- **Core / Services / UI** 均为 `netstandard2.1` + C# 9，不引用 UnityEngine 与第三方容器；每个工程带同名 `.asmdef`（`noEngineReferences: true`），Unity 与 dotnet 编译同一批源码。
- **Unity/** 引用 UnityEngine 与上述三个 asmdef，dotnet 构建范围排除（与 `Share/Vendor/VContainer` 的 `Unity/` 排除模式一致）。
- **Tests / Demo** 为 net10.0，只引用纯逻辑工程，因此可以在无 Unity 环境完整验证。

命名约定：程序集与命名空间 `Lyking.GameFramework.Client.*`；测试命名 `中文行为描述`，一个用例只验证一个行为。

## 2. 生命周期与失败语义

### 2.1 模块生命周期

```text
Add → Registered → InitializeAsync(拓扑序) → Start(同序) → Update/FixedUpdate/LateUpdate(每帧同序) → ShutdownAsync(严格逆序)
```

排序规则：依赖关系优先于优先级；就绪集合按（Priority 升序，注册顺序）稳定选取，因此输出顺序完全确定。循环依赖、未知依赖、重名模块在启动阶段立即抛出，并给出完整诊断信息。启动取消后使用独立且有界的清理令牌回滚已初始化模块，避免已取消令牌跳过清理。

### 2.2 失败语义矩阵

| 阶段 | 行为 | 上抛类型 |
|---|---|---|
| 初始化失败 | 逆序回滚已初始化模块（回滚异常记录不阻断） | `ModuleStartupException`（含失败模块名） |
| Start 失败 | 包装后直接上抛 | `ModuleStartupException` |
| 帧回调失败 | 单模块隔离：记录日志 + `ModuleHost.FrameFaulted`，后续模块照常更新 | —— |
| 关闭失败 | 先保证全部模块都被尝试关闭，再聚合 | `ModuleShutdownException`（逐模块失败列表） |
| 取消（启动中） | 逆序回滚后原样上抛，不包装 | `OperationCanceledException` |

### 2.3 子系统异常口径

| 子系统 | 隔离机制 | 兜底 |
|---|---|---|
| EventBus | 单处理器异常收集，`HandlerFaulted` 上报 | 无钩子且无日志时聚合抛出（快速失败） |
| ModuleHost 帧泵 | try/catch 每模块 | `FrameFaulted` + 日志 |
| EntityService 每实体更新 | try/catch 每实体 | `EntityFaulted` + 日志 |
| NetworkService 心跳/重连 | 异步任务内捕获 | 日志 + 状态回退 |
| DownloadService 任务 | 逐任务捕获，重试后失败入事件 | `DownloadFailed` |

原则：**有上报通道就隔离，没通道就快速失败**——一帧的局部故障不放大为整帧中断，但静默吞掉也是被禁止的。

## 3. 可测性设计

- 核心三层不引用 UnityEngine，是"编辑模式纯逻辑"在仓库无 Unity 工程阶段的落地形式；`dotnet test` 即可验证全部业务语义。
- 每个平台相关抽象（传输、资源、下载、实体实例、场景、音频、设置、日志）都有模拟实现，语义与真实实现一致，由接口合同约束。
- 时间全部由帧驱动注入（`deltaTime` 累计），测试不依赖真实时钟；异步收敛在测试里用条件等待而非固定延时。
- 可变框架状态归客户端帧线程所有。工作线程通过有界主线程派发器请求执行回调；队列拒绝、客户端关闭和排空超时均有明确结束结果。

## 4. 商业能力与信任边界

`Services/Commercial/` 将账号/游戏平台、隐私同意、支付/广告、推送/深链/归因、远程配置和可观测性拆成可替换合同；`Services/Input/` 把设备样本转换为动作，并在 UI 已消费时截断玩法输入。所有 `Simulation*` 实现仅供测试和无头演示，业务必须显式选择实现，平台缺失不隐式回退到模拟服务。

- **隐私**：分析、广告标识和归因发送前检查同意；撤回后停止采集并清理可清理队列。遥测过滤已知敏感字段、邮箱、常见手机号格式和标识符字段；生产事件仍应按业务字段白名单建模，不能把客户端过滤当作完整匿名化保证。
- **支付**：交易有稳定平台 ID，可重复提交可信验证；业务代码不能直接构造验证结果，供应商验证器从 `ReceiptVerifierBase` 派生；框架只转交收据和验证结果，不授予游戏权益。消耗型交易在可信验证成功前不能确认完成。
- **存档**：`Services/Persistence/` 使用带格式版本的信封、逐版迁移和暂存校验后原子激活。客户端数据可被篡改，不作为联网权威；保护器只提供扩展接口，不自创密钥管理。
- **内容更新**：`Services/ContentUpdate/` 对规范化清单签名验签，固定使用 ECDSA P-256、SHA-256 和 64 字节 IEEE P1363 签名，绑定应用/平台、有效期、单调序号、资源哈希及密钥轮换。下载块验哈希后才能激活；文件存储支持恢复前一快照且保留最高序号。`EcdsaContentManifestTrustStore` 的轮换状态仅驻留进程内，生产适配必须持久保存并在启动时恢复受信密钥状态。设备本地序号仅减轻普通回放，不抵御设备被篡改；服务端仍需执行最低版本策略。

## 5. 扩展点清单（平台实现替换位）

| 扩展点 | 注入位置 | Unity 侧参考实现 |
|---|---|---|
| `ILogSink` | Builder `WithSink` / `GameEntry.Awake` | `UnityLogSink`（已提供） |
| `IAssetProvider` | `ConfigureServices` | `ResourcesAssetProvider`（已提供，可扩展 Addressables） |
| `ITransport` | `new NetworkService(transport)` | TCP/KCP/WebSocket 封装 |
| `IMessageCodec` | `new NetworkService(transport, codec)` | 对接 `Share/` 协议后实现 |
| `IDownloadAgent` | `new DownloadService(agent)` | `UnityWebRequest` 封装 |
| `IEntityInstanceProvider` | `new EntityService(provider)` | prefab 实例化 + 逻辑组件包装 |
| `IMainThreadDispatcher` | `GameClient.Dispatcher` / 派发器服务注册 | Unity 帧循环调用 `Pump`，后台工作通过 `InvokeAsync` 回主线程 |
| `ISceneService` / `IAudioService` / `ISettingsStore` | `ConfigureServices` | `SceneManager` / `AudioSource` / `PlayerPrefs` 封装 |
| `IPlatformAccountService` / `IStoreService` / `IReceiptVerifier` | `ConfigureServices` | 项目选择账号、游戏平台与可信收据验证 SDK |
| `IConsentService` / `IObservabilityService` | `ConfigureServices` | 项目选择合规同意管理和分析/崩溃/性能出口 |
| `IAtomicSaveStore` / `ISaveDataProtector` | `SaveDataService` 构造注入 | Unity 文件系统/平台安全存储适配 |
| `IContentManifestTrustStore` / `IContentVersionStore` | `ContentUpdateService` 构造注入 | 固定公钥、持久版本索引与平台内容存储 |

## 6. 与 Share 框架的关系

`Share/GameFramework.PluginHost` 是面向宿主插件的模块组合中间件（VContainer 容器、Plan/Validate/Apply 管线），`Share/` 的协议与存档契约属于受控范围。客户端框架第一版**不依赖**它：客户端运行在 Unity 主线程帧循环里，模块模型更轻（同步帧回调 + 异步启停），且 Share 侧当时存在在途变更。两者在未来如需统一，客户端的 `IClientModule` 可以用适配器对接 PluginHost 的 `IGameModule`，本框架的接口边界已为此预留（见 `docs/changes/active/20261003-client-framework-v1`）。

## 7. 目录索引

```text
Client/GameFramework/
├── Lyking.ClientFramework.slnx
├── Core/           核心层（Modules/ Dispatching/ Fsms/ Procedures/ Events/ Pooling/ DataNode/ Scheduling/ ServiceLocation/ StateMachines/ Logging/）
├── Services/       平台服务（Commercial/ Input/ Persistence/ ContentUpdate/ Assets/ Network/ DataTable/ Entity/ Download/ Config/ Localization/ Scene/ Audio/ Settings/）
├── UI/             界面层（Windows/ Binding/ Mvp/）
├── Unity/          Unity 适配层（dotnet 范围排除）
├── Demo/           无头演示（Builder + 流程 + 游戏服务 + 存档/更新 + 模态窗口 + 网络）
├── Tests/          纯逻辑行为测试（Core/ Services/ UI/）
└── docs/           本文档与模块开发指南
```
