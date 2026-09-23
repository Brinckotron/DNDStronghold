using System;
using System.Collections.Generic;
using System.Linq;
using DNDStrongholdApp.Models;

namespace DNDStrongholdApp.Services
{
    public static class ProjectResolutionService
    {
        public static int GetSkillLevel(NPC npc, string skillName)
        {
            if (npc == null || string.IsNullOrEmpty(skillName)) return 0;
            return npc.Skills.FirstOrDefault(s => s.Name == skillName)?.Level ?? 0;
        }

        public static int GetWorkerRelevantSkill(NPC worker, List<string> skills)
        {
            if (worker == null || skills == null || skills.Count == 0) return 0;
            int best = 0;
            for (int i = 0; i < skills.Count; i++)
            {
                int level = GetSkillLevel(worker, skills[i]);
                int value = i == 0 ? level : level / 2;
                if (value > best) best = value;
            }
            return best;
        }

        public static List<string> GetRelevantSkills(BuildingInfo? info, Project project)
        {
            if (project.BonusSkills != null && project.BonusSkills.Count > 0)
                return project.BonusSkills.Where(s => !string.IsNullOrWhiteSpace(s)).ToList();

            var skills = new List<string>();
            if (info != null)
            {
                if (!string.IsNullOrEmpty(info.primarySkill)) skills.Add(info.primarySkill);
                if (!string.IsNullOrEmpty(info.secondarySkill)) skills.Add(info.secondarySkill);
                if (!string.IsNullOrEmpty(info.tertiarySkill)) skills.Add(info.tertiarySkill);
            }
            return skills;
        }

        public static int ComputeBonus(Building building, BuildingInfo? info, Project project, List<NPC> allNpcs)
        {
            var skills = GetRelevantSkills(info, project);
            var workers = project.AssignedWorkers
                .Select(id => allNpcs.Find(n => n.Id == id))
                .Where(n => n != null)
                .Cast<NPC>()
                .ToList();

            if (workers.Count == 0)
                return Math.Max(0, building.Level - 1);

            int highest = workers.Max(w => GetWorkerRelevantSkill(w, skills));
            string primary = skills.FirstOrDefault() ?? string.Empty;
            int extras = 0;
            bool countedHighest = false;
            foreach (var worker in workers)
            {
                if (GetSkillLevel(worker, primary) < 1) continue;
                if (!countedHighest && GetWorkerRelevantSkill(worker, skills) == highest)
                {
                    countedHighest = true;
                    continue;
                }
                extras++;
            }

            return highest + extras + Math.Max(0, building.Level - 1);
        }

        public static ProjectResultTier GetResultTier(int d20Total, int bonus, int dc)
        {
            int total = d20Total + bonus;
            int margin = total - dc;
            if (margin >= 10) return ProjectResultTier.Exceptional;
            if (margin >= 0) return ProjectResultTier.Success;
            if (margin >= -4) return ProjectResultTier.Partial;
            return ProjectResultTier.Failure;
        }

        public static decimal GetYieldMultiplier(ProjectResultTier tier) => tier switch
        {
            ProjectResultTier.Exceptional => 1.5m,
            ProjectResultTier.Success => 1.0m,
            ProjectResultTier.Partial => 0.6m,
            ProjectResultTier.Failure => 0.25m,
            _ => 1.0m
        };

        public static List<ResourceCost> ScaleYield(List<ResourceCost> listed, ProjectResultTier tier)
        {
            decimal mult = GetYieldMultiplier(tier);
            var result = new List<ResourceCost>();
            foreach (var cost in listed)
            {
                int amount = (int)Math.Ceiling(cost.Amount * mult);
                if (amount > 0)
                {
                    result.Add(new ResourceCost { ResourceType = cost.ResourceType, Amount = amount });
                }
            }
            return result;
        }

