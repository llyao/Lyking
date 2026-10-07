using System;
using System.Collections.Generic;

namespace Lyking.Simon.Core
{
    /// <summary>对局阶段：等待首次输入 → 播放序列 → 复述比对 → 已失败。</summary>
    public enum SimonPhase
    {
        /// <summary>未开始：等待首次输入，任何色块开局。</summary>
        Ready,

        /// <summary>播放中：按节奏逐个点亮第 Level 轮序列，此时不接受复述输入。</summary>
        Showing,

        /// <summary>复述中：逐项比对输入，限时内输满即进入下一轮。</summary>
        Input,

        /// <summary>已结束：按错或超时，输入与推进均无效。</summary>
        GameOver,
    }

    /// <summary>
    /// 西蒙记忆核心对局：2x2 四色序列复述，无 Unity 依赖、无内建时钟。
    /// 序列在构造时以注入种子的 <see cref="Random"/> 逐项 <c>Next(4)</c> 预生成
    /// <see cref="SimonRules.MaxSequenceLength"/> 项封顶，同种子对局完全一致；
    /// 第 N 轮固定取前 N 项，因此任意轮与前轮共享前缀。播放节奏（点亮
    /// <see cref="SimonRules.LitSeconds"/> 秒、间隔 <see cref="SimonRules.GapSeconds"/> 秒）
    /// 与复述输入超时由 <see cref="Step"/> 按调用粒度推进，单次大步长不补偿中间错过的点亮。
    /// <see cref="Press"/> 为离散输入：Ready 阶段任意色块开局，Showing 阶段拒绝，
    /// Input 阶段逐项比对，全部按对升等级回播放，按错或超时即失败并记录最长纪录。
    /// </summary>
    public sealed class SimonGame
    {
        private const int MaxEventLogs = 128;

        private Random _random;
        private readonly int[] _sequence = new int[SimonRules.MaxSequenceLength];
        private readonly List<SimonEventLog> _events = new List<SimonEventLog>();

        private float _showElapsed;

        public SimonGame(int seed = 0)
        {
            _random = new Random(seed);
            FillSequence();
        }

        public SimonPhase Phase { get; private set; } = SimonPhase.Ready;

        /// <summary>当前轮等级：第几轮序列，Ready 阶段为 0。</summary>
        public int Level { get; private set; }

        /// <summary>最长纪录：完整复述成功的最高轮数，跨局保留。</summary>
        public int BestLevel { get; private set; }

        /// <summary>复述阶段已按对的项数，进入新一轮播放时清零。</summary>
        public int InputProgress { get; private set; }

        /// <summary>当前点亮的色块编号，无点亮时为 -1。</summary>
        public int LitPadIndex { get; private set; } = -1;

        /// <summary>本轮播放剩余秒数，仅 Showing 阶段为正。</summary>
        public float ShowingRemaining { get; private set; }

        /// <summary>复述输入剩余秒数，仅 Input 阶段为正。</summary>
        public float InputRemaining { get; private set; }

        public bool IsOver => Phase == SimonPhase.GameOver;

        /// <summary>预生成的完整序列；第 N 轮取前 N 项，仅供测试与调试检视。</summary>
        public IReadOnlyList<int> Sequence => _sequence;

        public IReadOnlyList<SimonEventLog> Events => _events;

        /// <summary>
        /// 输入一个色块。Ready 阶段任意色块开局；Showing 与 GameOver 阶段拒绝；
        /// Input 阶段逐项比对：按对推进进度，输满整轮升等级回播放，按错即失败。
        /// 返回输入是否被核心接受。
        /// </summary>
        public bool Press(int padIndex)
        {
            if (padIndex < 0 || padIndex >= SimonRules.PadCount)
                throw new ArgumentOutOfRangeException(nameof(padIndex), padIndex, "色块编号越界。");

            if (Phase == SimonPhase.Ready)
            {
                Log(SimonEventType.Started);
                Level = 1;
                StartShowing();
                return true;
            }

            if (Phase != SimonPhase.Input) return false;

            if (padIndex != _sequence[InputProgress])
            {
                Log(SimonEventType.InputWrong, padIndex, _sequence[InputProgress]);
                EndGame();
                return false;
            }

            InputProgress++;
            Log(SimonEventType.InputCorrect, padIndex, InputProgress);
            if (InputProgress >= Level)
            {
                Level++;
                StartShowing();
            }

            return true;
        }

