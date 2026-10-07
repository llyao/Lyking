<!-- lyking-ai
{"schemaVersion":1,"id":"20261003-unity-ecs-demos","profile":"standard","status":"active","areas":["Client"],"paths":["docs/changes/active/20261003-unity-ecs-demos/","docs/superpowers/specs/2026-10-03-unity-ecs-demos-design.md","docs/superpowers/plans/2026-10-03-unity-ecs-demos.md","Client/UnityEcsDemos/"],"riskSignals":["architecture-choice","runtime-integration"]}
-->

# Unity ECS 性能对比 Demo

## 解决什么问题

在现有客户端仓库中提供一套可独立打开的 Unity 示例，用同一批移动实体比较传统 GameObject、Unity DOTS、Entitas 风格、Hydra VetBrain ECS 风格和 Unity Mass 的代码组织与性能采样方式。

## 当前状态

骨架已经实现，正在等待目标 Unity 环境验证。外部包采用可选接入；无包时通过兼容后端保证场景可运行，但兼容后端不作为第三方包性能证据。

## 完成后应看到什么

`Client/UnityEcsDemos/` 是独立 Unity 2022.3 项目，包含六个独立场景。打开任意场景后能看到实体数、后端状态、平均/峰值更新耗时、帧耗时和 GC 分配；安装 Unity Entities 后可按文档切换 DOTS 真实分支，Entitas、VetBrain 和 Mass 本版保留适配占位。

## 范围与非目标

- 新建 Unity 2022.3 Demo 项目和六个场景。
- 新建统一基准模型、统计器、启动器和运行面板。
- 新建 GameObject、DOTS、Entitas 风格、Hydra VetBrain 风格、Mass 后端；Entitas、VetBrain 和 Mass 本版提供适配占位，DOTS 提供可选真实分支。
- 提供包接入说明、性能记录模板和适用性分析。
- 记录结构验证、纯逻辑验证和 Unity 验证缺口。

非目标：不复制 Hydra MMO 全量运行时，不伪造第三方包版本，不把兼容实现冒充真实 ECS，不修改现有 `Client/GasUnity`，不承诺跨机器或跨 Unity 补丁版本的绝对性能排名。


## 验收方式

运行工作流 `validate`、`git diff --check`，并在 Unity 可用时执行项目导入、编辑模式/运行模式测试和场景烟测。真实第三方包性能只有在对应包和固定运行环境都可用时记录。

## 风险与回滚

最大风险是第三方包来源和 Unity 补丁版本不一致导致真实分支不能编译。通过条件编译、兼容后端和接入文档隔离该风险。回滚时删除 `Client/UnityEcsDemos/` 及本变更文档即可。