        public static List<ResourceCost> ComputeTradeFairYield(Building building, Project project, List<NPC> allNpcs, int reputation)
        {
            var workers = project.AssignedWorkers
                .Select(id => allNpcs.Find(n => n.Id == id))
                .Where(n => n != null)
                .Cast<NPC>()
                .ToList();

            // Charismatic and Shy shift how well a trader actually works a market.
            int highestTrade = workers.Count == 0 ? 0 : workers.Max(w => TraitService.EffectiveSocialSkill(w, "Trade"));
            int extras = Math.Max(0, workers.Count(w => TraitService.EffectiveSocialSkill(w, "Trade") >= 1) - (highestTrade >= 1 ? 1 : 0));
            int gold = 5 + highestTrade * 4 + extras * 2 + building.Level * 5 + Math.Clamp(reputation, 0, 30);
            var yield = new List<ResourceCost>
            {
                new ResourceCost { ResourceType = ResourceType.Gold, Amount = Math.Max(1, gold) }
            };

            if (project.FairFocusResource is ResourceType focus
                && focus != ResourceType.Gold
                && CanChooseTradeFairFocus(focus, building.Level))
            {
                int focusAmount = project.ExpectedReturn?.Find(c => c.ResourceType == focus)?.Amount ?? 0;
                AddYield(yield, focus, focusAmount);
            }

            return yield;
        }

        public static int TradeFairFocusMinLevel(ResourceType type) => type switch
        {
            ResourceType.Iron => 2,
            ResourceType.Luxury => 3,
            ResourceType.Food or ResourceType.Stone or ResourceType.Wood => 1,
            _ => int.MaxValue
        };

        public static bool CanChooseTradeFairFocus(ResourceType type, int buildingLevel) =>
            buildingLevel >= TradeFairFocusMinLevel(type);

        public static (int min, int max) TradeFairFocusRange(ResourceType type) => type switch
        {
            ResourceType.Food => (10, 15),
            ResourceType.Stone => (5, 10),
            ResourceType.Wood => (5, 10),
            ResourceType.Iron => (1, 3),
            ResourceType.Luxury => (0, 2),
            _ => (0, 0)
        };

        public static int RollTradeFairFocusAmount(ResourceType type)
        {
            var (min, max) = TradeFairFocusRange(type);
            if (max < min) return 0;
            return Random.Shared.Next(min, max + 1);
        }

        private static void AddYield(List<ResourceCost> yield, ResourceType type, int amount)
        {
            if (amount <= 0) return;
            var existing = yield.Find(c => c.ResourceType == type);
            if (existing != null)
                existing.Amount += amount;
            else
                yield.Add(new ResourceCost { ResourceType = type, Amount = amount });
        }

        public static void AwardProjectSkillXp(Project project, BuildingInfo? info, List<NPC> allNpcs)
        {
            var skills = GetRelevantSkills(info, project);
            string primary = skills.FirstOrDefault() ?? string.Empty;
            string secondary = skills.Count > 1 ? skills[1] : string.Empty;

            foreach (var workerId in project.AssignedWorkers)
            {
                var npc = allNpcs.Find(n => n.Id == workerId);
                if (npc == null || npc.IsStarving()) continue;
                if (!string.IsNullOrEmpty(primary))
                    npc.AddSkillExperience(primary, Random.Shared.Next(8, 16));
                if (!string.IsNullOrEmpty(secondary))
                    npc.AddSkillExperience(secondary, Random.Shared.Next(3, 11));
            }
        }

        public static string FormatCosts(IEnumerable<ResourceCost> costs)
        {
            var list = costs?.Where(c => c.Amount > 0).ToList() ?? new List<ResourceCost>();
            if (list.Count == 0) return "None";
            return string.Join(", ", list.Select(c => $"{c.Amount} {c.ResourceType}"));
        }

        public static string DefaultMishap(string projectName, ProjectResultTier tier)
        {
            if (tier == ProjectResultTier.Partial)
                return "A minor setback complicated the work.";
            if (tier == ProjectResultTier.Failure)
                return "The effort went badly. A contact was offended, or the work produced a complication for later.";
            return string.Empty;
        }
    }
}
