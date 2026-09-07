# 开发工具上下文

## 职责

构建、资源处理、检查和维护工具。

## 当前入口

[目录说明](README.md)。

## 必读资料

[上下文地图](../CONTEXT-MAP.md)、[工作流](../docs/ai/workflow.md)和[验证边界](../docs/ai/verification.md)。按实际影响读取相邻领域。

## 不属于本目录

游戏运行时代码和业务协议。

## 验证入口

运行 `dotnet test Tool/AiWorkflow/Tests/Lyking.AiWorkflow.Tests.csproj`，包含工具单元测试、临时 Git 仓库集成测试和幂等检查。工具独立于 Unity 程序集。
