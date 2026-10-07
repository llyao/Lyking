using System;
using System.Collections.Generic;

namespace Lyking.DungeonFighter.Core
{
    /// <summary>
    /// 地下城对局核心：固定时间步长的确定性模拟。
    /// 无 Unity 依赖、无随机数，同一输入序列必然得到同一结果；
    /// 表现层只读取状态与事件，不得回写。
    /// </summary>
    public sealed class DungeonGame
    {
        public const int EventCapacity = 24;

        // 只需阻断同一帧内的多次结算：0.35s 以上会吞掉更快的连击段
        private const float MonsterInvulnSeconds = 0.05f;

        private readonly List<MonsterFighter> _monsters = new List<MonsterFighter>();

        private readonly List<Projectile> _projectiles = new List<Projectile>();

        private readonly List<GameEvent> _events = new List<GameEvent>();

        private readonly List<MonsterSpawn> _pendingSpawns = new List<MonsterSpawn>();

        private int _nextEntityId = 2;

        public DungeonGame()
            : this(DungeonSpec.CreateDefault())
        {
        }

        public DungeonGame(DungeonSpec spec)
        {
            Spec = spec ?? throw new ArgumentNullException(nameof(spec));
            if (Spec.Rooms.Count == 0) throw new ArgumentException("地下城至少需要一个房间。", nameof(spec));
            Player = new PlayerFighter(1, Spec.Player)
            {
                Position = new Vec2(2.5f, Spec.ArenaDepth * 0.5f),
            };
            EnterRoom(0, logEntry: false);
            Log(GameEventType.RoomEntered, $"进入地下城【{Spec.Title}】第 1 层：{CurrentRoom.Name}");
        }

        public DungeonSpec Spec { get; }

        public PlayerFighter Player { get; }

        public IReadOnlyList<MonsterFighter> Monsters => _monsters;

        public IReadOnlyList<Projectile> Projectiles => _projectiles;

        public IReadOnlyList<GameEvent> Events => _events;

        /// <summary>当前楼层下标（0 起）。</summary>
        public int RoomIndex { get; private set; }

        public DungeonRoomSpec CurrentRoom => Spec.Rooms[RoomIndex];

        public int RoomCount => Spec.Rooms.Count;

        /// <summary>当前房间是否已清空（门开启）。</summary>
        public bool RoomCleared { get; private set; }

        public GamePhase Phase { get; private set; } = GamePhase.InProgress;

        public float ElapsedSeconds { get; private set; }

        public float RoomElapsedSeconds { get; private set; }

        public int TotalKills { get; private set; }

        public bool IsLastRoom => RoomIndex >= RoomCount - 1;

        /// <summary>门区左边界：房间清空后玩家 X 越过该值即进入下一层。</summary>
        public float DoorX => Spec.ArenaWidth - 1.5f;

        /// <summary>推进模拟一步；胜负已定或时间步非正时忽略。</summary>
        public void Tick(float deltaTime, in PlayerInputFrame input)
        {
            if (Phase != GamePhase.InProgress) return;
            float dt = Math.Min(deltaTime, 0.1f);
            if (dt <= 0f) return;

            ElapsedSeconds += dt;
            RoomElapsedSeconds += dt;
            UpdatePlayer(dt, input);
            UpdateMonsters(dt);
            UpdateProjectiles(dt);
            MaterializeSpawns();
            CheckRoomState();
            TryAdvanceRoom();
        }

        // ---------------------------------------------------------------- 玩家

