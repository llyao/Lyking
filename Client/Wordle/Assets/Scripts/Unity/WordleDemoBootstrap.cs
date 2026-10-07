using System;
using Lyking.Wordle.Core;
using UnityEngine;

namespace Lyking.Wordle.Unity
{
    /// <summary>
    /// 猜单词演示引导：装配核心对局并采集键盘输入，输入读取与核心模拟分离。
    /// 本局进行中 a-z 逐个输入、退格删除、回车提交；终局后 R 键开新局（连胜与局数统计保留）。
    /// <see cref="TypeLetter"/>、<see cref="Backspace"/>、<see cref="Submit"/> 等入口提供给测试直接驱动。
    /// </summary>
    public sealed class WordleDemoBootstrap : MonoBehaviour
    {
        [SerializeField] private WordleDemoPanel? panel;

        private WordleGame? _game;

        public WordleGame Game => _game ?? throw new InvalidOperationException("对局尚未创建。");

        public bool HasGame => _game is not null;

        public WordleDemoPanel? Panel => panel;

        private void Start()
        {
            if (panel == null) panel = FindObjectOfType<WordleDemoPanel>();
            if (panel == null)
            {
                // 场景缺省时动态创建面板物体，保证演示不依赖手工摆放。
                panel = new GameObject("WordlePanel").AddComponent<WordleDemoPanel>();
            }

            StartGame();
            panel.Bind(this);
        }

        /// <summary>开新局：首次创建对局，之后换答案并清空进度，连胜与局数统计保留。</summary>
        public void StartGame()
        {
            if (_game == null) _game = new WordleGame(Environment.TickCount);
            else _game.NextGame(Environment.TickCount);
        }

        /// <summary>测试入口：向当前行输入一个小写字母；非法输入由核心规则拒绝。</summary>
        public bool TypeLetter(char letter) => HasGame && Game.TypeLetter(letter);

        /// <summary>测试入口：删除当前行最后一个字母。</summary>
        public bool Backspace() => HasGame && Game.Backspace();

        /// <summary>测试入口：提交当前行，判定与胜负结算由核心规则执行。</summary>
        public bool Submit() => HasGame && Game.Submit();

        /// <summary>
        /// 采集本帧按键：本局进行中字母键输入、退格删除、回车提交；
        /// 终局后字母与提交被核心拒绝，R 键开新局。
        /// </summary>
        private void ReadInput()
        {
            if (!HasGame || Game.IsFinished)
            {
                if (Input.GetKeyDown(KeyCode.R)) StartGame();
                return;
            }

            for (KeyCode key = KeyCode.A; key <= KeyCode.Z; key++)
            {
                if (Input.GetKeyDown(key)) TypeLetter((char)('a' + (int)(key - KeyCode.A)));
            }

            if (Input.GetKeyDown(KeyCode.Backspace)) Backspace();
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) Submit();
        }

        private void Update()
        {
            ReadInput();
        }
    }
}
