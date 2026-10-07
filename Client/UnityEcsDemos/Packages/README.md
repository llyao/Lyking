# 可选包接入

当前 `manifest.json` 只包含 Unity 内置模块和 Unity Test Framework，保证没有外部 ECS 包时项目也能打开。测试框架本身不提供 DOTS、Entitas、VetBrain 或 Mass 实现。

## Unity Entities / DOTS

在 Unity 2022.3 项目中通过 Package Manager 安装与编辑器兼容的 `com.unity.entities`，然后在对应程序集的版本定义中启用 `UNITY_ENTITIES`。示例后端的真实 Entities 分支集中在 `Assets/Scripts/Backends/DotsEntitiesBackend.cs` 和 `DotsJobsBackend.cs`；未安装包时仍会回退到兼容数组实现。

## Entitas

Entitas 的发行包和代码生成器有多个版本，不能在没有确认来源时写死一个公共 UPM 坐标。将生成的 `Context`、组件和 `Group` 程序集放入项目后，后续可启用 `LYKING_ENTITAS_PRESENT` 并按 `EntitasStyleBackend` 的接口把查询系统接到统一基准。当前宏只用于显示“待接入”状态，不会伪造真实 API。

## Hydra VetBrain ECS

VetBrain 是 Hydra 工作区中的共享 ECS 程序集，不是 Unity 官方包。当前可访问参考路径为 `E:\hydra_git2\hydra\hydra-common\VetBrain`。把与 Unity 目标框架兼容的 VetBrain 程序集作为外部引用后，后续可启用 `LYKING_VETBRAIN_PRESENT`；当前示例只约束 World/Aspect/Grouping 的适配面，不复制 Hydra 的 MMO 网络、存档和生成器，宏也不会自动接入真实程序集。

## Unity Mass

Mass 依赖 Unity 版本和对应的包源。安装可用的 Mass Entity 包后，后续可启用 `LYKING_MASS_PRESENT`，将 `UnityMassBackend` 的实体查询接到同一采样器。当前场景仍使用兼容数组。Mass 与 Entities 都是数据导向方案，但 API、转换管线和空间查询模型不同，不能只替换命名空间。