        private void UpdatePlayer(float dt, in PlayerInputFrame input)
        {
            PlayerFighter p = Player;
            if (!p.IsAlive) return;

            p.InvulnTimer = Math.Max(0f, p.InvulnTimer - dt);
            p.ComboWindowTimer = Math.Max(0f, p.ComboWindowTimer - dt);
            p.Uppercut.CooldownRemaining = Math.Max(0f, p.Uppercut.CooldownRemaining - dt);
            p.FireWave.CooldownRemaining = Math.Max(0f, p.FireWave.CooldownRemaining - dt);
            if (p.ComboWindowTimer <= 0f && p.State is FighterState.Idle or FighterState.Move) p.ComboIndex = 0;
            p.Mana = Math.Min(p.Spec.MaxMana, p.Mana + p.Spec.ManaRegenPerSecond * dt);

            // 攻击动作中按普攻且本段已结算 → 缓冲接下一段。
            if (input.Attack && p.State == FighterState.Attack && p.AttackHitApplied) p.BufferedAttack = true;

            switch (p.State)
            {
                case FighterState.Hurt:
                    p.StateTimer -= dt;
                    if (p.StateTimer <= 0f)
                    {
                        p.State = FighterState.Idle;
                        p.StateTimer = 0f;
                    }
                    return;
                case FighterState.Attack:
                    TickPlayerAttack(dt);
                    return;
                case FighterState.Dead:
                    return;
            }

            // Idle / Move：空中只推进跳跃，不接收其他动作。
            if (p.IsJumping)
            {
                p.AirElapsed += dt;
                if (p.AirElapsed >= p.Spec.JumpDuration)
                {
                    p.IsJumping = false;
                    p.AirElapsed = 0f;
                }
                return;
            }

            if (input.Jump)
            {
                p.IsJumping = true;
                p.AirElapsed = 0f;
                return;
            }

            if (input.Attack)
            {
                StartPlayerCombo();
                return;
            }

            if (input.SkillUpper && TryCastSkill(p.Spec.Uppercut, p.Uppercut)) return;
            if (input.SkillWave && TryCastSkill(p.Spec.FireWave, p.FireWave)) return;

            float moveX = input.MoveX;
            float moveY = input.MoveY;
            float magnitude = MathF.Sqrt(moveX * moveX + moveY * moveY);
            if (magnitude > 1f)
            {
                moveX /= magnitude;
                moveY /= magnitude;
            }

            if (MathF.Abs(moveX) > 0.01f || MathF.Abs(moveY) > 0.01f)
            {
                p.Position = ClampToArena(new Vec2(
                    p.Position.X + moveX * p.MoveSpeed * dt,
                    p.Position.Y + moveY * p.MoveSpeed * dt));
                if (MathF.Abs(input.MoveX) > 0.01f) p.Facing = input.MoveX > 0f ? Facing.Right : Facing.Left;
                p.State = FighterState.Move;
            }
            else
            {
                p.State = FighterState.Idle;
            }
        }

        private void StartPlayerCombo()
        {
            PlayerFighter p = Player;
            int next = 1;
            if (p.ComboIndex >= 1 && p.ComboWindowTimer > 0f && p.ComboIndex < p.Spec.ComboSteps.Count) next = p.ComboIndex + 1;
            p.ComboIndex = next;
            BeginPlayerAttack(p.Spec.ComboSteps[next - 1], isCombo: true);
        }

        private void BeginPlayerAttack(MeleeAttackSpec attack, bool isCombo)
        {
            PlayerFighter p = Player;
            p.CurrentAttack = attack;
            p.CurrentAttackDuration = attack.Duration;
            p.CurrentAttackIsCombo = isCombo;
            p.AttackHitApplied = false;
            p.BufferedAttack = false;
            p.State = FighterState.Attack;
            p.StateTimer = attack.Duration;
            if (attack.LungeDistance > 0f)
            {
                p.Position = ClampToArena(p.Position.WithX(p.Position.X + Sign(p.Facing) * attack.LungeDistance));
            }
        }

        private void TickPlayerAttack(float dt)
        {
            PlayerFighter p = Player;
            MeleeAttackSpec attack = p.CurrentAttack!;
            p.StateTimer -= dt;

            float elapsed = p.CurrentAttackDuration - p.StateTimer;
            if (!p.AttackHitApplied && elapsed >= attack.ActiveDelay)
            {
                p.AttackHitApplied = true;
                ApplyMelee(p, attack);
                if (p.CurrentAttackIsCombo)
                {
                    p.ComboWindowTimer = p.Spec.ComboWindowSeconds;
                    Log(GameEventType.AttackHit, $"{attack.Name}命中（第 {p.ComboIndex} 段）");
                }
            }

            if (p.StateTimer > 0f || p.State != FighterState.Attack) return;
            p.State = FighterState.Idle;
            p.StateTimer = 0f;
            if (p.BufferedAttack && p.CurrentAttackIsCombo)
            {
                StartPlayerCombo();
            }
            else
            {
                p.BufferedAttack = false;
            }
        }

