<!-- lyking-ai
{}
-->

# 验证证据

环境：Windows / .NET SDK 10.0.201 / 日期 2026-10-03。

## 路由与结构

- 路由：`dotnet run --project Tool/AiWorkflow/Lyking.AiWorkflow.csproj -- route --text "商业游戏客户端通用框架" --path Client/GameFramework/Core/ --path Client/GameFramework/Services/ --path Client/GameFramework/UI/ --path Client/GameFramework/Unity/ --path Client/GameFramework/Demo/ --path Client/GameFramework/Tests/`：退出码 0，主领域 `Client`，相关领域为空；工具建议 `standard`，受控目标因存档、安全和运行时风险继续使用 `controlled`。
- 档案结构：`dotnet run --project Tool/AiWorkflow/Lyking.AiWorkflow.csproj -- validate --change docs/changes/active/20261003-client-commercial-framework-v1`：退出码 0，`issues: []`、`isValid: true`，已在最终文档更新后复跑。

## 构建与测试

- `dotnet test Client/GameFramework/Tests/Lyking.GameFramework.Client.Tests.csproj --filter FullyQualifiedName~ContentUpdateTests --no-restore --nologo`：退出码 0，15 通过、0 失败、0 跳过；覆盖 v1/v2 指针兼容、前一快照恢复、存储重建后的精确重放幂等、并发清单（共享/独立信任库）及冲突拒绝。
- `dotnet test Client/GameFramework/Tests/Lyking.GameFramework.Client.Tests.csproj --filter FullyQualifiedName~ContentManifestSignatureTests --no-restore --nologo`：退出码 0，3 通过、0 失败、0 跳过；覆盖固定 P-256/P1363 签名与对 256 位 secp256k1 根/轮换密钥的拒绝。
- `dotnet test Client/GameFramework/Tests/Lyking.GameFramework.Client.Tests.csproj --filter FullyQualifiedName~DownloadDispatcherTests --no-restore --nologo`：退出码 0，4 通过、0 失败、0 跳过；包括带/不带派发器的同步取消完成路径。
- `dotnet build Client/GameFramework/Lyking.ClientFramework.slnx --nologo`：退出码 0，Core、Services、UI、Tests、Demo 全部构建；0 警告、0 错误。
- `dotnet test Client/GameFramework/Tests/Lyking.GameFramework.Client.Tests.csproj --nologo --no-build`：退出码 0，224 通过、0 失败、0 跳过。
- `dotnet run --project Client/GameFramework/Demo/Lyking.GameFramework.Client.Demo.csproj --no-build`：退出码 0。无头演示完成模拟登录、隐私门控、支付验证委托、配置、输入、通知/深链/归因、遥测、存档迁移、签名内容更新及客户端关闭。
- 关闭异步调用取消后复用派发器队列容量的测试：首次全量运行在第二次 `Pump()` 处失败，根因是测试先 `await` 后可能在非创建线程泵送。将 await 推迟到所有 `Pump()` 之后，定向测试连续 6 次通过；随后全量测试通过 224 项。
- 并发相同清单回归在修复前以 `ContentManifestRejectedException` 失败；修复后内容更新组 15/15 通过。曲线拒绝测试在修复前因未抛出预期异常而失败，修复后签名组 3/3 通过。
- `git diff --check -- Client/GameFramework docs/changes/active/20261003-client-commercial-framework-v1`：退出码 0。`rg -n '[\t ]+$' Client/GameFramework docs/changes/active/20261003-client-commercial-framework-v1`：退出码 1，表示扫描范围内无尾随空白匹配。
- 独立只读复审第二轮确认 3 项 P2 修复，无新阻塞；复审者未运行测试，验证均由主线程执行。
- 下载同步取消代理已证明清理会在取消回调返回前发生；旧实现未在本机 .NET 10 稳定重现字典枚举异常。快照遍历为跨运行时防护；Unity/Mono 未安装，尚无目标运行时验证。
- 路由、档案结构校验与以上 .NET/Demo/空白检查均为本次会话重新执行。最终文档更新后的 validate 退出码 0、`isValid: true`；尾随空白扫描无匹配、范围内 `git diff --check` 退出码 0。

## 平台验证缺口

- 当前变更没有关联的 Unity 工程；`Client/GameFramework/Unity/` 以及真实主线程、暂停恢复、场景和平台服务验证尚未执行。此项不由 .NET 测试替代，接入实际 Unity 工程后补验。
- 当前环境未安装 `Unity.exe` 或 `mono`；不能对 Unity/Mono 的字典枚举器行为和 Unity API 主线程接入执行本机验证。
- `EcdsaContentManifestTrustStore` 将密钥轮换状态保存在进程内存，适用于测试和演示。生产信任存储仍需跨重启持久恢复轮换，并与内容指针激活保持事务一致；尚无生产持久化适配和断电注入验证。
- 当前真实商店、广告、账号、推送与遥测 SDK 不在本次范围内；本次验证证明平台中立合同和模拟流程，不证明厂商集成或线上服务端支付裁决。
