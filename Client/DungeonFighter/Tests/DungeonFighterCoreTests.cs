using System;
using System.Linq;
using Lyking.DungeonFighter.Core;
using Xunit;

namespace Lyking.DungeonFighter.Core.Tests
{
    /// <summary>测试专用地下城与驱动工具：怪物位置贴近玩家出生点，全程确定性。</summary>
    internal static class TestDungeon
    {
        public const float Dt = 1f / 60f;

        public static DungeonSpec CreateSingleRoom(string kind, float x, float y)
        {
            return new DungeonSpec
            {
                Title = "测试地下城",
                Rooms = new System.Collections.Generic.List<DungeonRoomSpec>
                {
                    new DungeonRoomSpec
                    {
                        Name = "试炼间",
                        Spawns = new System.Collections.Generic.List<MonsterSpawn> { new MonsterSpawn(kind, x, y, 0f) },
                    },
                },
            };
        }

        /// <summary>无干扰房间：怪物延迟 999 秒出生，永远保持待机且不会触发通关。</summary>
        public static DungeonSpec CreateQuietRoom()
        {
            return new DungeonSpec
            {
                Title = "静默测试",
                Rooms = new System.Collections.Generic.List<DungeonRoomSpec>
                {
                    new DungeonRoomSpec { Name = "静默间", Spawns = new System.Collections.Generic.List<MonsterSpawn> { new MonsterSpawn("史莱姆", 25f, 4f, 999f) } },
                },
            };
        }

        public static DungeonSpec CreateTwoRooms()
        {
            return new DungeonSpec
            {
                Title = "测试地下城",
                Rooms = new System.Collections.Generic.List<DungeonRoomSpec>
                {
                    new DungeonRoomSpec { Name = "前厅", Spawns = new System.Collections.Generic.List<MonsterSpawn> { new MonsterSpawn("史莱姆", 5f, 4f, 0f) } },
                    new DungeonRoomSpec { Name = "深处", Spawns = new System.Collections.Generic.List<MonsterSpawn> { new MonsterSpawn("史莱姆", 6f, 4f, 0f) } },
                },
            };
        }

        /// <summary>按固定步长推进指定秒数。</summary>
        public static void TickSeconds(DungeonGame game, float seconds, PlayerInputFrame input = default)
        {
            int steps = (int)MathF.Ceiling(seconds / Dt);
            for (int i = 0; i < steps; i++) game.Tick(Dt, input);
        }

        /// <summary>按一次普攻并等待动作完全结束。</summary>
        public static void PressAttack(DungeonGame game)
        {
            game.Tick(Dt, new PlayerInputFrame { Attack = true });
            int guard = 0;
            while (game.Player.State == FighterState.Attack && guard++ < 120) game.Tick(Dt, PlayerInputFrame.None);
        }

        public static void TickUntil(Func<DungeonGame, bool> condition, DungeonGame game, float maxSeconds)
        {
            int guard = (int)MathF.Ceiling(maxSeconds / Dt);
            while (guard-- > 0)
            {
                if (condition(game)) return;
                game.Tick(Dt, PlayerInputFrame.None);
            }
            throw new TimeoutException("等待条件超时。");
        }
    }

    public class DungeonFlowTests
    {
        [Fact]
        public void 初始状态为三层地下城且玩家满状态()
        {
            var game = new DungeonGame();

            Assert.Equal(3, game.RoomCount);
            Assert.Equal(0, game.RoomIndex);
            Assert.Equal("格兰之森入口", game.CurrentRoom.Name);
            Assert.Equal(GamePhase.InProgress, game.Phase);
            Assert.False(game.RoomCleared);
            Assert.Equal(100, game.Player.Health);
            Assert.Equal(100, game.Player.Mana);
            Assert.True(game.Player.IsAlive);
            Assert.Empty(game.Monsters); // 怪物按出生延迟逐步落地
        }

        [Fact]
        public void 怪物按出生延迟落地()
        {
            var game = new DungeonGame();

            TestDungeon.TickSeconds(game, 1.0f);

            Assert.Equal(3, game.Monsters.Count);
            Assert.All(game.Monsters, m => Assert.True(m.IsAlive));
        }

