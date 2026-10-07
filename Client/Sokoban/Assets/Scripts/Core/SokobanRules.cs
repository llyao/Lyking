using System;
using System.Collections.Generic;

namespace Lyking.Sokoban.Core
{
    /// <summary>
    /// 推箱子规则表：内置 5 个经典关卡，关卡字符串行从上到下书写。
    /// 也可传入自定义关卡构建规则表，供测试与扩展使用；全部关卡在构造时完成解析校验。
    /// </summary>
    public sealed class SokobanRules
    {
        private static readonly string[][] BuiltInMaps =
        {
            new[] // 第一关：一推过关，用于入门与最小验证。
            {
                "#######",
                "#     #",
                "# @$. #",
                "#     #",
                "#######",
            },
            new[] // 第二关：绕到箱子上方向下推入目标。
            {
                "########",
                "#      #",
                "#  @$  #",
                "#  #   #",
                "#   .  #",
                "########",
            },
            new[] // 第三关：两个箱子各推一格入位。
            {
                "#########",
                "#       #",
                "#  $ $  #",
                "#  . .  #",
                "#   @   #",
                "#       #",
                "#########",
            },
            new[] // 第四关：绕过回廊把箱子推到拐角目标。
            {
                "########",
                "#  #   #",
                "#  $   #",
                "#  #@#.#",
                "#  #   #",
                "#      #",
                "########",
            },
            new[] // 第五关：三个箱子依次向下推入一排目标。
            {
                "##########",
                "#        #",
                "#  $$$   #",
                "#  ...   #",
                "#   @    #",
                "#        #",
                "##########",
            },
        };

        private static readonly string[] BuiltInNames = { "初试身手", "转角", "双子", "回廊", "仓库大厅" };

        /// <summary>默认规则表：内置 5 关。</summary>
        public static readonly SokobanRules Default = new SokobanRules();

        private readonly List<SokobanLevel> _levels = new List<SokobanLevel>();

        /// <summary>以内置 5 关构建规则表。</summary>
        public SokobanRules() : this(BuiltInMaps, BuiltInNames)
        {
        }

        /// <summary>以给定关卡构建规则表；名称缺省时按“第 N 关”编号。</summary>
        public SokobanRules(IReadOnlyList<string[]> maps, IReadOnlyList<string>? names = null)
        {
            if (maps is null || maps.Count == 0) throw new ArgumentException("至少需要一关。", nameof(maps));

            for (int i = 0; i < maps.Count; i++)
            {
                string name = names is not null && i < names.Count ? names[i] : $"第 {i + 1} 关";
                _levels.Add(new SokobanLevel(name, maps[i]));
            }
        }

        public IReadOnlyList<SokobanLevel> Levels => _levels;

        public int LevelCount => _levels.Count;
    }
}