        /// <summary>施放技能：检查冷却与法力，被拒绝时记录事件并返回 false。</summary>
        private bool TryCastSkill(SkillSpec skill, SkillState state)
        {
            PlayerFighter p = Player;
            if (!state.IsReady)
            {
                Log(GameEventType.SkillDenied, $"【{skill.Name}】冷却中，剩余 {state.CooldownRemaining:0.0} 秒");
                return false;
            }

            if (p.Mana < skill.MpCost)
            {
                Log(GameEventType.SkillDenied, $"【{skill.Name}】法力不足（需要 {skill.MpCost}，当前 {p.Mana:0}）");
                return false;
            }

            p.Mana -= skill.MpCost;
            state.CooldownRemaining = skill.CooldownSeconds;
            Log(GameEventType.SkillUsed, $"施放【{skill.Name}】，消耗 {skill.MpCost} 法力");

            if (skill.Melee != null)
            {
                p.ComboIndex = 0;
                BeginPlayerAttack(skill.Melee, isCombo: false);
            }

            if (skill.Projectile != null)
            {
                float dir = Sign(p.Facing);
                var projectile = new Projectile(
                    _nextEntityId++,
                    FighterTeam.Player,
                    skill.Name,
                    ClampToArena(new Vec2(p.Position.X + dir * 0.9f, p.Position.Y)),
                    dir,
                    skill.Projectile);
                _projectiles.Add(projectile);
            }

            return true;
        }

        // ---------------------------------------------------------------- 怪物

        private void UpdateMonsters(float dt)
        {
            PlayerFighter player = Player;
            bool playerAlive = player.IsAlive;

            foreach (MonsterFighter m in _monsters)
            {
                m.InvulnTimer = Math.Max(0f, m.InvulnTimer - dt);
                m.AttackCooldown = Math.Max(0f, m.AttackCooldown - dt);

                if (m.State == FighterState.Dead) continue;
                if (m.State == FighterState.Hurt)
                {
                    m.StateTimer -= dt;
                    if (m.StateTimer <= 0f)
                    {
                        m.State = FighterState.Idle;
                        m.StateTimer = 0f;
                        m.AiState = playerAlive ? MonsterAiState.Chase : MonsterAiState.Idle;
                    }
                    continue;
                }

                switch (m.AiState)
                {
                    case MonsterAiState.Idle:
                        if (playerAlive
                            && MathF.Abs(player.Position.X - m.Position.X) <= m.Spec.AggroRange
                            && MathF.Abs(player.Position.Y - m.Position.Y) <= 3f)
                        {
                            m.AiState = MonsterAiState.Chase;
                        }
                        break;

                    case MonsterAiState.Chase:
                        if (!playerAlive)
                        {
                            m.AiState = MonsterAiState.Idle;
                            break;
                        }

                        float dx = player.Position.X - m.Position.X;
                        float dy = player.Position.Y - m.Position.Y;
                        m.Facing = dx >= 0f ? Facing.Right : Facing.Left;

                        if (MathF.Abs(dx) <= m.Spec.AttackRangeX
                            && MathF.Abs(dy) <= m.Spec.AttackHalfWidthY
                            && m.AttackCooldown <= 0f
                            && !player.IsAirborne)
                        {
                            m.AiState = MonsterAiState.Windup;
                            m.State = FighterState.Attack;
                            m.StateTimer = m.Spec.WindupSeconds;
                            m.AttackHitApplied = false;
                            break;
                        }

                        if (MathF.Abs(dx) > 0.05f)
                        {
                            m.Position = m.Position.WithX(ClampArenaX(m.Position.X + MathF.Sign(dx) * m.MoveSpeed * dt));
                        }

                        if (MathF.Abs(dy) > 0.05f)
                        {
                            m.Position = m.Position.WithY(ClampArenaY(m.Position.Y + MathF.Sign(dy) * m.MoveSpeed * 0.7f * dt));
                        }

                        // 同伴简单分离，避免完全重叠成一团。
                        foreach (MonsterFighter other in _monsters)
                        {
                            if (ReferenceEquals(other, m) || !other.IsAlive) continue;
                            float ox = m.Position.X - other.Position.X;
                            float oy = m.Position.Y - other.Position.Y;
                            if (MathF.Abs(ox) < 0.9f && MathF.Abs(oy) < 0.6f && MathF.Abs(ox) > 0.001f)
                            {
                                m.Position = m.Position.WithX(ClampArenaX(m.Position.X + MathF.Sign(ox) * 0.8f * dt));
                            }
                        }
                        break;

                    case MonsterAiState.Windup:
                        m.StateTimer -= dt;
                        if (playerAlive)
                        {
                            m.Facing = player.Position.X >= m.Position.X ? Facing.Right : Facing.Left;
                            // 前摇期间小幅逼近，确保挥击距离够得着。
                            float wdx = player.Position.X - m.Position.X;
                            if (MathF.Abs(wdx) > 0.1f)
                            {
                                m.Position = m.Position.WithX(ClampArenaX(m.Position.X + MathF.Sign(wdx) * m.MoveSpeed * 0.35f * dt));
                            }
                        }
                        if (m.StateTimer <= 0f) MonsterStrike(m);
                        break;

                    case MonsterAiState.Recover:
                        m.StateTimer -= dt;
                        if (m.StateTimer <= 0f)
                        {
                            m.AiState = playerAlive ? MonsterAiState.Chase : MonsterAiState.Idle;
                            m.State = FighterState.Idle;
                        }
                        break;
                }
            }
        }

