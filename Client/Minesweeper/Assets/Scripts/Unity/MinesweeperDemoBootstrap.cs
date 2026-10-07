#nullable enable
using System;
using Lyking.Minesweeper.Core;
using UnityEngine;

namespace Lyking.Minesweeper.Unity
{
    /// <summary>
    /// 扫雷演示引导：持有核心棋盘，负责难度切换、重开、按键分发与计时。
    /// 翻开与插旗提供公共入口，输入读取与游戏动作分离，便于测试替换。
    /// 计时属于表现层状态：进行中累计，重开归零，不进入核心规则。
    /// </summary>
    public sealed class MinesweeperDemoBootstrap : MonoBehaviour
    {
        [SerializeField] private MinesweeperDemoPanel? panel;
        [SerializeField] private Difficulty difficulty = Difficulty.Beginner;
        [SerializeField] private bool useFixedSeed;
        [SerializeField] private int fixedSeed = 20261004;

        /// <summary>演示难度，映射到核心 <see cref="BoardSpec"/> 预设。</summary>
        public enum Difficulty
        {
            Beginner = 0,
            Intermediate = 1,
            Expert = 2,
        }

        private MinesweeperBoard? _board;
        private float _elapsedSeconds;

        public MinesweeperBoard Board => _board ?? throw new InvalidOperationException("棋盘尚未创建。");

        public bool HasBoard => _board is not null;

        public Difficulty CurrentDifficulty => difficulty;

        public bool UseFixedSeed
        {
            get => useFixedSeed;
            set => useFixedSeed = value;
        }

        public int FixedSeed
        {
            get => fixedSeed;
            set => fixedSeed = value;
        }

        /// <summary>本局已用秒数，仅在对局进行中累计。</summary>
        public float ElapsedSeconds => _elapsedSeconds;

        private void Start()
        {
            panel ??= FindObjectOfType<MinesweeperDemoPanel>();
            if (!HasBoard)
            {
                // Start 前调用过 Restart/SelectDifficulty 时保留已建棋盘，不覆盖外部难度选择。
                CreateBoard();
            }

            if (panel is not null)
            {
                panel.Bind(this);
                panel.SetMessage("左键翻开，右键插旗");
            }
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.R))
            {
                ProcessKey(KeyCode.R);
            }
            else if (Input.GetKeyDown(KeyCode.Alpha1))
            {
                ProcessKey(KeyCode.Alpha1);
            }
            else if (Input.GetKeyDown(KeyCode.Alpha2))
            {
                ProcessKey(KeyCode.Alpha2);
            }
            else if (Input.GetKeyDown(KeyCode.Alpha3))
            {
                ProcessKey(KeyCode.Alpha3);
            }

            if (_board is not null && _board.State == GameState.Playing)
            {
                _elapsedSeconds += Time.deltaTime;
            }
        }

        private void OnDestroy()
        {
            if (_board is not null)
            {
                _board.EventPublished -= OnBoardEvent;
            }
        }

        /// <summary>处理一帧按键输入：R 重开，1/2/3 切换难度。返回是否消耗了按键。</summary>
        public bool ProcessKey(KeyCode key)
        {
            switch (key)
            {
                case KeyCode.R:
                    Restart();
                    return true;
                case KeyCode.Alpha1:
                    SelectDifficulty(Difficulty.Beginner);
                    return true;
                case KeyCode.Alpha2:
                    SelectDifficulty(Difficulty.Intermediate);
                    return true;
                case KeyCode.Alpha3:
                    SelectDifficulty(Difficulty.Expert);
                    return true;
                default:
                    return false;
            }
        }

        public RevealOutcome RevealCell(int x, int y)
        {
            return Board.Reveal(x, y);
        }

        public FlagOutcome ToggleFlagCell(int x, int y)
        {
            return Board.ToggleFlag(x, y);
        }

        public void Restart()
        {
            CreateBoard();
        }

        public void SelectDifficulty(Difficulty value)
        {
            difficulty = value;
            CreateBoard();
        }

        private void CreateBoard()
        {
            if (_board is not null)
            {
                _board.EventPublished -= OnBoardEvent;
            }

            BoardSpec spec = difficulty switch
            {
                Difficulty.Intermediate => BoardSpec.Intermediate,
                Difficulty.Expert => BoardSpec.Expert,
                _ => BoardSpec.Beginner,
            };
            IMinesweeperRandom random = useFixedSeed ? new SystemRandomWrapper(fixedSeed) : new SystemRandomWrapper();
            _board = new MinesweeperBoard(spec, random);
            _board.EventPublished += OnBoardEvent;
            _elapsedSeconds = 0;
            if (panel is not null)
            {
                panel.AppendEvent(new MinesweeperEvent(MinesweeperEventType.NewGame));
                panel.SetMessage("左键翻开，右键插旗");
            }
        }

        private void OnBoardEvent(MinesweeperEvent gameplayEvent)
        {
            if (panel is null)
            {
                return;
            }

            panel.AppendEvent(gameplayEvent);
            switch (gameplayEvent.Type)
            {
                case MinesweeperEventType.MineHit:
                case MinesweeperEventType.Lost:
                    panel.SetMessage("踩雷，对局失败。按 R 重开。");
                    break;
                case MinesweeperEventType.Won:
                    panel.SetMessage("全部安全格已翻开，胜利！按 R 再来一局。");
                    break;
            }
        }
    }
}
