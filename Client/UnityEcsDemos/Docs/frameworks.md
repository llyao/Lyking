# 框架适用性

## GameObject

适合少量对象、对象行为差异大、需要直接拖拽组件和快速做原型的玩法。对象数量达到数千且每帧逻辑高度同构时，逐对象 `Update`、托管对象和组件查找会成为主要成本。本 Demo 的 `00_GameObjectBaseline` 只提供数组兼容基线，不创建逐对象 `GameObject`，因此不能用它推断真实 MonoBehaviour 性能。

## Unity DOTS

适合大量同构实体、稳定的组件数据布局、Burst/Job 并行和低托管分配。UI、镜头、编辑器工具、少量有复杂生命周期的对象可以保留在 GameObject 层，通过桥接同步。

## Entitas

适合希望用 Context、Group、Matcher 和系统链表达玩法状态的团队。它的查询模型清晰，事件和组件拆分容易测试；代价是代码生成、组件注册和调试工具链需要统一版本。本 Demo 只展示这些边界，未绑定真实 Entitas 程序集。

## Hydra VetBrain ECS

适合共享模拟、多个 World、Aspect/Grouping、生成代码和跨宿主边界。它更接近 Hydra 的运行时基础设施，不能当成 Unity DOTS 的替代包；客户端接入应只保留适配层，网络、存档和 MMO 语义仍由各自模块负责。本 Demo 未把 Hydra 私有程序集带入 Unity，只提供适配占位。

## Unity Mass

适合人群、交通、环境代理等大规模实体和空间查询。少量实体、频繁单体脚本或需要直接操作 GameObject 的场景通常不值得引入 Mass 的转换和学习成本。Mass 可以在 Unity 中试验，但必须按目标 Unity 版本取得兼容包并单独测量。本 Demo 的 Mass 场景仍是兼容数组，不代表 Mass 性能。

## 情景速查

| 情景 | 优先考虑 | 原因 | 通常不优先 |
| --- | --- | --- | --- |
| 复杂 UI、背包、任务面板 | GameObject | 生命周期和编辑器拖拽比批量吞吐更重要 | DOTS/Mass |
| 1 万个同构子弹或粒子 | DOTS Entities/Jobs | 数据布局稳定，可批量更新并并行 | 逐对象 MonoBehaviour |
| 需要 Context/Group 事件链的战斗状态 | Entitas 风格 | 查询和系统边界清晰，便于拆分测试 | 只为几十个对象引入生成器 |
| 客户端与共享模拟使用多个 World | Hydra VetBrain 适配层 | 可以隔离 World、Aspect 和 Grouping 约束 | 把 VetBrain 当作 Unity DOTS 替代品 |
| 城市人群、交通和空间邻域查询 | Unity Mass | 面向大规模代理和空间组织 | 少量手工控制的关键角色 |
| 只有几百个对象但每个行为完全不同 | GameObject | 直接脚本化更简单，迁移成本低 | 为了理论吞吐强行数据化 |
