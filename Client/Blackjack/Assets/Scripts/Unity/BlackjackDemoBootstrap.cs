using System;
using Lyking.Blackjack.Core;
using UnityEngine;

namespace Lyking.Blackjack.Unity
{
    /// <summary>
    /// 21 点演示引导：装配核心对局、采集按键并转发到核心状态机。
    /// 玩家回合内 H 要牌、S 停牌、D 加倍，R 以新种子重开一局并保留筹码，
    /// 下注由面板按钮驱动；离散回合制无时间累加器，
    /// <see cref="Deal"/>、<see cref="Hit"/> 等公开入口供面板与测试直接驱动。
    /// </summary>
    public sealed class BlackjackDemoBootstrap : MonoBehaviour
    {
        [SerializeField] private BlackjackDemoPanel? panel;

        private BlackjackGame? _game;

        public BlackjackGame Game => _game ?? throw new InvalidOperationException("对局尚未创建。");

        public bool HasGame => _game is not null;

        public BlackjackDemoPanel? Panel => panel;

        private void Start()
        {
            if (panel == null) panel = FindObjectOfType<BlackjackDemoPanel>();
            if (panel == null)
            {
                // 场景缺省时动态创建面板物体，保证演示不依赖手工摆放。
                panel = new GameObject("BlackjackPanel").AddComponent<BlackjackDemoPanel>();
            }

            StartGame();
            panel.Bind(this);
        }

        /// <summary>以当前帧计数为种子开新局：首次创建对局，之后重开并保留筹码。</summary>
        public void StartGame()
        {
            int chips = _game?.Chips ?? BlackjackRules.InitialChips;
            _game = new BlackjackGame(Environment.TickCount, chips);
        }

        /// <summary>按下注额开局发牌；非法注额由核心规则拒绝。</summary>
        public void Deal(int bet)
        {
            if (HasGame) Game.Deal(bet);
        }

        /// <summary>玩家要牌。</summary>
        public void Hit()
        {
            if (HasGame) Game.Hit();
        }

        /// <summary>玩家停牌。</summary>
        public void Stand()
        {
            if (HasGame) Game.Stand();
        }

        /// <summary>玩家加倍：赌注翻倍且只再补一张。</summary>
        public void Double()
        {
            if (HasGame) Game.Double();
        }

        /// <summary>采集本帧按键：R 重开一局，玩家回合内 H/S/D 对应要牌、停牌、加倍。</summary>
        private void ReadInput()
        {
            if (Input.GetKeyDown(KeyCode.R))
            {
                StartGame();
                return;
            }

            if (!HasGame || Game.State != BlackjackPhase.PlayerTurn) return;
            if (Input.GetKeyDown(KeyCode.H)) Game.Hit();
            else if (Input.GetKeyDown(KeyCode.S)) Game.Stand();
            else if (Input.GetKeyDown(KeyCode.D)) Game.Double();
        }

        // 离散回合制：每帧只采集一次按键并即时调用核心，无时间累加器。
        private void Update()
        {
            ReadInput();
        }
    }
}