        [Fact]
        public void 八方向移动受场地边界约束()
        {
            var game = new DungeonGame(TestDungeon.CreateQuietRoom()); // 无怪干扰移动
            var right = new PlayerInputFrame { MoveX = 1f };
            for (int i = 0; i < 600; i++) game.Tick(TestDungeon.Dt, right);
            Assert.True(game.Player.Position.X <= game.Spec.ArenaWidth - 0.6f + 0.01f);

            var left = new PlayerInputFrame { MoveX = -1f };
            for (int i = 0; i < 600; i++) game.Tick(TestDungeon.Dt, left);
            Assert.True(game.Player.Position.X >= 0.6f - 0.01f);

            var down = new PlayerInputFrame { MoveY = -1f };
            for (int i = 0; i < 600; i++) game.Tick(TestDungeon.Dt, down);
            Assert.True(game.Player.Position.Y >= 0.6f - 0.01f);

            var up = new PlayerInputFrame { MoveY = 1f };
            for (int i = 0; i < 600; i++) game.Tick(TestDungeon.Dt, up);
            Assert.True(game.Player.Position.Y <= game.Spec.ArenaDepth - 0.6f + 0.01f);
        }

        [Fact]
        public void 朝向随横向输入翻转()
        {
            var game = new DungeonGame(TestDungeon.CreateQuietRoom());

            TestDungeon.TickSeconds(game, 0.2f, new PlayerInputFrame { MoveX = -1f });
            Assert.Equal(Facing.Left, game.Player.Facing);

            TestDungeon.TickSeconds(game, 0.2f, new PlayerInputFrame { MoveX = 1f });
            Assert.Equal(Facing.Right, game.Player.Facing);
        }

        [Fact]
        public void 胜负已定后模拟停止推进()
        {
            var game = new DungeonGame(TestDungeon.CreateSingleRoom("史莱姆", 8f, 4f)); // 出生在仇恨范围内
            game.Player.Health = 1;
            TestDungeon.TickSeconds(game, 30f);

            Assert.Equal(GamePhase.Defeat, game.Phase);
            float elapsedAtEnd = game.ElapsedSeconds;
            TestDungeon.TickSeconds(game, 2f);
            Assert.Equal(elapsedAtEnd, game.ElapsedSeconds);
        }
    }

    public class CombatTests
    {
        [Fact]
        public void 普攻在判定时刻造成伤害与击退()
        {
            var game = new DungeonGame(TestDungeon.CreateSingleRoom("史莱姆", 4.5f, 4f));
            TestDungeon.TickSeconds(game, 0.1f);
            MonsterFighter slime = game.Monsters.Single();
            int hpBefore = slime.Health;

            var attack = new PlayerInputFrame { Attack = true };
            game.Tick(TestDungeon.Dt, attack);
            float xBeforeHit = slime.Position.X;
            int guard = 0;
            while (!game.Player.AttackHitApplied && guard++ < 60)
            {
                xBeforeHit = slime.Position.X;
                game.Tick(TestDungeon.Dt, PlayerInputFrame.None);
            }
            float xAfterHit = slime.Position.X;

            Assert.Equal(hpBefore - 12, slime.Health);
            Assert.Equal(0.3f, xAfterHit - xBeforeHit, 2); // 击退 0.3（命中同帧结算）
            Assert.Equal(FighterState.Hurt, slime.State); // 硬直 0.3s 未结束
        }

        [Fact]
        public void 连击窗口内推进段数且伤害累加()
        {
            var game = new DungeonGame(TestDungeon.CreateSingleRoom("史莱姆", 4.5f, 4f));
            TestDungeon.TickSeconds(game, 0.1f);
            MonsterFighter slime = game.Monsters.Single();

            var attack = new PlayerInputFrame { Attack = true };
            game.Tick(TestDungeon.Dt, attack);
            TestDungeon.TickSeconds(game, 0.2f);
            Assert.Equal(1, game.Player.ComboIndex);
            Assert.True(game.Player.ComboWindowTimer > 0f);

            int guard = 0;
            while (game.Player.State == FighterState.Attack && guard++ < 60) game.Tick(TestDungeon.Dt, PlayerInputFrame.None);
            game.Tick(TestDungeon.Dt, attack); // 窗口内再按 → 第二段
            TestDungeon.TickSeconds(game, 0.2f);

            Assert.Equal(2, game.Player.ComboIndex);
            Assert.Equal(30 - 12 - 14, slime.Health);
        }

