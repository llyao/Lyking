#nullable enable
using Lyking.Game2048.Core;
using UnityEngine;

namespace Lyking.Game2048.Unity
{
    /// <summary>键盘输入到滑动方向的映射，与运行时输入读取分开便于测试。</summary>
    public static class Game2048InputMap
    {
        public static MoveDirection? MapKey(KeyCode key)
        {
            return key switch
            {
                KeyCode.UpArrow or KeyCode.W => MoveDirection.Up,
                KeyCode.DownArrow or KeyCode.S => MoveDirection.Down,
                KeyCode.LeftArrow or KeyCode.A => MoveDirection.Left,
                KeyCode.RightArrow or KeyCode.D => MoveDirection.Right,
                _ => null,
            };
        }
    }
}