        private void MonsterStrike(MonsterFighter m)
        {
            m.State = FighterState.Idle;
            m.AiState = MonsterAiState.Recover;
            m.StateTimer = m.Spec.RecoverSeconds;
            m.AttackCooldown = m.Spec.AttackCooldownSeconds;
            m.AttackHitApplied = true;

            PlayerFighter p = Player;
            if (!p.IsAlive || p.IsAirborne || p.InvulnTimer > 0f) return;

            float dir = Sign(m.Facing);
            float dx = (p.Position.X - m.Position.X) * dir;
            if (dx < -0.3f || dx > m.Spec.MeleeReach) return;
            if (MathF.Abs(p.Position.Y - m.Position.Y) > m.Spec.AttackHalfWidthY) return;

            float knockDir = MathF.Abs(p.Position.X - m.Position.X) > 0.01f
                ? MathF.Sign(p.Position.X - m.Position.X)
                : dir;
            Damage(p, m.Spec.Damage, knockDir * 0.5f, 0.25f, m);
        }

        // ---------------------------------------------------------------- 弹道

        private void UpdateProjectiles(float dt)
        {
            for (int i = _projectiles.Count - 1; i >= 0; i--)
            {
                Projectile proj = _projectiles[i];
                if (proj.Expired)
                {
                    _projectiles.RemoveAt(i);
                    continue;
                }

                float step = proj.Spec.Speed * dt;
                proj.Position = proj.Position.WithX(proj.Position.X + proj.DirectionX * step);
                proj.Traveled += step;

                bool remove = proj.Traveled >= proj.Spec.MaxDistance
                    || proj.Position.X <= 0.3f
                    || proj.Position.X >= Spec.ArenaWidth - 0.3f;

                if (!remove && proj.Team == FighterTeam.Player)
                {
                    foreach (MonsterFighter m in _monsters)
                    {
                        if (!m.IsAlive || m.IsAirborne || m.InvulnTimer > 0f || proj.HasHit(m.Id)) continue;
                        if (MathF.Abs(m.Position.X - proj.Position.X) > 0.7f) continue;
                        if (MathF.Abs(m.Position.Y - proj.Position.Y) > proj.Spec.HalfWidthY) continue;

                        proj.MarkHit(m.Id);
                        float knockDir = proj.DirectionX >= 0f ? 1f : -1f;
                        Damage(m, proj.Spec.Damage, knockDir * proj.Spec.Knockback, proj.Spec.StunSeconds, Player);
                        if (!proj.Spec.Pierces)
                        {
                            remove = true;
                            break;
                        }
                    }
                }

                if (remove) _projectiles.RemoveAt(i);
            }
        }

        // ---------------------------------------------------------------- 判定与结算

        private void ApplyMelee(Fighter attacker, MeleeAttackSpec attack)
        {
            float dir = Sign(attacker.Facing);
            foreach (Fighter target in TargetsOf(attacker.Team))
            {
                if (!target.IsAlive || target.IsAirborne || target.InvulnTimer > 0f) continue;

                float dx = (target.Position.X - attacker.Position.X) * dir;
                if (dx < -0.45f || dx > attack.Reach) continue;
                if (MathF.Abs(target.Position.Y - attacker.Position.Y) > attack.HalfWidthY) continue;

                float knockDir = MathF.Abs(target.Position.X - attacker.Position.X) > 0.01f
                    ? MathF.Sign(target.Position.X - attacker.Position.X)
                    : dir;
                Damage(target, attack.Damage, knockDir * attack.Knockback, attack.StunSeconds, attacker);
            }
        }

