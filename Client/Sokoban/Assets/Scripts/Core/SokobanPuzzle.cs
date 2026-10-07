using System;
using System.Collections.Generic;

namespace Lyking.Sokoban.Core
{
    /// <summary>单步撤销记录：玩家原位、触发方向与被推箱子的原位/新位。</summary>
    internal readonly struct UndoRecord
    {
        public UndoRecord(GridPos playerPrevious, SokobanDir direction, GridPos? boxFrom, GridPos? boxTo)
        {
            PlayerPrevious = playerPrevious;
            Direction = direction;
            BoxFrom = boxFrom;
            BoxTo = boxTo;
        }

        public GridPos PlayerPrevious { get; }

        public SokobanDir Direction { get; }

        public GridPos? BoxFrom { get; }

        public GridPos? BoxTo { get; }
    }

    /// <summary>
    /// 推箱子核心棋局：确定性离散回合制状态机，无 Unity 依赖、无随机数。
    /// <see cref="Move"/> 目标格是墙则拒绝，是箱子则检查箱子前方（墙或另一箱则拒绝，否则推动）；
    /// 成功移动步数 +1，推动箱子另计推数 +1；箱子全部就位即 <see cref="IsSolved"/>。
    /// 每步可 <see cref="Undo"/> 恢复步数、推数与局面，<see cref="Reset"/> 回到关卡初始状态，
    /// 通关后 <see cref="NextLevel"/> 进入下一关。
    /// </summary>
    public sealed class SokobanPuzzle
    {
        private const int MaxEventLogs = 128;

        private readonly SokobanRules _rules;
        private readonly List<GridPos> _boxes = new List<GridPos>();
        private readonly HashSet<GridPos> _boxSet = new HashSet<GridPos>();
        private readonly List<UndoRecord> _undoStack = new List<UndoRecord>();
        private readonly List<SokobanEventLog> _events = new List<SokobanEventLog>();
        private int _levelIndex;
        private GridPos _player;
        private int _moveCount;
        private int _pushCount;

        public SokobanPuzzle(SokobanRules? rules = null, int levelIndex = 0)
        {
            _rules = rules ?? SokobanRules.Default;
            if (levelIndex < 0 || levelIndex >= _rules.LevelCount)
                throw new ArgumentOutOfRangeException(nameof(levelIndex), levelIndex, "关卡序号超出范围。");

            _levelIndex = levelIndex;
            LoadCurrentLevel();
            Log(SokobanEventType.LevelChanged, SokobanDir.None);
        }

        /// <summary>当前关卡序号（从 0 开始）。</summary>
        public int LevelIndex => _levelIndex;

        /// <summary>规则表中的关卡总数。</summary>
        public int LevelCount => _rules.LevelCount;

        public SokobanLevel CurrentLevel => _rules.Levels[_levelIndex];

        public GridPos Player => _player;

        /// <summary>当前箱子位置，按关卡装载顺序排列。</summary>
        public IReadOnlyList<GridPos> Boxes => _boxes;

        /// <summary>已完成的步数（不含被拒绝的尝试）。</summary>
        public int MoveCount => _moveCount;

        /// <summary>已完成的推动数。</summary>
        public int PushCount => _pushCount;

        /// <summary>本关是否通关：全部箱子都在目标点上。</summary>
        public bool IsSolved
        {
            get
            {
                foreach (var box in _boxes)
                {
                    if (!CurrentLevel.IsGoal(box)) return false;
                }

                return true;
            }
        }

        /// <summary>是否还有下一关。</summary>
        public bool HasNextLevel => _levelIndex < _rules.LevelCount - 1;

        /// <summary>是否已通关最后一关。</summary>
        public bool IsAllCompleted => IsSolved && !HasNextLevel;

        public IReadOnlyList<SokobanEventLog> Events => _events;

        /// <summary>指定格子上是否有箱子。</summary>
        public bool IsBoxAt(GridPos pos) => _boxSet.Contains(pos);

        /// <summary>
        /// 向指定方向走一步。目标格是墙、箱子前方是墙或另一箱时拒绝并记录事件；
        /// 成功则步数 +1，推动箱子另计推数 +1，推动后全部箱子就位即记录过关。已通关的关卡拒绝继续移动。
        /// </summary>
        public bool Move(SokobanDir dir)
        {
            if (dir == SokobanDir.None || IsSolved) return false;

            var delta = dir.ToDelta();
            var target = _player + delta;
            if (CurrentLevel.IsWall(target))
            {
                Log(SokobanEventType.MoveRejected, dir);
                return false;
            }

            GridPos? boxFrom = null;
            GridPos? boxTo = null;
            if (_boxSet.Contains(target))
            {
                var beyond = target + delta;
                if (CurrentLevel.IsWall(beyond) || _boxSet.Contains(beyond))
                {
                    Log(SokobanEventType.MoveRejected, dir);
                    return false;
                }

                boxFrom = target;
                boxTo = beyond;
                _boxSet.Remove(target);
                _boxSet.Add(beyond);
                _boxes[_boxes.IndexOf(target)] = beyond;
            }

            _undoStack.Add(new UndoRecord(_player, dir, boxFrom, boxTo));
            _player = target;
            _moveCount++;

            if (boxFrom is not null)
            {
                _pushCount++;
                Log(SokobanEventType.Pushed, dir);
                if (IsSolved) Log(SokobanEventType.LevelSolved, dir);
            }
            else
            {
                Log(SokobanEventType.Moved, dir);
            }

            return true;
        }

        /// <summary>撤销上一步：恢复玩家、箱子、步数与推数。无步可撤返回 false。</summary>
        public bool Undo()
        {
            if (_undoStack.Count == 0) return false;

            var record = _undoStack[^1];
            _undoStack.RemoveAt(_undoStack.Count - 1);
            _player = record.PlayerPrevious;
            if (record.BoxFrom is GridPos from)
            {
                var to = record.BoxTo!.Value;
                _boxSet.Remove(to);
                _boxSet.Add(from);
                _boxes[_boxes.IndexOf(to)] = from;
                _pushCount--;
            }

            _moveCount--;
            Log(SokobanEventType.Undone, record.Direction);
            return true;
        }

        /// <summary>重置当前关：回到关卡初始状态并清空撤销栈。</summary>
        public void Reset()
        {
            LoadCurrentLevel();
            Log(SokobanEventType.Reset, SokobanDir.None);
        }

        /// <summary>进入下一关：仅当前关已全部箱子就位且还有后续关卡时允许，返回是否切换成功。</summary>
        public bool NextLevel()
        {
            if (!IsSolved || !HasNextLevel) return false;

            _levelIndex++;
            LoadCurrentLevel();
            Log(SokobanEventType.LevelChanged, SokobanDir.None);
            return true;
        }

        /// <summary>按关卡初始集合重建箱子、玩家与全部计数，清空撤销栈。</summary>
        private void LoadCurrentLevel()
        {
            var level = CurrentLevel;
            _boxes.Clear();
            _boxSet.Clear();
            foreach (var box in level.InitialBoxes)
            {
                _boxes.Add(box);
                _boxSet.Add(box);
            }

            _player = level.PlayerStart;
            _moveCount = 0;
            _pushCount = 0;
            _undoStack.Clear();
        }

        private void Log(SokobanEventType type, SokobanDir direction)
        {
            _events.Add(new SokobanEventLog(_moveCount, type, direction));
            if (_events.Count > MaxEventLogs) _events.RemoveRange(0, _events.Count - MaxEventLogs);
        }
    }
}