        [Fact]
        public void 连击窗口过期后回到第一段()
        {
            var game = new DungeonGame(TestDungeon.CreateSingleRoom("史莱姆", 40f, 4f)); // 怪物放远处避免干扰
            TestDungeon.TickSeconds(game, 0.1f);

            var attack = new PlayerInputFrame { Attack = true };
            game.Tick(TestDungeon.Dt, attack);
            TestDungeon.TickSeconds(game, 1.2f); // 超过连击窗口 0.9s

            Assert.Equal(0, game.Player.ComboIndex);
            game.Tick(TestDungeon.Dt, attack);
            Assert.Equal(1, game.Player.ComboIndex);
        }

        [Fact]
        public void 攻击动作中按键缓冲接下一段()
        {
            var game = new DungeonGame(TestDungeon.CreateSingleRoom("史莱姆", 4.5f, 4f));
            TestDungeon.TickSeconds(game, 0.1f);

            var attack = new PlayerInputFrame { Attack = true };
            game.Tick(TestDungeon.Dt, attack);
            TestDungeon.TickSeconds(game, 0.15f); // 本段已结算（0.12s），动作未结束（0.3s）
            game.Tick(TestDungeon.Dt, attack); // 应被缓冲
            TestDungeon.TickSeconds(game, 0.6f); // 第一段收招后自动接第二段并结束

            Assert.Equal(2, game.Player.ComboIndex);
            Assert.Equal(FighterState.Idle, game.Player.State);
        }

        [Fact]
        public void 上挑消耗法力进入冷却且冷却中被拒绝()
        {
            var game = new DungeonGame(TestDungeon.CreateSingleRoom("史莱姆", 4.5f, 4f));
            TestDungeon.TickSeconds(game, 0.1f);
            MonsterFighter slime = game.Monsters.Single();
            int hpBefore = slime.Health;

            game.Tick(TestDungeon.Dt, new PlayerInputFrame { SkillUpper = true });
            Assert.Equal(100 - 15, game.Player.Mana);
            Assert.True(game.Player.Uppercut.CooldownRemaining > 4.9f);
            Assert.Equal(FighterState.Attack, game.Player.State);

            TestDungeon.TickSeconds(game, 0.2f); // 判定时刻 0.16s 已过
            Assert.Equal(hpBefore - 26, slime.Health);
            Assert.Equal(1.4f, slime.StateTimer, 1); // 长硬直

            TestDungeon.TickSeconds(game, 0.5f); // 等收招
            float manaBeforeRetry = game.Player.Mana;
            game.Tick(TestDungeon.Dt, new PlayerInputFrame { SkillUpper = true });
            Assert.Equal(manaBeforeRetry + 6f * TestDungeon.Dt, game.Player.Mana, 3); // 仅自然回复，未扣技能费
            Assert.Contains(game.Events, e => e.Type == GameEventType.SkillDenied && e.Message.Contains("冷却"));
        }

        [Fact]
        public void 法力不足时烈焰波被拒绝()
        {
            var game = new DungeonGame(TestDungeon.CreateSingleRoom("史莱姆", 20f, 4f));
            TestDungeon.TickSeconds(game, 0.1f);
            game.Player.Mana = 10f;

            game.Tick(TestDungeon.Dt, new PlayerInputFrame { SkillWave = true });

            Assert.Empty(game.Projectiles);
            Assert.Equal(10f + 6f * TestDungeon.Dt, game.Player.Mana, 3); // 仅自然回复
            Assert.Contains(game.Events, e => e.Type == GameEventType.SkillDenied && e.Message.Contains("法力不足"));
        }

