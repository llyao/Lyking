<!-- lyking-ai
{"schemaVersion":1,"id":"20261003-client-commercial-framework-v1","profile":"controlled","status":"active","areas":["Client"],"paths":["docs/changes/active/20261003-client-commercial-framework-v1/","Client/GameFramework/"],"riskSignals":["save-data","network-protocol","security","architecture-choice","runtime-integration"]}
-->

# 商业客户端通用框架

## 解决什么问题

`Client/GameFramework/` 已有模块生命周期、UI 窗口、网络与下载抽象、资源、场景、音频、数据表、本地化和实体池等基础能力，但多数平台服务仍为模拟实现或窄接口。商业项目还需要账号与游戏平台能力、隐私同意、支付和广告、通知/深链/归因、输入、内容更新、存档迁移、远程配置、分析与崩溃诊断等完整模块，并需要一致的线程、取消、失败和隐私语义。

本变更在现有框架上扩展成 Unity 项目可接入、无 Unity 环境可测试的商业客户端能力框架。所有新服务先提供平台中立合同与确定性模拟实现；供应商 SDK、游戏服务器协议与具体项目内容由后续适配层提供。

## 当前状态

`active`。用户已确认采用通用接口与模拟适配，真实供应商 SDK 留作后续适配包。设计中的线程派发/排空、启动取消清理、支付交易幂等、清单信任与防回放、存档迁移/原子替换等推荐决策作为本次实现约束；按 `plan.md` 与 `tasks.md` 实施，验证证据和独立审查完成后再评估是否满足 `verified`。

## 完成后应看到什么

- 现有 `GameClient`、模块宿主、服务注册、UI、实体、下载、网络和音频门面得到扩展；不出现第二套客户端宿主、模块生命周期或 UI 管理器。
- 框架提供平台与账号、成就/排行榜等游戏平台能力、隐私与权限、支付、广告、推送/深链/归因、输入、存档、内容更新、远程配置/功能开关、分析/崩溃/性能诊断等能力合同，并有可确定验证行为的模拟实现。
- 账号凭据、支付收据和个人数据不会由核心模块擅自持久化或写入遥测；支付交易有稳定标识、重复验证幂等，结果不直接授予游戏内权益。
- 存档带格式版本和显式迁移；内容清单具备签名信任链、有效期和防回放规则，块完整性通过后才原子切换；失败保留可运行版本。
- 异步 I/O 可在后台执行，框架生命周期、帧回调、Unity API 与业务事件遵循一个明确的线程派发规则；队列拒绝/关闭有明确完成语义；启动回滚使用独立有界清理令牌。
- .NET 构建、自动化测试与无头 Demo 覆盖正常路径、取消、失败、重试、迁移、回滚、隐私门控和模块关闭；Unity 侧能力缺口如实记录，不用模拟测试冒充真实编辑器验证。

## 验收方式

设计确认后在 `plan.md` 与 `tasks.md` 固化逐步实施范围。预期执行 `route`、`validate`、`dotnet build Client/GameFramework/Lyking.ClientFramework.slnx`、`dotnet test Client/GameFramework/Tests/Lyking.GameFramework.Client.Tests.csproj`、`dotnet run --project Client/GameFramework/Demo/Lyking.GameFramework.Client.Demo.csproj`、`git diff --check`。测试需覆盖存档迁移与损坏、内容清单/块校验和回滚、支付验证边界、隐私同意、后台派发、启动中取消和退出收敛。

Unity 适配层须在实际 Unity 工程中验证主线程调用、应用暂停/恢复、场景/UI 生命周期和设备服务。当前仓库没有此框架的 Unity 工程；不能以 `dotnet` 结果替代 Unity 编辑器验证。

## 范围与非目标

范围：现有 `Client/GameFramework/` 的 Core、Services、UI、Unity、Demo、Tests，以及本变更工件。现有服务采用兼容扩展方式；对外服务适配只依赖合同与注册门面。

非目标：不接入具体移动/桌面商店、广告网络、分析或崩溃平台的真实 SDK；不定义 `Share/` 网络协议、服务端账号/支付校验或游戏玩法数据模型；不实现反作弊、版权保护、云存档供应商、程序集热更新和具体游戏 UI/场景。Unity 供应商适配需项目提供 SDK、包和平台凭据后另行实施。

## 风险与回滚

风险包括：异步延续线程破坏 Unity 主线程约束；本地存档被误当权威数据；伪造支付结果导致权益错误；不可信清单造成错误内容覆盖；遥测绕过同意收集个人信息。分别通过显式线程派发合同、服务端权威边界、支付验证委托、更新失败关闭与原子回滚、同意门控和敏感字段过滤控制。

变更以新增服务和适配为主，现有公共行为由回归测试固定。若新服务设计不适合，按文件回退新增能力并保留已有框架；存档/更新实现不在未通过迁移和回滚测试前替代现有设置存储或资源加载路径。
