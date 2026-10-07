using System.Collections.Generic;
using Lyking.DungeonFighter.Core;
using UnityEngine;

namespace Lyking.DungeonFighter.Unity
{
    /// <summary>
    /// IMGUI 战场与 HUD：把核心只读状态绘制为色块、血条、冷却与日志。
    /// 只订阅状态，不回写任何核心数据。
    /// </summary>
    public sealed class DungeonFighterPanel : MonoBehaviour
    {
        private static readonly Color FloorColor = new Color(0.11f, 0.13f, 0.17f);
        private static readonly Color FloorLineColor = new Color(0.16f, 0.19f, 0.24f);
        private static readonly Color BorderColor = new Color(0.38f, 0.41f, 0.48f);
        private static readonly Color PlayerColor = new Color(0.30f, 0.64f, 1f);
        private static readonly Color PlayerAttackColor = new Color(0.78f, 0.92f, 1f);
        private static readonly Color SlashColor = new Color(1f, 1f, 1f, 0.32f);
        private static readonly Color SlimeColor = new Color(0.36f, 0.80f, 0.45f);
        private static readonly Color GoblinColor = new Color(0.85f, 0.48f, 0.30f);
        private static readonly Color BossColor = new Color(0.72f, 0.36f, 0.86f);
        private static readonly Color WindupColor = new Color(1f, 0.85f, 0.30f, 0.85f);
        private static readonly Color ProjectileColor = new Color(1f, 0.62f, 0.20f);
        private static readonly Color DoorOpenColor = new Color(0.40f, 1f, 0.60f);
        private static readonly Color DoorClosedColor = new Color(0.42f, 0.44f, 0.50f);
        private static readonly Color BarBackColor = new Color(0.22f, 0.22f, 0.26f);
        private static readonly Color HpBarColor = new Color(0.90f, 0.28f, 0.28f);
        private static readonly Color MpBarColor = new Color(0.32f, 0.52f, 0.95f);
        private static readonly Color TextColor = new Color(0.92f, 0.94f, 0.97f);
        private static readonly Color DimTextColor = new Color(0.65f, 0.69f, 0.76f);

        private DungeonFighterBootstrap? _bootstrap;

        public void Bind(DungeonFighterBootstrap bootstrap) => _bootstrap = bootstrap;

        private void OnGUI()
        {
            var bootstrap = _bootstrap;
            if (bootstrap == null || !bootstrap.HasGame) return;
            DungeonGame game = bootstrap.Game;

            DrawHud(game);
            DrawWorld(game);
            DrawLog(game);
            DrawBanner(game);
        }

        // ---------------------------------------------------------------- HUD

        private void DrawHud(DungeonGame game)
        {
            float width = Screen.width;
            string phaseText = game.Phase switch
            {
                GamePhase.Victory => "已通关",
                GamePhase.Defeat => "已失败",
                _ => "进行中",
            };
            GUI.Label(new Rect(16, 8, width - 32, 24), $"{game.Spec.Title} · 第 {game.RoomIndex + 1}/{game.RoomCount} 层 {game.CurrentRoom.Name} · {phaseText}", Label(16, TextColor));

            Bar(new Rect(16, 36, 320, 16), (float)game.Player.Health / game.Player.MaxHealth, HpBarColor);
            GUI.Label(new Rect(344, 36, 220, 16), $"HP {game.Player.Health}/{game.Player.MaxHealth}", Label(13, TextColor));

            Bar(new Rect(16, 58, 320, 12), game.Player.Mana / game.Player.Spec.MaxMana, MpBarColor);
            GUI.Label(new Rect(344, 56, 220, 16), $"MP {game.Player.Mana:0}/{game.Player.Spec.MaxMana}", Label(13, DimTextColor));

            string combo = game.Player.ComboIndex > 0 ? $"连击 {game.Player.ComboIndex}/3" : "连击 -";
            GUI.Label(new Rect(16, 76, 400, 18), combo, Label(13, game.Player.ComboIndex >= 3 ? WindupColor : DimTextColor));

            string upper = game.Player.Uppercut.IsReady
                ? "[K] 上挑 就绪 15MP"
                : $"[K] 上挑 冷却 {game.Player.Uppercut.CooldownRemaining:0.0}s";
            string wave = game.Player.FireWave.IsReady
                ? "[L] 烈焰波 就绪 25MP"
                : $"[L] 烈焰波 冷却 {game.Player.FireWave.CooldownRemaining:0.0}s";
            GUI.Label(new Rect(200, 76, 420, 18), upper, Label(13, game.Player.Uppercut.IsReady ? TextColor : DimTextColor));
            GUI.Label(new Rect(480, 76, 420, 18), wave, Label(13, game.Player.FireWave.IsReady ? TextColor : DimTextColor));

            GUI.Label(new Rect(16, 96, width - 32, 18), "WASD/方向键 移动 · Space 跳跃（空中躲攻击） · J 普攻三连 · K 上挑 · L 烈焰波 · R 重开", Label(12, DimTextColor));
        }

