using System.Linq;
using Lyking.DoodleJumper.Core;
using NUnit.Framework;

namespace Lyking.DoodleJumper.EditModeTests
{
    /// <summary>编辑模式冒烟：核心棋局在 Unity 程序集边界内可用，关键规则表现一致。</summary>
    public sealed class DoodleJumperEditModeTests
    {
        [Test]
        public void 核心在引擎边界内可创建并预生成平台()
        {
            var game = new JumperGame(JumperRules.Default, seed: 7);

            Assert.AreEqual(8f, game.PlayerPosition.X);
            Assert.AreEqual(0.5f, game.PlayerPosition.Y);
            Assert.AreEqual(JumperRules.Default.JumpSpeed, game.VelocityY);
            Assert.AreEqual(0f, game.MaxHeight);
            Assert.AreEqual(0, game.Score);
            Assert.IsFalse(game.IsFinished);
            Assert.AreEqual(2.5f, game.CameraCenterY, 0.001f);

            // 起始平台顶面为高度 0，上方已按前视余量预生成平台。
            Assert.AreEqual(JumperPlatformType.Normal, game.Platforms[0].Type);
            Assert.AreEqual(8f, game.Platforms[0].Center.X);
            Assert.AreEqual(-0.2f, game.Platforms[0].Center.Y);
            Assert.GreaterOrEqual(game.Platforms.Count, 5);
            Assert.AreEqual(JumperEventType.Spawned, game.Events[0].Type);
        }

        [Test]
        public void 弹跳轨迹与计分表现一致()
        {
            var game = new JumperGame(JumperRules.Default, seed: 1);

            // 每步重力增量 20×0.25=5：跳后 vy=5、高度 1.75、底部最高 1.25 换算 12 分。
            Assert.IsTrue(game.Advance(0.25f, JumperInput.None));
            Assert.AreEqual(1.75f, game.PlayerPosition.Y, 0.001f);
            Assert.AreEqual(5f, game.VelocityY, 0.001f);
            Assert.AreEqual(1.25f, game.MaxHeight, 0.001f);
            Assert.AreEqual(12, game.Score);

            // 第 2 步到达顶点，第 3 步落回起始平台弹起。
            Assert.IsTrue(game.Advance(0.25f, JumperInput.None));
            Assert.AreEqual(0f, game.VelocityY, 0.001f);
            Assert.IsTrue(game.Advance(0.25f, JumperInput.None));
            Assert.AreEqual(JumperEventType.Bounced, game.Events[game.Events.Count - 1].Type);
            Assert.AreEqual(0, game.Events[game.Events.Count - 1].PlatformIndex);
            Assert.AreEqual(JumperRules.Default.JumpSpeed, game.VelocityY, 0.001f);
            Assert.AreEqual(0.5f, game.PlayerPosition.Y, 0.001f);
        }

        [Test]
        public void 平台按固定种子生成且同种子可复现()
        {
            var game = new JumperGame(JumperRules.Default, seed: 7);
            var again = new JumperGame(JumperRules.Default, seed: 7);

            Assert.AreEqual(game.Platforms.Count, again.Platforms.Count);
            for (int i = 0; i < game.Platforms.Count; i++)
            {
                Assert.AreEqual(game.Platforms[i].Index, again.Platforms[i].Index);
                Assert.AreEqual(game.Platforms[i].Type, again.Platforms[i].Type);
                Assert.AreEqual(game.Platforms[i].Center.X, again.Platforms[i].Center.X);
                Assert.AreEqual(game.Platforms[i].Center.Y, again.Platforms[i].Center.Y);
            }

            // 推进后位置与事件序列仍逐位一致。
            for (int i = 0; i < 10; i++)
            {
                game.Advance(0.25f, new JumperInput(i % 2, false));
                again.Advance(0.25f, new JumperInput(i % 2, false));
            }

            Assert.AreEqual(game.PlayerPosition, again.PlayerPosition);
            Assert.AreEqual(game.Score, again.Score);
            Assert.AreEqual(game.MaxHeight, again.MaxHeight, 0.001f);

            // 不同种子的一号平台种类不同：种子 7 为普通，种子 11 为易碎。
            var other = new JumperGame(JumperRules.Default, seed: 11);
            Assert.AreEqual(JumperPlatformType.Normal, game.Platforms[1].Type);
            Assert.AreEqual(JumperPlatformType.Fragile, other.Platforms[1].Type);
        }

        [Test]
        public void 走出平台坠落死亡重置后恢复()
        {
            var game = new JumperGame(JumperRules.Default, seed: 1);
            game.Advance(0.25f, new JumperInput(1f, false));
            game.Advance(0.25f, new JumperInput(1f, false));
            for (int i = 3; i <= 20 && !game.IsFinished; i++) game.Advance(0.25f, JumperInput.None);

            Assert.IsTrue(game.IsFinished);
            Assert.AreEqual(JumperEventType.FellOut, game.Events[game.Events.Count - 1].Type);

            game.Reset(5);
            Assert.AreEqual(0.5f, game.PlayerPosition.Y, 0.001f);
            Assert.AreEqual(JumperRules.Default.JumpSpeed, game.VelocityY, 0.001f);
            Assert.AreEqual(0f, game.MaxHeight, 0.001f);
            Assert.AreEqual(0, game.Score);
            Assert.AreEqual(0, game.StepIndex);
            Assert.IsFalse(game.IsFinished);
            Assert.GreaterOrEqual(game.Platforms.Count, 5);
            Assert.IsTrue(game.Events.Any(e => e.Type == JumperEventType.Reset));

            // 重置后可正常推进：首个上升段抬升高度并计分。
            Assert.IsTrue(game.Advance(0.25f, JumperInput.None));
            Assert.AreEqual(1.25f, game.MaxHeight, 0.001f);
            Assert.AreEqual(12, game.Score);
        }
    }
}
