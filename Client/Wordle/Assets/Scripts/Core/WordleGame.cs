using System;
using System.Collections.Generic;

namespace Lyking.Wordle.Core
{
    /// <summary>对局状态：正在输入当前行、猜中获胜或六次未中失败。</summary>
    public enum WordleGameState
    {
        /// <summary>正在输入当前行。</summary>
        Input,

        /// <summary>猜中答案获胜。</summary>
        Won,

        /// <summary>六次尝试未中失败。</summary>
        Lost,
    }

    /// <summary>
    /// 猜单词对局状态机：确定性离散回合制，无 Unity 依赖、无内部随机数。
    /// 每局由注入种子从词库确定性选出答案；TypeLetter/Backspace 编辑当前行且不产生事件，
    /// Submit 在满 5 字母且命中词库时记一次尝试并判定，猜中转 Won 并连胜加一，第 6 次未中转 Lost 并连胜清零。
    /// 重开（NewGame）只清空进度并保留连胜与局数统计；事件日志封顶 128 条裁剪。
    /// </summary>
    public sealed class WordleGame
    {
        private const int MaxEventLogs = 128;

        private readonly List<WordleEventLog> _events = new List<WordleEventLog>();
        private readonly List<string> _guesses = new List<string>();
        private readonly List<IReadOnlyList<LetterHint>> _hints = new List<IReadOnlyList<LetterHint>>();
        private readonly char[] _input = new char[WordleRules.WordLength];
        private int _inputLength;

        public WordleGame(int seed)
        {
            Round = 1;
            BeginRound(seed);
        }

        /// <summary>当前局数（从 1 起），每次新局加一。</summary>
        public int Round { get; private set; }

        /// <summary>当前连胜局数：获胜加一、失败清零，重开保留。</summary>
        public int WinStreak { get; private set; }

        public WordleGameState State { get; private set; } = WordleGameState.Input;

        /// <summary>本局答案，由注入种子从词库确定性选出。</summary>
        public string Answer { get; private set; } = string.Empty;

        /// <summary>本局已提交的尝试次数（0 到 6）。</summary>
        public int AttemptCount => _guesses.Count;

        public bool IsFinished => State != WordleGameState.Input;

        /// <summary>当前输入行已输入的字母。</summary>
        public string CurrentGuess => new string(_input, 0, _inputLength);

        /// <summary>本局已提交的各次猜测，按提交顺序。</summary>
        public IReadOnlyList<string> Guesses => _guesses;

        /// <summary>各次猜测的逐格判定结果，与 <see cref="Guesses"/> 一一对应。</summary>
        public IReadOnlyList<IReadOnlyList<LetterHint>> Hints => _hints;

        public IReadOnlyList<WordleEventLog> Events => _events;

        /// <summary>
        /// 向当前行输入一个小写字母。终局后、非 a-z 或当前行已满 5 字母时拒绝并返回 false。
        /// 输入只编辑当前行，不产生事件。
        /// </summary>
        public bool TypeLetter(char letter)
        {
            if (IsFinished) return false;
            if (letter < 'a' || letter > 'z') return false;
            if (_inputLength >= WordleRules.WordLength) return false;

            _input[_inputLength++] = letter;
            return true;
        }

        /// <summary>删除当前行最后一个字母。终局后或当前行为空时拒绝并返回 false，不产生事件。</summary>
        public bool Backspace()
        {
            if (IsFinished) return false;
            if (_inputLength == 0) return false;

            _inputLength--;
            _input[_inputLength] = '\0';
            return true;
        }

        /// <summary>
        /// 提交当前行：不足 5 字母或不在词库内时拒绝（返回 false 并记拒绝事件，不消耗尝试）；
        /// 接受后清空当前行、记一次尝试并判定，猜中转 Won 且连胜加一，第 6 次未中转 Lost 且连胜清零。
        /// </summary>
        public bool Submit()
        {
            if (IsFinished) return false;

            string guess = CurrentGuess;
            if (_inputLength < WordleRules.WordLength || !WordleRules.ContainsWord(guess))
            {
                Log(new WordleEventLog(Round, AttemptCount, WordleEventType.WordRejected, guess, null, null));
                return false;
            }

            var hints = WordleEvaluator.Evaluate(guess, Answer);
            _guesses.Add(guess);
            _hints.Add(hints);
            ClearInput();
            Log(new WordleEventLog(Round, AttemptCount, WordleEventType.Submitted, guess, null, hints));

            if (guess == Answer)
            {
                State = WordleGameState.Won;
                WinStreak++;
                Log(new WordleEventLog(Round, AttemptCount, WordleEventType.Won, null, Answer, null));
            }
            else if (AttemptCount >= WordleRules.MaxAttempts)
            {
                State = WordleGameState.Lost;
                WinStreak = 0;
                Log(new WordleEventLog(Round, AttemptCount, WordleEventType.Lost, null, Answer, null));
            }

            return true;
        }

        /// <summary>开新局：局数加一，答案按新种子重选，进度清空，连胜与统计保留。</summary>
        public void NextGame(int seed)
        {
            Round++;
            BeginRound(seed);
            Log(new WordleEventLog(Round, 0, WordleEventType.NewGame, null, null, null));
        }

        /// <summary>整体重置：局数回到 1、连胜清零，并以新种子开局。</summary>
        public void Reset(int seed)
        {
            Round = 1;
            WinStreak = 0;
            BeginRound(seed);
            Log(new WordleEventLog(Round, 0, WordleEventType.Reset, null, null, null));
        }

        /// <summary>按注入种子从词库确定性选出本局答案。</summary>
        private static string PickWord(int seed)
        {
            var random = new Random(seed);
            return WordleRules.WordBank[random.Next(WordleRules.WordBank.Count)];
        }

        /// <summary>清空当前输入行，供退格编辑与提交成功后进入下一行使用。</summary>
        private void ClearInput()
        {
            _inputLength = 0;
            for (int i = 0; i < _input.Length; i++) _input[i] = '\0';
        }

        /// <summary>开启新的一局：重选答案、清空输入与已提交进度，状态回到输入中。</summary>
        private void BeginRound(int seed)
        {
            Answer = PickWord(seed);
            _guesses.Clear();
            _hints.Clear();
            ClearInput();
            State = WordleGameState.Input;
        }

        private void Log(WordleEventLog entry)
        {
            _events.Add(entry);
            if (_events.Count > MaxEventLogs) _events.RemoveRange(0, _events.Count - MaxEventLogs);
        }
    }
}
