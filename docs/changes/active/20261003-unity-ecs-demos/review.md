# 独立审查记录

## 审查范围

- 目标：`Client/UnityEcsDemos/` 六个独立场景、统一基准、可选后端边界和变更文档。
- 关注：生命周期、统计口径、兼容实现标识、真实包声明和场景装配。
- 审查方式：读取当前未提交差异和完整相关源码，沿无包兼容路径、DOTS 条件编译路径和异常路径检查。

## 发现与处理

### 已处理

1. 启动器原先可能在 `Awake` 和 `BenchmarkRunner.Start` 各初始化一次后端。现在由 `EcsDemoBootstrap` 关闭自动启动后只保留一处启动。
2. 后端更新抛异常时原先可能遗留未结束的计时状态。现在 `BenchmarkRunner` 用 `try/finally` 结束更新采样。
3. 统计文档宣称峰值但实现未提供。现在统计快照和面板都按最近窗口计算更新峰值与帧峰值。
4. DOTS Entities 真实分支原先忽略颜色刷新。现在加入 `ColorPhaseComponent` 并在 Job 中按配置更新。
5. GameObject、Entitas、VetBrain、Mass 的兼容实现曾容易被理解成真实包性能。现在代码状态、README、基准方法和包说明均明确它们的限制；Entitas、VetBrain、Mass 本版是适配占位，GameObject 是数组兼容基线。

## 残余风险

### 高

- 当前主机没有可用的 Unity 2022.3.40f1，且 Unity 6000.4.0f1 许可证不可用，因此不能把导入、编辑器测试或播放模式烟测写成已通过。
- 没有安装 Entitas、Hydra VetBrain 或 Mass 的目标程序集/包，本版没有真实第三方 API 和性能数据。

### 中

- `00_GameObjectBaseline` 不创建逐对象 `GameObject`，不能代表真实 `MonoBehaviour.Update` 调用成本；README 和面板已经明确这一点。
- DOTS Entities 的真实分支只在安装包并定义 `UNITY_ENTITIES` 时编译，本次环境没有验证该分支。

### 低

- 兼容基准脚本测量的是托管数组循环，结果只用于检查统一模型和数量扩展趋势。
- 已新增 `Assets/Tests/EditMode/Editor/BenchmarkCoreTests.cs` 测试资产，覆盖边界反弹、颜色刷新、统计窗口和配置校验；但尚未在 Unity 编辑器中执行，需目标版本环境补充证据。

## 结论

未发现当前实现中的阻塞级正确性问题。变更应保持 `active`，直到取得目标 Unity 补丁版本的导入/运行证据，以及真实第三方包的独立适配和基准证据。
