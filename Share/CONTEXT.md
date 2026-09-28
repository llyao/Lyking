# 共享契约上下文

## 职责

跨端数据结构、协议和序列化契约。

## 当前入口

[目录说明](README.md)。

## 必读资料

[上下文地图](../CONTEXT-MAP.md)、[工作流](../docs/ai/workflow.md)和[验证边界](../docs/ai/verification.md)。按实际影响读取相邻领域。

## 不属于本目录

仅客户端或仅服务端实现。

## 验证入口

同时验证客户端与服务端编译、兼容性和协议测试；本领域默认触发受控目标。

当前可执行项目（2026-09-26 起）：

```powershell
dotnet build Share/GameFramework.slnx
dotnet test Share/GameFramework.PluginHost.Tests/GameFramework.PluginHost.Tests.csproj
dotnet run --project Share/GameFramework.ModuleDemo
```

`Share/Vendor/` 下的第三方源码按 `PROVENANCE.md` 边界使用，禁止直接手改。新增可执行项目时同步补充本节。