        [Fact]
        public void 烈焰波穿透命中直线上的多个目标()
        {
            var game = new DungeonGame(new DungeonSpec
            {
                Title = "弹道测试",
                Rooms = new System.Collections.Generic.List<DungeonRoomSpec>
                {
                    new DungeonRoomSpec
                    {
                        Name = "弹道间",
                        Spawns = new System.Collections.Generic.List<MonsterSpawn>
                        {
                            new MonsterSpawn("史莱姆", 10f, 4f, 0f),
                            new MonsterSpawn("史莱姆", 14f, 4f, 0f),
                        },
                    },
                },
            });
            TestDungeon.TickSeconds(game, 0.1f);

            game.Tick(TestDungeon.Dt, new PlayerInputFrame { SkillWave = true });
            TestDungeon.TickSeconds(game, 1.6f); // 弹道飞完全程

            Assert.All(game.Monsters, m => Assert.False(m.IsAlive));
            Assert.True(game.RoomCleared);
        }

        [Fact]
        public void 玩家跳跃期间怪物挥击落空()
        {
            var game = new DungeonGame(TestDungeon.CreateSingleRoom("史莱姆", 4f, 4f));
            TestDungeon.TickSeconds(game, 0.1f);

            TestDungeon.TickUntil(g => g.Monsters.Single().AiState == MonsterAiState.Windup, game, 5f);
            TestDungeon.TickUntil(g => g.Monsters.Single().StateTimer <= 0.1f, game, 5f);
            game.Tick(TestDungeon.Dt, new PlayerInputFrame { Jump = true });
            Assert.True(game.Player.IsAirborne);

            TestDungeon.TickSeconds(game, 1.5f);
            Assert.Equal(100, game.Player.Health);
        }

        [Fact]
        public void 受击无敌帧阻断紧随其后的第二次挥击()
        {
            // 两只哥布林同点出生、行动几乎同步：第一只命中后，第二只的挥击落在无敌帧内被阻断。
            var game = new DungeonGame(new DungeonSpec
            {
                Title = "无敌帧测试",
                Rooms = new System.Collections.Generic.List<DungeonRoomSpec>
                {
                    new DungeonRoomSpec
                    {
                        Name = "夹击间",
                        Spawns = new System.Collections.Generic.List<MonsterSpawn>
                        {
                            new MonsterSpawn("哥布林", 4.5f, 4f, 0f),
                            new MonsterSpawn("哥布林", 4.6f, 4f, 0f),
                        },
                    },
                },
            });
            TestDungeon.TickSeconds(game, 40f);

            // 非致命击每次固定 12 点；最后一击按剩余血量截断（≤11 点），因此总掉血落在 [hits*12, hits*12+11]
            int hits = game.Events.Count(e => e.Type == GameEventType.PlayerHurt);
            int totalDamage = game.Player.MaxHealth - game.Player.Health;
            Assert.InRange(totalDamage, hits * 12, hits * 12 + 11);

            // 相邻两次受伤的时间差必须超过无敌帧（否则说明无敌帧没有生效）
            var hurtTimes = game.Events.Where(e => e.Type == GameEventType.PlayerHurt).Select(e => (double)e.TimeSeconds).ToList();
            for (int i = 1; i < hurtTimes.Count; i++)
            {
                Assert.True(hurtTimes[i] - hurtTimes[i - 1] > 0.35, "两次受伤间隔小于无敌帧，无敌帧未生效");
            }

            Assert.Equal(game.Player.Health == 0 ? GamePhase.Defeat : GamePhase.InProgress, game.Phase);
        }

        [Fact]
        public void 同一输入序列两次模拟结果一致()
        {
            DungeonGame runA = PlayScriptedRun();
            DungeonGame runB = PlayScriptedRun();

            Assert.Equal(runA.Player.Position.X, runB.Player.Position.X, 3);
            Assert.Equal(runA.Player.Position.Y, runB.Player.Position.Y, 3);
            Assert.Equal(runA.Player.Health, runB.Player.Health);
            Assert.Equal(runA.Player.Mana, runB.Player.Mana, 3);
            Assert.Equal(runA.TotalKills, runB.TotalKills);
            Assert.Equal(runA.Phase, runB.Phase);
            Assert.Equal(runA.Events.Count, runB.Events.Count);
        }

