using System;
using System.Collections.Generic;
using System.Linq;
using DNDStrongholdApp.Models;

namespace DNDStrongholdApp.Services
{
    // Every rule for NPC traits lives here: how rare they are, which ones contradict each
    // other, and how much each one actually shifts. Callers in the models and other services
    // only ask for a modifier, so the numbers below are the single place to tune them.
    public static class TraitService
    {
        // Rarity. Most of the stronghold is unremarkable; a few people stand out, and it is
        // rarer still to be doubly remarkable.
        public const int TraitChancePercent = 20;       // chance an NPC has any trait at all
        public const int SecondTraitChancePercent = 15; // chance, given one, of also having a second

        // Learning
        private const double QuickLearnerMultiplier = 1.5;
        private const double SlowLearnerMultiplier = 0.5;

        // Injury severity: a lower roll is a better outcome, so Hardy shifts down.
        private const int ToughnessRollShift = 10;

        // Physical output
        private const decimal StrongMultiplier = 1.25m;
        private const decimal WeakMultiplier = 0.75m;

        // Social
        private const int CharismaticSkillBonus = 1;
        private const int ShySkillPenalty = -1;

        // Traits that cannot sensibly coexist on the same person.
        private static readonly Dictionary<NPCTraitType, NPCTraitType> Opposites = new()
        {
            { NPCTraitType.Frail, NPCTraitType.Hardy },
            { NPCTraitType.Hardy, NPCTraitType.Frail },
            { NPCTraitType.QuickLearner, NPCTraitType.SlowLearner },
            { NPCTraitType.SlowLearner, NPCTraitType.QuickLearner },
            { NPCTraitType.Strong, NPCTraitType.Weak },
            { NPCTraitType.Weak, NPCTraitType.Strong },
            { NPCTraitType.Charismatic, NPCTraitType.Shy },
            { NPCTraitType.Shy, NPCTraitType.Charismatic },
            { NPCTraitType.Expensive, NPCTraitType.Charitable },
            { NPCTraitType.Charitable, NPCTraitType.Expensive },
            { NPCTraitType.Glutton, NPCTraitType.Nibbler },
            { NPCTraitType.Nibbler, NPCTraitType.Glutton }
        };

        // Skills where raw physical power matters, for Strong and Weak.
        private static readonly HashSet<string> PhysicalSkills = new(StringComparer.OrdinalIgnoreCase)
        {
            "Labor", "Construction", "Farming", "Mining", "Stoneworking", "Smithing"
        };

        // Skills where bearing and personality matter, for Charismatic and Shy.
        private static readonly HashSet<string> SocialSkills = new(StringComparer.OrdinalIgnoreCase)
        {
            "Trade", "Connections"
        };

        public static NPCTraitType? Opposite(NPCTraitType trait)
        {
            return Opposites.TryGetValue(trait, out var opposite) ? opposite : null;
        }

        public static bool AreOpposed(NPCTraitType a, NPCTraitType b)
        {
            return Opposites.TryGetValue(a, out var opposite) && opposite == b;
        }

        // Returns the first contradictory pair in the set, or null when the set is coherent.
        public static (NPCTraitType First, NPCTraitType Second)? FindConflict(IEnumerable<NPCTraitType> traits)
        {
            var list = traits.Distinct().ToList();
            for (int i = 0; i < list.Count; i++)
            {
                for (int j = i + 1; j < list.Count; j++)
                {
                    if (AreOpposed(list[i], list[j]))
                        return (list[i], list[j]);
                }
            }
            return null;
        }

        public static bool IsPhysicalSkill(string skillName) =>
            !string.IsNullOrEmpty(skillName) && PhysicalSkills.Contains(skillName);

        public static bool IsSocialSkill(string skillName) =>
            !string.IsNullOrEmpty(skillName) && SocialSkills.Contains(skillName);

        // Rolls traits for an NPC the game generated. Leaves an NPC who already has traits
        // alone so it is safe to call more than once and never overwrites DM-authored values.
        public static void AssignRandomTraits(NPC npc)
        {
            if (npc == null || npc.Traits.Count > 0) return;

            if (Random.Shared.Next(100) >= TraitChancePercent) return;

            var pool = Enum.GetValues<NPCTraitType>().ToList();
            var first = pool[Random.Shared.Next(pool.Count)];
            npc.Traits.Add(new NPCTrait { Type = first });

            if (Random.Shared.Next(100) < SecondTraitChancePercent)
            {
                var opposite = Opposite(first);
                var candidates = pool
                    .Where(t => t != first && (opposite == null || t != opposite.Value))
                    .ToList();
                if (candidates.Count > 0)
                {
                    var second = candidates[Random.Shared.Next(candidates.Count)];
                    npc.Traits.Add(new NPCTrait { Type = second });
                }
            }

            // Expensive, Glutton, Charitable and Nibbler change what this NPC costs.
            npc.UpdateUpkeepCosts();
        }

