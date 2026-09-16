using System;
using System.Collections.Generic;
using System.Linq;

namespace DNDStrongholdApp.Models
{
    public enum OperationalGoal
    {
        Pillage,
        Capture,
        Scout,
        Raze
    }

    public enum RaidFrequencyTier
    {
        VeryRare,
        Rare,
        Occasional,
        Frequent,
        Custom
    }

    public enum TroopType
    {
        LightInfantry,
        HeavyInfantry,
        Scout,
        Divine,
        Bruiser,
        Tank,
        Cavalry,
        Flying,
        Arcane,
        Grunt
    }

    public enum MagicKind
    {
        None,
        Divine,
        Arcane
    }

    public class TroopDefinition
    {
        public TroopType Type { get; init; }
        public string Name { get; init; } = string.Empty;
        public int Strength { get; init; }
        public int HitPoints { get; init; }
        public int Stealth { get; init; }
        public MagicKind Magic { get; init; }
        public bool IsMagic => Magic != MagicKind.None;
    }

    public static class TroopCatalog
    {
        public static readonly IReadOnlyList<TroopDefinition> All = new[]
        {
            Def(TroopType.LightInfantry, "Light Infantry", 1, 1, 1),
            Def(TroopType.HeavyInfantry, "Heavy Infantry", 2, 1, 0),
            Def(TroopType.Scout, "Scout", 1, 1, 2),
            Def(TroopType.Divine, "Divine", 2, 1, 1, MagicKind.Divine),
            Def(TroopType.Bruiser, "Bruiser", 2, 2, -1),
            Def(TroopType.Tank, "Tank", 4, 2, -2),
            Def(TroopType.Cavalry, "Cavalry", 3, 1, -1),
            Def(TroopType.Flying, "Flying", 2, 1, 1),
            Def(TroopType.Arcane, "Arcane", 3, 1, 0, MagicKind.Arcane),
            Def(TroopType.Grunt, "Grunt", 1, 1, 0)
        };

        public static TroopDefinition Get(TroopType type) =>
            All.First(t => t.Type == type);

        public static string DisplayName(TroopType type) => Get(type).Name;

        public static string TagSuffix(TroopDefinition def) => def.Magic switch
        {
            MagicKind.Divine => ", divine",
            MagicKind.Arcane => ", arcane",
            _ => string.Empty
        };

        public static string ReportSuffix(TroopDefinition def) => def.Magic switch
        {
            MagicKind.Divine => " (divine)",
            MagicKind.Arcane => " (arcane)",
            _ => string.Empty
        };

        public static int Cost(TroopType type)
        {
            var def = Get(type);
            return Math.Max(1, def.Strength + def.HitPoints);
        }

        private static TroopDefinition Def(TroopType type, string name, int strength, int hp, int stealth, MagicKind magic = MagicKind.None)
        {
            return new TroopDefinition
            {
                Type = type,
                Name = name,
                Strength = strength,
                HitPoints = hp,
                Stealth = stealth,
                Magic = magic
            };
        }
    }

    public static class RaidFrequency
    {
        public static int BasePercent(RaidFrequencyTier tier, int customPercent)
        {
            return tier switch
            {
                RaidFrequencyTier.VeryRare => 3,
                RaidFrequencyTier.Rare => 5,
                RaidFrequencyTier.Occasional => 7,
                RaidFrequencyTier.Frequent => 10,
                RaidFrequencyTier.Custom => Math.Clamp(customPercent, 0, 100),
                _ => 7
            };
        }

        public static string Label(RaidFrequencyTier tier)
        {
            return tier switch
            {
                RaidFrequencyTier.VeryRare => "Very Rare",
                RaidFrequencyTier.Rare => "Rare",
                RaidFrequencyTier.Occasional => "Occasional",
                RaidFrequencyTier.Frequent => "Frequent",
                RaidFrequencyTier.Custom => "Custom",
                _ => "Occasional"
            };
        }

        public static RaidFrequencyTier FromLegacyPercent(int percent)
        {
            return percent switch
            {
                3 => RaidFrequencyTier.VeryRare,
                5 => RaidFrequencyTier.Rare,
                7 => RaidFrequencyTier.Occasional,
                10 => RaidFrequencyTier.Frequent,
                _ => RaidFrequencyTier.Custom
            };
        }
    }

