using System.Collections;
using Lyking.Blackjack.Core;
using Lyking.Blackjack.Unity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Lyking.Blackjack.PlayModeTests
{
    public sealed class BlackjackPlayModeTests
    {
        [UnityTest]
        public IEnumerator 场景启动后对局就绪且面板已绑定()
        {
            AsyncOperation load = SceneManager.LoadSceneAsync("BlackjackDemo");
            yield return load;
            yield return null;

            var bootstrap = Object.FindObjectOfType<BlackjackDemoBootstrap>();
            Assert.IsNotNull(bootstrap);
            Assert.IsTrue(bootstrap!.HasGame);
            Assert.AreEqual(BlackjackPhase.Betting, bootstrap.Game.State);
            Assert.AreEqual(1, bootstrap.Game.Round);
            Assert.AreEqual(BlackjackRules.InitialChips, bootstrap.Game.Chips);
            Assert.IsEmpty(bootstrap.Game.PlayerCards);
            Assert.IsNotNull(bootstrap.Panel);
        }

        [UnityTest]
        public IEnumerator 下注推进结算后重开复位且筹码保留()
        {
            AsyncOperation load = SceneManager.LoadSceneAsync("BlackjackDemo");
            yield return load;
            yield return null;

            var bootstrap = Object.FindObjectOfType<BlackjackDemoBootstrap>();
            Assert.IsNotNull(bootstrap);

            // 下注发牌；天牌会直接结算，因此仅在实际进入玩家回合时断言筹码。
            bootstrap!.Deal(BlackjackRules.MinBet);
            if (bootstrap.Game.State == BlackjackPhase.PlayerTurn)
            {
                Assert.AreEqual(BlackjackRules.InitialChips - BlackjackRules.MinBet, bootstrap.Game.Chips);
                bootstrap.Hit();
                if (bootstrap.Game.State == BlackjackPhase.PlayerTurn) bootstrap.Stand();
            }

            Assert.AreEqual(BlackjackPhase.Settled, bootstrap.Game.State);

            // 重开以新种子重建对局：阶段复位、手牌清空、筹码保留。
            int chipsAfterSettle = bootstrap.Game.Chips;
            bootstrap.StartGame();
            Assert.AreEqual(BlackjackPhase.Betting, bootstrap.Game.State);
            Assert.AreEqual(chipsAfterSettle, bootstrap.Game.Chips);
            Assert.IsEmpty(bootstrap.Game.PlayerCards);
            Assert.IsEmpty(bootstrap.Game.DealerCards);
            Assert.AreEqual(0, bootstrap.Game.Bet);
        }
    }
}