        private static DungeonGame PlayScriptedRun()
        {
            var game = new DungeonGame();
            for (int i = 0; i < 60 * 40; i++)
            {
                game.Tick(TestDungeon.Dt, NextScriptedInput(i));
            }
            return game;
        }

        /// <summary>以 tick 序号驱动的确定性输入脚本（无随机数）。</summary>
        private static PlayerInputFrame NextScriptedInput(int tick)
        {
            int phase = tick % 240;
            if (phase < 60) return new PlayerInputFrame { MoveX = 1f };
            if (phase < 66) return new PlayerInputFrame { Attack = true };
            if (phase < 120) return new PlayerInputFrame { MoveY = 1f };
            if (phase < 126) return new PlayerInputFrame { SkillWave = true };
            if (phase < 180) return new PlayerInputFrame { MoveX = -1f };
            if (phase < 186) return new PlayerInputFrame { SkillUpper = true };
            if (phase < 190) return new PlayerInputFrame { Jump = true };
            return new PlayerInputFrame { MoveY = -1f };
        }
    }

    public class RoomFlowTests
    {
        [Fact]
        public void 未清空房间时走到门区不切换()
        {
            var game = new DungeonGame(TestDungeon.CreateTwoRooms());
            TestDungeon.TickSeconds(game, 0.1f);
            Assert.Single(game.Monsters);

            game.Player.Position = game.Player.Position.WithX(game.Spec.ArenaWidth - 1f);
            TestDungeon.TickSeconds(game, 0.2f);

            Assert.Equal(0, game.RoomIndex);
            Assert.False(game.RoomCleared);
        }

        [Fact]
        public void 清空房间开门并走进门区进入下一层()
        {
            var game = new DungeonGame(TestDungeon.CreateTwoRooms());
            TestDungeon.TickSeconds(game, 0.1f);

            KillRoom(game);

            Assert.True(game.RoomCleared);
            Assert.Contains(game.Events, e => e.Type == GameEventType.RoomCleared);
            Assert.Contains(game.Events, e => e.Type == GameEventType.DoorOpened);
            Assert.Equal(GamePhase.InProgress, game.Phase); // 还有第二层

            game.Player.Position = game.Player.Position.WithX(game.Spec.ArenaWidth - 1f);
            TestDungeon.TickSeconds(game, 0.2f);

            Assert.Equal(1, game.RoomIndex);
            Assert.Equal("深处", game.CurrentRoom.Name);
            Assert.False(game.RoomCleared);
            Assert.Equal(2.5f, game.Player.Position.X, 2);
            Assert.Single(game.Monsters); // 新房间的怪物已落地
            Assert.Contains(game.Events, e => e.Type == GameEventType.RoomEntered);
        }

        [Fact]
        public void 最后一层清空判定通关()
        {
            var game = new DungeonGame(TestDungeon.CreateSingleRoom("史莱姆", 5f, 4f));
            TestDungeon.TickSeconds(game, 0.1f);

            KillRoom(game);

            Assert.Equal(GamePhase.Victory, game.Phase);
            Assert.Contains(game.Events, e => e.Type == GameEventType.Victory);
        }

        /// <summary>循环普攻至房间全灭；怪物被击退后会自己走回来。每轮恢复玩家生命，保持流程测试确定性。</summary>
        private static void KillRoom(DungeonGame game)
        {
            int guard = 0;
            while (game.Phase == GamePhase.InProgress && game.Monsters.Any(m => m.IsAlive) && guard++ < 30)
            {
                game.Player.Health = game.Player.MaxHealth;
                game.Player.InvulnTimer = 0f;
                int hits = 0;
                while (game.Phase == GamePhase.InProgress && game.Monsters.Any(m => m.IsAlive) && hits++ < 16)
                {
                    TestDungeon.PressAttack(game);
                }
            }
            Assert.True(game.Monsters.All(m => !m.IsAlive), "30 轮内未能清空房间。");
        }
    }
}