    public static class FactionPower
    {
        public static string Label(int power)
        {
            return Math.Clamp(power, 0, 5) switch
            {
                0 => "Inactive",
                1 => "Weak",
                2 => "Notable",
                3 => "Strong",
                4 => "Dominant",
                5 => "Overbearing",
                _ => "Notable"
            };
        }
    }

    public class FactionOutpost
    {
        public string Name { get; set; } = "Outpost";
        public string Notes { get; set; } = string.Empty;
        public int IntelPoints { get; set; }
    }

    public class RaidTroop
    {
        public TroopType Type { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Strength { get; set; }
        public int MaxHitPoints { get; set; }
        public int HitPoints { get; set; }
        public int Stealth { get; set; }
        public MagicKind Magic { get; set; }
        public bool IsDead => HitPoints <= 0;
        public bool IsDivine => Magic == MagicKind.Divine;
        public bool IsArcane => Magic == MagicKind.Arcane;
    }

    public enum RaidEndReason
    {
        Ongoing,
        RaidersWiped,
        Escaped,
        ScoutFinished
    }

    public class RaidStatblock
    {
        public int Numbers { get; set; }
        public int Strength { get; set; }
        public int HitPoints { get; set; }
        public int Stealth { get; set; }
        public int SizeStealthPenalty { get; set; }
        public int MagicUsers { get; set; }
    }

    public class EnemyFaction
    {
        public static readonly int[] GoalWeights = { 50, 25, 15, 10 };

        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = "Bandits";
        public RaidFrequencyTier Frequency { get; set; } = RaidFrequencyTier.Occasional;
        public int CustomFrequencyPercent { get; set; } = 7;
        public int RaidFrequencyPercent { get; set; } = 7;
        public double OutpostFrequencyMultiplier { get; set; } = 2.0;
        public FactionOutpost? Outpost { get; set; }
        public int Power { get; set; } = 2;
        public int RaidSize { get; set; } = 10;
        public List<TroopType> AvailableTroops { get; set; } = DefaultBanditTroops();
        public List<OperationalGoal> GoalPriority { get; set; } = DefaultGoals();
        public List<NPC> CapturedNpcs { get; set; } = new List<NPC>();
        public Dictionary<string, string> TroopDisplayNames { get; set; } = new Dictionary<string, string>();

        public bool HasOutpost => Outpost != null && !string.IsNullOrWhiteSpace(Outpost.Name);

        public int BaseFrequencyPercent => RaidFrequency.BasePercent(Frequency, CustomFrequencyPercent);

        public int EffectiveFrequencyPercent
        {
            get
            {
                if (Power <= 0) return 0;
                double chance = BaseFrequencyPercent;
                if (HasOutpost)
                    chance *= OutpostFrequencyMultiplier;
                if (chance < 0) return 0;
                if (chance > 100) return 100;
                return (int)Math.Round(chance);
            }
        }

        public void Normalize()
        {
            GoalPriority ??= DefaultGoals();
            if (GoalPriority.Count == 0)
                GoalPriority = DefaultGoals();

            AvailableTroops ??= new List<TroopType>();
            if (AvailableTroops.Count == 0)
                AvailableTroops = DefaultBanditTroops();

            CapturedNpcs ??= new List<NPC>();
            TroopDisplayNames ??= new Dictionary<string, string>();
            if (Outpost != null && Outpost.IntelPoints < 0)
                Outpost.IntelPoints = 0;

            Power = Math.Clamp(Power, 0, 5);

            if (RaidSize < 1)
                RaidSize = 10;

            if (Frequency == RaidFrequencyTier.VeryRare && RaidFrequencyPercent != 3 && RaidFrequencyPercent > 0)
            {
                Frequency = RaidFrequency.FromLegacyPercent(RaidFrequencyPercent);
                CustomFrequencyPercent = RaidFrequencyPercent;
            }

            RaidFrequencyPercent = BaseFrequencyPercent;

            if (string.Equals(Name, "Bandits", StringComparison.OrdinalIgnoreCase))
                AvailableTroops.RemoveAll(t => t == TroopType.Divine);
        }