        // QuickLearner / SlowLearner. Any positive award stays at least 1 so a slow learner
        // still creeps forward rather than stalling forever.
        public static int ModifyExperienceGain(NPC npc, int amount)
        {
            if (npc == null || amount <= 0) return amount;

            double multiplier = 1.0;
            if (npc.HasTrait(NPCTraitType.QuickLearner)) multiplier *= QuickLearnerMultiplier;
            if (npc.HasTrait(NPCTraitType.SlowLearner)) multiplier *= SlowLearnerMultiplier;
            if (Math.Abs(multiplier - 1.0) < 0.0001) return amount;

            return Math.Max(1, (int)Math.Round(amount * multiplier));
        }

        // Frail / Hardy against a d100 where a low roll is a better outcome.
        public static int ModifyInjuryRoll(NPC npc, int roll)
        {
            if (npc == null) return roll;

            if (npc.HasTrait(NPCTraitType.Hardy)) roll -= ToughnessRollShift;
            if (npc.HasTrait(NPCTraitType.Frail)) roll += ToughnessRollShift;
            return Math.Clamp(roll, 0, 99);
        }

        // Frail / Hardy against the weekly recovery check, where a low roll recovers.
        // Scaled rather than shifted by a flat amount: a flat penalty on the low grave-injury
        // odds would drop Frail to zero and leave them incurable without magic.
        private const double HardyRecoveryMultiplier = 1.5;
        private const double FrailRecoveryMultiplier = 0.5;
        private const int MinRecoveryChance = 3; // natural recovery is never completely off the table

        public static int RecoveryThreshold(NPC npc, int baseThreshold)
        {
            if (npc == null) return baseThreshold;

            double threshold = baseThreshold;
            if (npc.HasTrait(NPCTraitType.Hardy)) threshold *= HardyRecoveryMultiplier;
            if (npc.HasTrait(NPCTraitType.Frail)) threshold *= FrailRecoveryMultiplier;

            int rounded = (int)Math.Round(threshold);
            if (baseThreshold > 0) rounded = Math.Max(MinRecoveryChance, rounded);
            return Math.Clamp(rounded, 0, 100);
        }

        public static int DeathThreshold(NPC npc, int recoveryThreshold, int baseWindow)
        {
            if (npc == null) return recoveryThreshold + baseWindow;

            int window = baseWindow;
            if (npc.HasTrait(NPCTraitType.Hardy)) window = Math.Max(1, window - 2);
            if (npc.HasTrait(NPCTraitType.Frail)) window += 3;
            return Math.Clamp(recoveryThreshold + window, 0, 100);
        }

        // Strong / Weak, for production that comes from a physical skill.
        public static decimal PhysicalOutputMultiplier(NPC npc, string skillName)
        {
            if (npc == null || !IsPhysicalSkill(skillName)) return 1m;

            decimal multiplier = 1m;
            if (npc.HasTrait(NPCTraitType.Strong)) multiplier *= StrongMultiplier;
            if (npc.HasTrait(NPCTraitType.Weak)) multiplier *= WeakMultiplier;
            return multiplier;
        }

        // Strong / Weak, for a week of building work. A worker always contributes something.
        public static int ModifyConstructionPoints(NPC npc, int points)
        {
            if (npc == null) return points;

            if (npc.HasTrait(NPCTraitType.Strong)) points += 1;
            if (npc.HasTrait(NPCTraitType.Weak)) points -= 1;
            return Math.Max(1, points);
        }

        // Charismatic / Shy, as a shift to an effective social skill level.
        public static int SocialSkillModifier(NPC npc)
        {
            if (npc == null) return 0;

            int modifier = 0;
            if (npc.HasTrait(NPCTraitType.Charismatic)) modifier += CharismaticSkillBonus;
            if (npc.HasTrait(NPCTraitType.Shy)) modifier += ShySkillPenalty;
            return modifier;
        }

        // Effective level of a social skill once bearing is taken into account. Never pushes
        // someone who has no aptitude at all above 0.
        public static int EffectiveSocialSkill(NPC npc, string skillName)
        {
            int baseLevel = npc?.GetSkillLevel(skillName) ?? 0;
            if (npc == null || !IsSocialSkill(skillName) || baseLevel <= 0) return baseLevel;

            return Math.Max(0, baseLevel + SocialSkillModifier(npc));
        }
    }
}