        /// <summary>伤害结算：无敌帧内无效，造成扣血、击退、硬直与死亡判定。</summary>
        private void Damage(Fighter target, int amount, float knockbackX, float stunSeconds, Fighter source)
        {
            if (Phase != GamePhase.InProgress) return;
            if (!target.IsAlive || target.InvulnTimer > 0f) return;

            int applied = Math.Min(amount, target.Health);
            target.Health -= applied;

            float invulnSeconds = target is PlayerFighter playerTarget ? playerTarget.Spec.HurtInvulnSeconds : MonsterInvulnSeconds;
            target.InvulnTimer = invulnSeconds;
            target.Position = target.Position.WithX(ClampArenaX(target.Position.X + knockbackX));

            if (target.Health <= 0)
            {
                target.Health = 0;
                target.State = FighterState.Dead;
                target.StateTimer = 0f;
                if (target is MonsterFighter deadMonster)
                {
                    TotalKills++;
                    Log(GameEventType.MonsterKilled, $"击杀 {deadMonster.DisplayName}");
                }
                else
                {
                    Phase = GamePhase.Defeat;
                    Log(GameEventType.PlayerDied, $"{target.DisplayName} 倒下，挑战失败……按 R 重新开始");
                }
                return;
            }

            target.State = FighterState.Hurt;
            target.StateTimer = Math.Max(target.StateTimer, stunSeconds);
            if (target is MonsterFighter interrupted && interrupted.AiState is MonsterAiState.Windup or MonsterAiState.Recover)
            {
                interrupted.AiState = MonsterAiState.Chase;
            }

            if (target is PlayerFighter)
            {
                Log(GameEventType.PlayerHurt, $"受到 {(source is MonsterFighter monster ? monster.DisplayName : "未知")} 的 {applied} 点伤害（剩余 {target.Health}）");
            }
        }

        private IEnumerable<Fighter> TargetsOf(FighterTeam attackerTeam)
        {
            if (attackerTeam == FighterTeam.Player)
            {
                foreach (MonsterFighter m in _monsters)
                {
                    if (m.IsAlive) yield return m;
                }
            }
            else if (Player.IsAlive)
            {
                yield return Player;
            }
        }

        // ---------------------------------------------------------------- 房间流程

        private void MaterializeSpawns()
        {
            if (_pendingSpawns.Count == 0) return;
            for (int i = _pendingSpawns.Count - 1; i >= 0; i--)
            {
                MonsterSpawn spawn = _pendingSpawns[i];
                if (RoomElapsedSeconds + 1e-4f < spawn.DelaySeconds) continue;
                MonsterSpec spec = Spec.ResolveMonster(spawn.Kind);
                var monster = new MonsterFighter(_nextEntityId++, spec, ClampToArena(new Vec2(spawn.X, spawn.Y)));
                _monsters.Add(monster);
                _pendingSpawns.RemoveAt(i);
            }
        }

        private void CheckRoomState()
        {
            if (Phase != GamePhase.InProgress || RoomCleared) return;
            if (_pendingSpawns.Count > 0) return;
            foreach (MonsterFighter m in _monsters)
            {
                if (m.IsAlive) return;
            }

            RoomCleared = true;
            Log(GameEventType.RoomCleared, $"【{CurrentRoom.Name}】清空");
            if (IsLastRoom)
            {
                Phase = GamePhase.Victory;
                Log(GameEventType.Victory, $"击败{Spec.Title}的守关 Boss，地下城通关！按 R 重新开始");
            }
            else
            {
                Log(GameEventType.DoorOpened, "右侧传送门开启，走进门区进入下一层");
            }
        }

        private void TryAdvanceRoom()
        {
            if (Phase != GamePhase.InProgress || !RoomCleared || IsLastRoom) return;
            if (Player.Position.X < DoorX) return;
            EnterRoom(RoomIndex + 1, logEntry: true);
        }

        private void EnterRoom(int index, bool logEntry)
        {
            RoomIndex = index;
            RoomElapsedSeconds = 0f;
            RoomCleared = false;
            _monsters.Clear();
            _projectiles.Clear();
            _pendingSpawns.Clear();
            _pendingSpawns.AddRange(CurrentRoom.Spawns);

            PlayerFighter p = Player;
            p.Position = new Vec2(2.5f, Spec.ArenaDepth * 0.5f);
            p.State = FighterState.Idle;
            p.StateTimer = 0f;
            p.CurrentAttack = null;
            p.BufferedAttack = false;
            p.AttackHitApplied = false;
            p.IsJumping = false;
            p.AirElapsed = 0f;

            if (logEntry) Log(GameEventType.RoomEntered, $"进入第 {index + 1} 层：{CurrentRoom.Name}");
        }

        // ---------------------------------------------------------------- 工具

        public Vec2 ClampToArena(Vec2 position) => new Vec2(ClampArenaX(position.X), ClampArenaY(position.Y));

        public float ClampArenaX(float x) => Math.Clamp(x, 0.6f, Spec.ArenaWidth - 0.6f);

        public float ClampArenaY(float y) => Math.Clamp(y, 0.6f, Spec.ArenaDepth - 0.6f);

        private static float Sign(Facing facing) => (float)facing;

        private void Log(GameEventType type, string message)
        {
            _events.Add(new GameEvent(type, message, ElapsedSeconds));
            if (_events.Count > EventCapacity) _events.RemoveRange(0, _events.Count - EventCapacity);
        }
    }
}
