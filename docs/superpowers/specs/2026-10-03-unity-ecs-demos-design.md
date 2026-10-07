# Unity ECS 性能对比 Demo 设计

## 目标

在 `Client/UnityEcsDemos/` 建立一个独立的 Unity 2022.3 示例项目，用同一套移动实体模拟分别展示传统 `MonoBehaviour`、Unity DOTS、Entitas 风格、Hydra VetBrain ECS 风格和 Unity Mass 的数据组织、更新路径和性能采样方式。外部包未安装时项目仍能导入、运行兼容后端并阅读真实接入边界。

## 设计决策

1. **独立项目**：不改动现有 `Client/GasUnity`，目录内包含 `Assets`、`Packages`、`ProjectSettings` 和固定场景入口。
2. **统一基准**：每个场景默认创建 10,000 个实体，每个实体执行位置积分、边界反弹和可选颜色刷新。实体数量、随机种子、固定步长和采样窗口由同一份配置控制。
3. **条件编译与适配边界**：DOTS Entities/Jobs 在独立后端文件中提供可编译的可选真实分支，用包版本定义包住引用；未安装包时由兼容后端提供相同可观察行为。Entitas、VetBrain 和 Mass 本版只提供独立场景、接口形状和宏占位，不声明真实 API 已接入；面板必须标注当前后端状态。
4. **场景隔离**：每个比较对象使用独立 `.unity` 场景文件，场景只负责装配 `EcsDemoBootstrap` 和后端选择，不把实体更新逻辑埋入场景对象。
5. **结果边界**：兼容后端只证明 API 形状和测试模型，不证明第三方包的真实性能。真实包性能必须在相同 Unity 版本、相同实体数和相同构建配置下重新采样。

## 场景

| 场景 | 内容 | 适合观察 |
|---|---|---|
| `00_GameObjectBaseline` | `MonoBehaviour.Update` 逐对象更新 | 调用开销、托管对象和 GC 基线 |
| `01_DotsEntities` | `IJobEntity`/组件数据更新 | 数据导向布局和批处理 |
| `02_DotsJobs` | 显式 `IJobParallelFor` 后端 | 并行调度开销与实体规模拐点 |
| `03_EntitasStyle` | Context、组件、Group、系统链 | 事件驱动和组合查询的可读性 |
| `04_HydraVetBrain` | World、Aspect、Grouping 风格适配 | 多 World、共享模拟和生成代码边界 |
| `05_UnityMass` | Mass Entity 可选适配 | 大量实体、空间查询和表示层分离 |

## 代码边界

- `Assets/Scripts/Benchmark/`：配置、统一统计、运行面板和后端接口。
- `Assets/Scripts/Backends/`：每个后端一个文件，兼容实现与真实包实现保持同一接口。
- `Assets/Scripts/Scenes/`：场景启动器与可选相机，不包含后端算法。
- `Assets/Scenes/`：六个独立场景和场景元数据。
- `Packages/manifest.json`：只锁 Unity 内置依赖；第三方包以 `Packages/README.md` 记录接入，不在缺少来源时伪造版本号。
- `Docs/`：运行步骤、对比口径、适用/不适用情景和结果记录模板。

## 适用性结论

- 传统 GameObject 更适合少量对象、强组件生命周期、编辑器直观操作和复杂单体行为。
- DOTS 适合同构实体数量大、更新规则稳定、需要 Burst/Job 并行和低托管分配的模拟；不适合把所有 UI、脚本化单体逻辑都强行数据化。
- Entitas 风格适合系统分层清晰、需要 Context/Group 查询和事件驱动的玩法；代码生成和组件注册会增加构建与调试成本。
- Hydra VetBrain 更适合作为共享模拟、World 隔离、Aspect/Grouping 和生成式约束的服务端或跨宿主边界；不能直接等同 Unity DOTS，也不应把私有 Hydra 程序集当成 Unity 包依赖。
- Unity Mass 适合大规模实体和空间索引，尤其是人群、交通、环境代理；对少量实体或高频独立行为会引入额外学习和转换成本。

## 验收

1. 六个场景文件和对应 `.meta` 存在，能在 Unity 2022.3 导入。
2. 无外部包时所有场景能运行兼容后端，面板显示实体数、更新耗时、帧耗时、GC 分配和后端状态。
3. DOTS 的可选真实分支和 Entitas、VetBrain、Mass 的适配占位均有包来源、编译宏、替换文件和限制说明，且没有把兼容后端结果写成真实包性能结论。
4. 纯 C# 基准逻辑测试通过；Unity 编辑模式/运行模式测试在编辑器可用时执行并记录退出码和结果文件。
5. 变更文档保持 `active`，直到真实 Unity 验证证据齐全；没有 Unity 编辑器时明确记录缺口。

## 回滚

删除 `Client/UnityEcsDemos/`、本设计文件、实施计划和对应 `docs/changes/active/20261003-unity-ecs-demos/` 即可回滚，不影响现有 Unity 工程。
