using System;
using System.Collections.Generic;

namespace Lyking.DungeonFighter.Core
{
    /// <summary>房间内一个怪物出生点。</summary>
    public sealed class MonsterSpawn
    {
        public MonsterSpawn(string kind, float x, float y, float delaySeconds)
        {
            Kind = kind;
            X = x;
            Y = y;
            DelaySeconds = delaySeconds;
        }

        /// <summary>怪物类型名，对应 <see cref="DungeonSpec.ResolveMonster"/>。</summary>
        public string Kind { get; }

        public float X { get; }

        public float Y { get; }

        /// <summary>进入房间后延迟多少秒出生。</summary>
        public float DelaySeconds { get; }
    }

    /// <summary>地下城单层房间定义。</summary>
    public sealed class DungeonRoomSpec
    {
        public string Name { get; set; } = string.Empty;

        public IReadOnlyList<MonsterSpawn> Spawns { get; set; } = new List<MonsterSpawn>();
    }

    /// <summary>一整座地下城的配置：场地尺寸、玩家配置、怪物图鉴与楼层出生表。</summary>
    public sealed class DungeonSpec
    {
        private static readonly Dictionary<string, MonsterSpec> MonsterCatalog = new Dictionary<string, MonsterSpec>();

        public string Title { get; set; } = "地下城";

        /// <summary>场地宽度（X 世界单位）。</summary>
        public float ArenaWidth { get; set; } = 30f;

        /// <summary>场地纵深（Y 世界单位）。</summary>
        public float ArenaDepth { get; set; } = 8f;

        public PlayerSpec Player { get; set; } = new PlayerSpec();

        /// <summary>楼层顺序，最后一层清空即通关。</summary>
        public IReadOnlyList<DungeonRoomSpec> Rooms { get; set; } = new List<DungeonRoomSpec>();

        /// <summary>按类型名取怪物规格，首次解析时缓存。</summary>
        public MonsterSpec ResolveMonster(string kind)
        {
            if (MonsterCatalog.TryGetValue(kind, out MonsterSpec? cached)) return cached;
            MonsterSpec created = kind switch
            {
                "史莱姆" => MonsterSpec.CreateSlime(),
                "哥布林" => MonsterSpec.CreateGoblin(),
                "哥布林之王" => MonsterSpec.CreateGoblinKing(),
                _ => throw new ArgumentException($"未知怪物类型：{kind}", nameof(kind)),
            };
            MonsterCatalog[kind] = created;
            return created;
        }

        /// <summary>默认地下城：格兰之森三层结构，史莱姆、哥布林与哥布林之王。</summary>
        public static DungeonSpec CreateDefault()
        {
            var rooms = new List<DungeonRoomSpec>
            {
                new DungeonRoomSpec
                {
                    Name = "格兰之森入口",
                    Spawns = new List<MonsterSpawn>
                    {
                        new MonsterSpawn("史莱姆", 14f, 3f, 0f),
                        new MonsterSpawn("史莱姆", 20f, 5.5f, 0.4f),
                        new MonsterSpawn("史莱姆", 25f, 2.5f, 0.8f),
                    },
                },
                new DungeonRoomSpec
                {
                    Name = "幽暗洞窟",
                    Spawns = new List<MonsterSpawn>
                    {
                        new MonsterSpawn("史莱姆", 13f, 2f, 0f),
                        new MonsterSpawn("哥布林", 18f, 6f, 0.2f),
                        new MonsterSpawn("哥布林", 23f, 3f, 0.6f),
                        new MonsterSpawn("史莱姆", 27f, 5f, 1f),
                    },
                },
                new DungeonRoomSpec
                {
                    Name = "哥布林王座",
                    Spawns = new List<MonsterSpawn>
                    {
                        new MonsterSpawn("哥布林", 14f, 5.5f, 0f),
                        new MonsterSpawn("哥布林之王", 24f, 4f, 0.5f),
                        new MonsterSpawn("哥布林", 19f, 2f, 0f),
                    },
                },
            };
            return new DungeonSpec
            {
                Title = "格兰之森的哥布林巢穴",
                Rooms = rooms,
            };
        }
    }
}