        /// <summary>推进计时。Showing 阶段推进点亮节奏，Input 阶段推进输入超时，其余阶段无效。</summary>
        public void Step(float deltaTime)
        {
            if (Phase == SimonPhase.Showing) StepShowing(deltaTime);
            else if (Phase == SimonPhase.Input) StepInput(deltaTime);
        }

        /// <summary>
        /// 重开一局：以新种子重建序列，清空日志与对局状态，仅保留最长纪录。
        /// 重开后回到 Ready，等待下一次首次输入。
        /// </summary>
        public void Reset(int seed)
        {
            _random = new Random(seed);
            FillSequence();
            Phase = SimonPhase.Ready;
            Level = 0;
            InputProgress = 0;
            LitPadIndex = -1;
            ShowingRemaining = 0f;
            InputRemaining = 0f;
            _showElapsed = 0f;
            _events.Clear();
            Log(SimonEventType.Reset);
        }

        /// <summary>进入第 Level 轮播放：重置播放计时并立刻点亮序列首项。</summary>
        private void StartShowing()
        {
            // 序列按 MaxSequenceLength 封顶：等级越界时循环挑战同一满长序列（实际对局几乎不可达）。
            if (Level > SimonRules.MaxSequenceLength) Level = SimonRules.MaxSequenceLength;

            _showElapsed = 0f;
            ShowingRemaining = Level * SimonRules.SegmentSeconds;
            InputProgress = 0;
            Phase = SimonPhase.Showing;
            Log(SimonEventType.ShowingStarted);
            SetLit(_sequence[0]);
        }

        private void StepShowing(float deltaTime)
        {
            _showElapsed += deltaTime;
            int segment = (int)(_showElapsed / SimonRules.SegmentSeconds);
            if (segment >= Level)
            {
                // 播放完毕进入复述：输入超时从满值开始倒数。
                ShowingRemaining = 0f;
                InputRemaining = SimonRules.InputTimeoutSeconds;
                Phase = SimonPhase.Input;
                SetLit(-1);
                return;
            }

            float within = _showElapsed - segment * SimonRules.SegmentSeconds;
            SetLit(within < SimonRules.LitSeconds ? _sequence[segment] : -1);
            ShowingRemaining = Level * SimonRules.SegmentSeconds - _showElapsed;
        }

        private void StepInput(float deltaTime)
        {
            InputRemaining -= deltaTime;
            if (InputRemaining > 0f) return;

            // 复述超时未输满：与按错同样失败。
            InputRemaining = 0f;
            EndGame();
        }

        /// <summary>点亮一个色块并在点亮内容变化时记录事件，熄灭不记录。</summary>
        private void SetLit(int padIndex)
        {
            if (LitPadIndex == padIndex) return;

            LitPadIndex = padIndex;
            if (padIndex >= 0) Log(SimonEventType.Lit, padIndex);
        }

        /// <summary>结束对局：记失败事件，完整复述成功的轮数刷新最长纪录时记新纪录。</summary>
        private void EndGame()
        {
            Phase = SimonPhase.GameOver;
            Log(SimonEventType.Failed);

            int completed = Level - 1;
            if (completed > BestLevel)
            {
                BestLevel = completed;
                Log(SimonEventType.NewRecord, -1, completed);
            }
        }

        private void FillSequence()
        {
            for (int i = 0; i < SimonRules.MaxSequenceLength; i++)
            {
                _sequence[i] = _random.Next(SimonRules.PadCount);
            }
        }

        private void Log(SimonEventType type, int padIndex = -1, int detail = -1)
        {
            _events.Add(new SimonEventLog(Level, type, padIndex, detail));
            if (_events.Count > MaxEventLogs) _events.RemoveRange(0, _events.Count - MaxEventLogs);
        }
    }
}
