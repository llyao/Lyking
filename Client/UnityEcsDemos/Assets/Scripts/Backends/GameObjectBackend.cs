using Lyking.UnityEcsDemos.Benchmark;

namespace Lyking.UnityEcsDemos.Backends
{
    /// <summary>
    /// GameObject API 形状基线。默认兼容模式使用相同的实体数据，不创建大量场景对象。
    /// </summary>
    public sealed class GameObjectBackend : ArraySimulationBackend
    {
        public GameObjectBackend()
            : base("GameObject API 基线", "内置兼容数组循环（未创建 GameObject）", false)
        {
        }

        public override void Step(float deltaTime)
        {
            // 真实 GameObject 对象会在单独实验中创建；本项目默认只比较统一数据更新形状。
            base.Step(deltaTime);
        }
    }
}