        // ---------------------------------------------------------------- 战场

        private void DrawWorld(DungeonGame game)
        {
            var area = new Rect(16, 122, Screen.width - 32, Screen.height - 300);
            Box(area, FloorColor);

            float xScale = area.width / game.Spec.ArenaWidth;
            float yScale = area.height / game.Spec.ArenaDepth;

            // 纵深参考线
            var line = new Color(FloorLineColor.r, FloorLineColor.g, FloorLineColor.b, 0.7f);
            for (int y = 1; y < (int)game.Spec.ArenaDepth; y++)
            {
                float ly = ToScreenY(area, game, y, 0f);
                Box(new Rect(area.x, ly - 0.5f, area.width, 1f), line);
            }

            // 场地边框
            Box(new Rect(area.x, area.y, area.width, 2f), BorderColor);
            Box(new Rect(area.x, area.yMax - 2f, area.width, 2f), BorderColor);
            Box(new Rect(area.x, area.y, 2f, area.height), BorderColor);
            Box(new Rect(area.xMax - 2f, area.y, 2f, area.height), BorderColor);

            // 传送门：清空后变绿
            float doorX = ToScreenX(area, game, game.DoorX + 0.75f);
            var doorRect = new Rect(doorX - 7f, area.y + area.height * 0.12f, 14f, area.height * 0.76f);
            Box(doorRect, game.RoomCleared ? DoorOpenColor : DoorClosedColor);
            if (game.RoomCleared && !game.IsLastRoom)
            {
                GUI.Label(new Rect(doorX - 60f, area.y + 8f, 120f, 18f), "门已开启 →", Label(12, DoorOpenColor, TextAnchor.MiddleCenter));
            }

            // 单位按纵深排序绘制（远的先画）
            var units = new List<Fighter>();
            foreach (MonsterFighter m in game.Monsters)
            {
                if (m.IsAlive) units.Add(m);
            }
            if (game.Player.IsAlive) units.Add(game.Player);
            units.Sort((a, b) => a.Position.Y.CompareTo(b.Position.Y));

            foreach (Fighter unit in units)
            {
                DrawFighter(area, game, unit, xScale, yScale);
            }

            // 弹道
            foreach (Projectile proj in game.Projectiles)
            {
                float px = ToScreenX(area, game, proj.Position.X);
                float py = ToScreenY(area, game, proj.Position.Y, 0f);
                Box(new Rect(px - 0.4f * xScale, py - 0.14f * yScale, 0.8f * xScale, 0.28f * yScale), ProjectileColor);
            }

            GUI.Label(new Rect(area.x + 10f, area.y + 6f, 420f, 20f), $"【{game.CurrentRoom.Name}】剩余怪物 {AliveMonsters(game)}", Label(13, DimTextColor));
        }

        private void DrawFighter(Rect area, DungeonGame game, Fighter unit, float xScale, float yScale)
        {
            float bodyW;
            float bodyH;
            Color color;
            if (unit is PlayerFighter player)
            {
                bodyW = 1.1f;
                bodyH = 1.7f;
                color = player.State == FighterState.Attack ? PlayerAttackColor : PlayerColor;
            }
            else if (unit is MonsterFighter monster)
            {
                bodyW = monster.Spec.BodyWidth;
                bodyH = monster.Spec.BodyHeight;
                color = MonsterColor(monster);
            }
            else
            {
                return;
            }

            float airHeight = unit is PlayerFighter jumper ? jumper.AirHeight : 0f;
            float cx = ToScreenX(area, game, unit.Position.X);
            float footY = ToScreenY(area, game, unit.Position.Y, airHeight);
            var body = new Rect(cx - bodyW * xScale * 0.5f, footY - bodyH * yScale, bodyW * xScale, bodyH * yScale);

            // 怪物前摇预警圈
            if (unit is MonsterFighter telegraphed && telegraphed.AiState == MonsterAiState.Windup)
            {
                var warn = new Rect(body.x - 3f, body.y - 3f, body.width + 6f, body.height + 6f);
                Box(warn, WindupColor);
            }

            Box(body, color);

            // 朝向缺口
            var notch = unit.Facing == Facing.Right
                ? new Rect(body.xMax - 5f, body.y + body.height * 0.34f, 5f, body.height * 0.3f)
                : new Rect(body.x, body.y + body.height * 0.34f, 5f, body.height * 0.3f);
            Box(notch, new Color(1f, 1f, 1f, 0.85f));

            // 玩家出招判定区提示
            if (unit is PlayerFighter attacker && attacker.State == FighterState.Attack
                && !attacker.AttackHitApplied && attacker.CurrentAttack != null)
            {
                MeleeAttackSpec attack = attacker.CurrentAttack;
                float front = ToScreenX(area, game, attacker.Position.X + Sign(attacker.Facing) * attack.Reach);
                float back = ToScreenX(area, game, attacker.Position.X);
                var slash = new Rect(
                    Mathf.Min(front, back),
                    ToScreenY(area, game, attacker.Position.Y + attack.HalfWidthY * 0.5f, 0f),
                    Mathf.Abs(front - back),
                    attack.HalfWidthY * yScale);
                Box(slash, SlashColor);
            }

            // 怪物头顶血条
            if (unit is MonsterFighter barMonster)
            {
                var barBack = new Rect(body.x, body.y - 9f, body.width, 6f);
                Box(barBack, BarBackColor);
                float fraction = Mathf.Clamp01((float)barMonster.Health / barMonster.MaxHealth);
                Box(new Rect(barBack.x + 1f, barBack.y + 1f, Mathf.Max(0f, (barBack.width - 2f) * fraction), 4f), HpBarColor);
            }
        }

