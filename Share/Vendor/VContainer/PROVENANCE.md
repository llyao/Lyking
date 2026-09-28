# VContainer 来源记录

| 项 | 值 |
|---|---|
| 上游仓库 | https://github.com/hadashiA/VContainer |
| 引入提交 | `master` `4fc1ed47dff1314e73fbf2a85ab88f53601414f6` |
| 引入日期 | 2026-09-26 |
| 许可证 | MIT（见同目录 `LICENSE`，版权归 hadashiA 所有） |
| 引入范围 | `VContainer/Assets/VContainer/Runtime/**/*.cs`，排除 `Unity/` 子目录 |
| 本地修改 | 无逻辑修改；仅按上述范围裁剪文件，未改动任何 `.cs` 内容 |

## 引入原因

NuGet 上的 `VContainer` 包最高版本 1.0.2 是 Godot 平台构建，在脱离 Godot 的进程中构建容器时崩溃，不可使用。为使 headless 测试、未来服务端与 Unity 客户端共享同一核心 API，按源码引入官方 Standalone 编译范围。

## 使用边界

- Unity 侧后续接入时使用官方 UPM 包，不使用本目录；本目录只服务纯 .NET 编译。
- 物化器（`GameFramework.PluginHost.VContainer`）只允许使用最小核心 API 子集：`ContainerBuilder`、`Lifetime`、`Register`、`RegisterInstance`、`As`、`Build`、`IObjectResolver.Resolve`、`CreateScope`、`Dispose`；不使用 `Unity/` 目录下的入口点类型。
- 升级方式：锁定新提交后重新裁剪复制，更新本表并重跑全部测试；禁止直接手改本目录代码。

## 排除内容

- `Runtime/Unity/**`：Unity 专用宿主（LifetimeScope、入口点等），不属于核心物化 API。
- `.asmdef`、`.meta`：Unity 工程元数据，纯 .NET 编译不需要。
- 源生成器（`VContainer.SourceGenerator`）：可选优化，反射注入只在实例创建时发生，满足当前验收边界。