        public string TroopName(TroopType type)
        {
            TroopDisplayNames ??= new Dictionary<string, string>();
            string key = type.ToString();
            if (TroopDisplayNames.TryGetValue(key, out string? custom) && !string.IsNullOrWhiteSpace(custom))
                return custom.Trim();
            return TroopCatalog.Get(type).Name;
        }

        public static List<OperationalGoal> DefaultGoals()
        {
            return new List<OperationalGoal>
            {
                OperationalGoal.Pillage,
                OperationalGoal.Scout,
                OperationalGoal.Capture,
                OperationalGoal.Raze
            };
        }

        public static List<TroopType> DefaultBanditTroops()
        {
            return new List<TroopType>
            {
                TroopType.LightInfantry,
                TroopType.Scout,
                TroopType.Grunt,
                TroopType.Bruiser
            };
        }

        public static EnemyFaction CreateBandits()
        {
            var faction = new EnemyFaction
            {
                Name = "Bandits",
                Frequency = RaidFrequencyTier.Occasional,
                CustomFrequencyPercent = 7,
                OutpostFrequencyMultiplier = 2.0,
                Outpost = null,
                Power = 2,
                RaidSize = 10,
                AvailableTroops = DefaultBanditTroops(),
                GoalPriority = DefaultGoals()
            };
            faction.RaidFrequencyPercent = faction.BaseFrequencyPercent;
            return faction;
        }
    }

    public class RaidingParty
    {
        public string FactionId { get; set; } = string.Empty;
        public string FactionName { get; set; } = string.Empty;
        public List<TroopType> Troops { get; set; } = new List<TroopType>();
        public List<RaidTroop> Combatants { get; set; } = new List<RaidTroop>();
        public RaidStatblock Stats { get; set; } = new RaidStatblock();
        public OperationalGoal Goal { get; set; } = OperationalGoal.Pillage;
        public int Power { get; set; }
        public bool Surprise { get; set; }
        public int StealthBonus { get; set; }
    }

    public class RaidBattle
    {
        public RaidingParty Party { get; set; } = new RaidingParty();
        public int Round { get; set; }
        public int StartingNumbers { get; set; }
        public Dictionary<ResourceType, int> PendingLoot { get; set; } = new Dictionary<ResourceType, int>();
        public List<NPC> PendingCaptures { get; set; } = new List<NPC>();
        public int PendingIntel { get; set; }
        public double LootGoldValue { get; set; }
        public List<string> Log { get; set; } = new List<string>();
        public List<string> RoundReports { get; set; } = new List<string>();
        public bool Ended { get; set; }
        public bool Settled { get; set; }
        public RaidEndReason EndReason { get; set; } = RaidEndReason.Ongoing;
        public List<Project> CancelledProjects { get; set; } = new List<Project>();
        public List<string> KilledNpcs { get; set; } = new List<string>();
        public int CaptureGoalTicks { get; set; }
        public bool StealthSpellCast { get; set; }

        public int LivingCount => Party.Combatants.Count(t => !t.IsDead);

        public string FullReport => string.Join(Environment.NewLine, Log);
    }

    public class RaidRoundResult
    {
        public int Round { get; set; }
        public int AttackerRoll { get; set; }
        public int DefenderRoll { get; set; }
        public int AttackerTotal { get; set; }
        public int DefenderTotal { get; set; }
        public int Margin { get; set; }
        public bool AttackerWon { get; set; }
        public string Report { get; set; } = string.Empty;
        public bool Ended { get; set; }
        public RaidEndReason EndReason { get; set; }
    }

    public class DefenseSnapshot
    {
        public int BuildingDefense { get; set; }
        public int NpcCombat { get; set; }
        public int TotalDefense { get; set; }
        public int Perception { get; set; }
        public int FighterCount { get; set; }
        public int LookoutCount { get; set; }
        public int RoundOneDefense { get; set; }
        public List<string> BuildingLines { get; set; } = new List<string>();
        public List<string> Notes { get; set; } = new List<string>();
    }
}