        private static Color MonsterColor(MonsterFighter monster) => monster.Spec.Kind switch
        {
            "史莱姆" => SlimeColor,
            "哥布林" => GoblinColor,
            "哥布林之王" => BossColor,
            _ => TextColor,
        };

        // ---------------------------------------------------------------- 日志与横幅

        private void DrawLog(DungeonGame game)
        {
            var area = new Rect(16, Screen.height - 168, 460, 160);
            GUI.Label(new Rect(area.x, area.y, area.width, 18), "战斗日志", Label(13, DimTextColor));
            var events = game.Events;
            int start = Mathf.Max(0, events.Count - 8);
            for (int i = start; i < events.Count; i++)
            {
                GUI.Label(
                    new Rect(area.x, area.y + 20f + (i - start) * 17f, area.width, 17f),
                    $"[{events[i].TimeSeconds:0.0}s] {events[i].Message}",
                    Label(12, TextColor));
            }
        }

        private void DrawBanner(DungeonGame game)
        {
            if (game.Phase == GamePhase.InProgress) return;
            bool victory = game.Phase == GamePhase.Victory;
            Color color = victory ? DoorOpenColor : HpBarColor;
            string title = victory ? "通关胜利！" : "挑战失败……";
            float cx = Screen.width * 0.5f;
            float cy = Screen.height * 0.35f;
            GUI.Label(new Rect(cx - 240f, cy - 30f, 480f, 60f), title, Label(42, color, TextAnchor.MiddleCenter));
            GUI.Label(new Rect(cx - 240f, cy + 34f, 480f, 24f), $"击杀 {game.TotalKills} · 用时 {game.ElapsedSeconds:0.0} 秒 · 按 R 重新开始", Label(15, TextColor, TextAnchor.MiddleCenter));
        }

        // ---------------------------------------------------------------- 工具

        private static int AliveMonsters(DungeonGame game)
        {
            int count = 0;
            foreach (MonsterFighter m in game.Monsters)
            {
                if (m.IsAlive) count++;
            }
            return count;
        }

        private static float ToScreenX(Rect area, DungeonGame game, float worldX)
        {
            return area.x + worldX / game.Spec.ArenaWidth * area.width;
        }

        private static float ToScreenY(Rect area, DungeonGame game, float worldY, float airHeight)
        {
            float depthScale = area.height / game.Spec.ArenaDepth;
            return area.yMax - worldY * depthScale - airHeight * depthScale;
        }

        private static float Sign(Facing facing) => (float)facing;

        private static void Box(Rect rect, Color color)
        {
            var previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
        }

        private static void Bar(Rect rect, float fraction, Color fill)
        {
            Box(rect, BarBackColor);
            float width = Mathf.Max(0f, (rect.width - 2f) * Mathf.Clamp01(fraction));
            Box(new Rect(rect.x + 1f, rect.y + 1f, width, rect.height - 2f), fill);
        }

        private static GUIStyle Label(int size, Color color, TextAnchor anchor = TextAnchor.UpperLeft)
        {
            return new GUIStyle(GUI.skin.label)
            {
                fontSize = size,
                alignment = anchor,
                normal = new GUIStyleState { textColor = color },
            };
        }
    }
}
