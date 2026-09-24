using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using DNDStrongholdApp.Models;

namespace DNDStrongholdApp.Services
{
    public static partial class CombatService
    {
        private static readonly string[] AwayProjects =
        {
            "Trade Mission",
            "Reconnaissance"
        };

        public static DefenseSnapshot Calculate(Stronghold stronghold)
        {
            var snapshot = new DefenseSnapshot();
            if (stronghold == null) return snapshot;

            var catalog = LoadBuildingData();
            snapshot.BuildingDefense = SumBuildingDefense(stronghold, catalog, snapshot.BuildingLines);
            snapshot.NpcCombat = SumNpcCombat(stronghold, snapshot);
            snapshot.Perception = SumWatchtowerPerception(stronghold, snapshot);
            snapshot.TotalDefense = snapshot.BuildingDefense + snapshot.NpcCombat;
            snapshot.RoundOneDefense = snapshot.NpcCombat;
            return snapshot;
        }

        public static RaidingParty CreateParty(EnemyFaction faction, OperationalGoal? goalOverride = null, List<TroopType>? roster = null)
        {
            faction.Normalize();
            var goal = goalOverride ?? PickGoal(faction);
            var troops = roster != null && roster.Count > 0
                ? roster.ToList()
                : AssembleTroops(faction, goal);
            var party = new RaidingParty
            {
                FactionId = faction.Id,
                FactionName = faction.Name,
                Troops = troops,
                Combatants = CreateCombatants(troops, faction),
                Goal = goal,
                Power = faction.Power
            };
            RefreshLiveStats(party, faction);
            return party;
        }

        public static OperationalGoal PickGoal(EnemyFaction faction)
        {
            var goals = faction.GoalPriority;
            if (goals == null || goals.Count == 0)
                return OperationalGoal.Pillage;

            int roll = Random.Shared.Next(100);
            int cumulative = 0;
            for (int i = 0; i < goals.Count; i++)
            {
                int weight = i < EnemyFaction.GoalWeights.Length
                    ? EnemyFaction.GoalWeights[i]
                    : 0;
                cumulative += weight;
                if (roll < cumulative)
                    return goals[i];
            }

            return goals[^1];
        }

        public static List<TroopType> AssembleTroops(EnemyFaction faction, OperationalGoal goal)
        {
            int power = Math.Clamp(faction.Power, 0, 5);
            if (power <= 0)
                return new List<TroopType>();

            var available = (faction.AvailableTroops ?? new List<TroopType>())
                .Where(t => Enum.IsDefined(t))
                .Distinct()
                .ToList();
            if (available.Count == 0)
                available = EnemyFaction.DefaultBanditTroops();

            var pool = available.Where(t => TroopAllowedAtPower(t, power)).ToList();
            if (pool.Count == 0)
                pool = available;

            var preferred = PreferredRoster(goal).Where(pool.Contains).ToList();
            int budget = RaidBudget(power, goal);
            int minUnits = 6 + power * 4;
            int maxUnits = 12 + power * 6;
            int magicCap = power <= 2 ? 1 : power <= 4 ? 2 : 3;
            int typeCap = Math.Max(3, (int)Math.Ceiling(maxUnits * 0.6));
            int preferredSpendTarget = preferred.Count > 0 ? (int)Math.Ceiling(budget * 0.45) : 0;

            var troops = new List<TroopType>();
            int spent = 0;
            int magic = 0;

            while (troops.Count < maxUnits && spent < budget)
            {
                int remaining = budget - spent;
                bool preferPhase = spent < preferredSpendTarget && preferred.Count > 0;
                var candidates = (preferPhase ? preferred : pool)
                    .Where(t => TroopCatalog.Cost(t) <= remaining)
                    .Where(t => !TroopCatalog.Get(t).IsMagic || magic < magicCap)
                    .Where(t => troops.Count(x => x == t) < typeCap)
                    .ToList();

                if (candidates.Count == 0 && preferPhase)
                {
                    preferredSpendTarget = spent;
                    continue;
                }
                if (candidates.Count == 0)
                    break;

                var pick = WeightedPick(candidates, goal);
                troops.Add(pick);
                spent += TroopCatalog.Cost(pick);
                if (TroopCatalog.Get(pick).IsMagic)
                    magic++;
            }

            if (troops.Count == 0)
            {
                var cheapest = pool.OrderBy(TroopCatalog.Cost).First();
                troops.Add(cheapest);
            }

            var filler = pool.OrderBy(TroopCatalog.Cost).First();
            while (troops.Count < minUnits)
                troops.Add(filler);

            return troops;
        }

        private static int RaidBudget(int power, OperationalGoal goal)
        {
            int[] budgets = { 0, 16, 30, 50, 74, 104 };
            double multiplier = goal switch
            {
                OperationalGoal.Scout => 0.7,
                OperationalGoal.Pillage => 1.0,
                OperationalGoal.Capture => 1.1,
                OperationalGoal.Raze => 1.3,
                _ => 1.0
            };
            return Math.Max(2, (int)Math.Round(budgets[power] * multiplier));
        }

        private static bool TroopAllowedAtPower(TroopType type, int power)
        {
            if (power >= 2) return true;
            return type is not TroopType.Tank
                and not TroopType.Cavalry
                and not TroopType.Arcane
                and not TroopType.HeavyInfantry;
        }

        private static IEnumerable<TroopType> PreferredRoster(OperationalGoal goal)
        {
            return goal switch
            {
                OperationalGoal.Scout => new[] { TroopType.Scout, TroopType.Flying, TroopType.LightInfantry },
                OperationalGoal.Capture => new[] { TroopType.HeavyInfantry, TroopType.Bruiser, TroopType.LightInfantry, TroopType.Divine },
                OperationalGoal.Raze => new[] { TroopType.Tank, TroopType.Bruiser, TroopType.HeavyInfantry, TroopType.Arcane },
                _ => new[] { TroopType.LightInfantry, TroopType.Grunt, TroopType.Cavalry, TroopType.Bruiser }
            };
        }

        private static double RosterWeight(OperationalGoal goal, TroopType type)
        {
            return goal switch
            {
                OperationalGoal.Scout => type switch
                {
                    TroopType.Scout => 6,
                    TroopType.Flying => 4,
                    TroopType.LightInfantry => 3,
                    TroopType.Grunt => 2,
                    TroopType.Divine => 1,
                    TroopType.Tank => 0.15,
                    TroopType.Bruiser => 0.3,
                    TroopType.HeavyInfantry => 0.4,
                    _ => 0.5
                },
                OperationalGoal.Raze => type switch
                {
                    TroopType.Tank => 6,
                    TroopType.Bruiser => 5,
                    TroopType.HeavyInfantry => 4,
                    TroopType.Arcane => 4,
                    TroopType.Divine => 3,
                    TroopType.Grunt => 2,
                    TroopType.LightInfantry => 2,
                    TroopType.Scout => 0.25,
                    _ => 1.5
                },
                OperationalGoal.Capture => type switch
                {
                    TroopType.HeavyInfantry => 5,
                    TroopType.Bruiser => 4,
                    TroopType.LightInfantry => 3,
                    TroopType.Divine => 3,
                    TroopType.Tank => 2,
                    TroopType.Cavalry => 2,
                    TroopType.Grunt => 2,
                    TroopType.Scout => 0.8,
                    TroopType.Flying => 0.6,
                    _ => 1
                },
                _ => type switch
                {
                    TroopType.LightInfantry => 4,
                    TroopType.Grunt => 3,
                    TroopType.Cavalry => 3,
                    TroopType.Bruiser => 2,
                    TroopType.Scout => 2,
                    TroopType.HeavyInfantry => 2,
                    _ => 1
                }
            };
        }

        private static TroopType WeightedPick(List<TroopType> candidates, OperationalGoal goal)
        {
            double total = 0;
            var weights = new double[candidates.Count];
            for (int i = 0; i < candidates.Count; i++)
            {
                weights[i] = RosterWeight(goal, candidates[i]);
                total += weights[i];
            }
            if (total <= 0)
                return candidates[Random.Shared.Next(candidates.Count)];

            double roll = Random.Shared.NextDouble() * total;
            double cumulative = 0;
            for (int i = 0; i < candidates.Count; i++)
            {
                cumulative += weights[i];
                if (roll <= cumulative)
                    return candidates[i];
            }
            return candidates[^1];
        }

        public static RaidStatblock SumTroopStats(IReadOnlyList<TroopType> troops)
        {
            var stats = new RaidStatblock { Numbers = troops.Count };
            foreach (var type in troops)
            {
                var def = TroopCatalog.Get(type);
                stats.Strength += def.Strength;
                stats.HitPoints += def.HitPoints;
                stats.Stealth += def.Stealth;
                if (def.IsMagic)
                    stats.MagicUsers++;
            }
            stats.SizeStealthPenalty = SizeStealthPenalty(troops.Count);
            stats.Stealth += stats.SizeStealthPenalty;
            return stats;
        }

        // -1 stealth per 3 troops over 5, rounded up. Five or fewer: no penalty.
        public static int SizeStealthPenalty(int troopCount)
        {
            if (troopCount <= 5) return 0;
            return -((troopCount - 5 + 2) / 3);
        }

        public static string FormatStealth(RaidStatblock stats)
        {
            return stats.SizeStealthPenalty == 0
                ? $"Stealth {stats.Stealth}"
                : $"Stealth {stats.Stealth} ({stats.SizeStealthPenalty} size)";
        }

        public static string DescribeComposition(RaidingParty party)
        {
            if (party.Combatants != null && party.Combatants.Count > 0)
            {
                return string.Join(", ",
                    party.Combatants.GroupBy(t => t.Type)
                        .OrderByDescending(g => g.Count())
                        .ThenBy(g => g.First().Name)
                        .Select(g =>
                        {
                            var def = TroopCatalog.Get(g.Key);
                            return $"{g.Count()} {g.First().Name}{TroopCatalog.ReportSuffix(def)}";
                        }));
            }
            return DescribeComposition(party.Troops);
        }

        public static string DescribeComposition(IReadOnlyList<TroopType> troops, EnemyFaction? faction = null)
        {
            if (troops == null || troops.Count == 0)
                return "no troops";

            return string.Join(", ",
                troops.GroupBy(t => t)
                    .OrderByDescending(g => g.Count())
                    .ThenBy(g => faction?.TroopName(g.Key) ?? TroopCatalog.DisplayName(g.Key))
                    .Select(g =>
                    {
                        var def = TroopCatalog.Get(g.Key);
                        string name = faction?.TroopName(g.Key) ?? def.Name;
                        return $"{g.Count()} {name}{TroopCatalog.ReportSuffix(def)}";
                    }));
        }

        public static void ApplyDetection(RaidingParty party, DefenseSnapshot snapshot)
        {
            // Placeholder until the detection roll is designed: watchtowers win ties.
            party.Surprise = party.Stats.Stealth > snapshot.Perception;
        }

        public static RaidingParty? TryCreateWeeklyRaid(Stronghold stronghold)
        {
            if (stronghold?.EnemyFactions == null || stronghold.EnemyFactions.Count == 0)
                return null;

            var triggered = new List<EnemyFaction>();
            foreach (var faction in stronghold.EnemyFactions)
            {
                faction.Normalize();
                int chance = faction.EffectiveFrequencyPercent;
                if (chance <= 0) continue;
                if (Random.Shared.Next(100) < chance)
                    triggered.Add(faction);
            }

            if (triggered.Count == 0) return null;

            var chosen = triggered[Random.Shared.Next(triggered.Count)];
            var party = CreateParty(chosen);
            var snapshot = Calculate(stronghold);
            ApplyDetection(party, snapshot);
            return party;
        }

        public static string BuildRaidReport(RaidingParty party, DefenseSnapshot snapshot)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"{party.FactionName} (Power {party.Power} — {FactionPower.Label(party.Power)}) raid — goal: {party.Goal}.");
            sb.AppendLine($"Troops: {DescribeComposition(party)}.");
            int divine = party.Combatants?.Count(t => t.IsDivine && !t.IsDead) ?? 0;
            int arcane = party.Combatants?.Count(t => t.IsArcane && !t.IsDead) ?? 0;
            string casters = "";
            if (divine > 0 || arcane > 0)
            {
                var bits = new List<string>();
                if (divine > 0) bits.Add($"{divine} divine");
                if (arcane > 0) bits.Add($"{arcane} arcane");
                casters = $", {string.Join(", ", bits)}";
            }
            sb.AppendLine($"Party: {party.Stats.Numbers} strong, Strength {party.Stats.Strength}, HP {party.Stats.HitPoints}, {FormatStealth(party.Stats)}{casters}.");
            sb.AppendLine(party.Surprise
                ? "The raid was not detected (Surprise). Building Defense is ignored in round 1."
                : "The raid was detected. The stronghold mounts a regular defense.");
            sb.AppendLine();
            sb.AppendLine($"Perception: {snapshot.Perception} ({snapshot.LookoutCount} lookout(s)).");
            sb.AppendLine($"Defense: {snapshot.TotalDefense} (buildings {snapshot.BuildingDefense} + fighters {snapshot.NpcCombat} from {snapshot.FighterCount} NPC(s)).");
            if (party.Surprise)
                sb.AppendLine($"Round 1 Defense (no buildings): {snapshot.RoundOneDefense}.");
            return sb.ToString().TrimEnd();
        }

        public static bool IsAway(NPC npc, Stronghold stronghold)
        {
            if (npc == null) return false;
            if (npc.Assignment == null) return false;
            if (npc.Assignment.Type == AssignmentType.Mission) return true;

            var building = FindAssignedBuilding(npc, stronghold);
            if (building?.CurrentProject == null) return false;
            if (!IsAssignedToProject(npc, building.CurrentProject)) return false;
            return IsAwayProject(building.CurrentProject.Name);
        }

        public static bool CanFight(NPC npc, Stronghold stronghold)
        {
            if (npc == null || !npc.IsAlive) return false;
            if (HasState(npc, NPCStateType.Sick) || HasState(npc, NPCStateType.GravelyInjured))
                return false;
            if (IsAway(npc, stronghold)) return false;

            int skill = ProjectResolutionService.GetSkillLevel(npc, "Combat") + npc.SpellcastingLevel;
            if (skill <= 0 && IsImpaired(npc))
                return false;

            return true;
        }

        private static int SumBuildingDefense(Stronghold stronghold, BuildingData? catalog, List<string> lines)
        {
            int total = 0;
            foreach (var building in stronghold.Buildings)
            {
                int raw = GetCatalogDefense(catalog, building);
                int applied = ApplyCondition(building, raw);
                if (raw <= 0 && applied <= 0) continue;
                lines.Add($"{building.Name} (Lv {building.Level}, {building.ConstructionStatus}, {building.Condition}%): {applied}");
                total += applied;
            }
            return total;
        }

        private static int SumNpcCombat(Stronghold stronghold, DefenseSnapshot snapshot)
        {
            double total = 0;
            int fighters = 0;
            foreach (var npc in stronghold.NPCs)
            {
                if (!CanFight(npc, stronghold)) continue;
                fighters++;
                total += NpcCombatValue(npc);
            }
            snapshot.FighterCount = fighters;
            return (int)Math.Floor(total);
        }

        // Base 0.5 for every fighter. Impaired trained NPCs keep the base and halve skill only.
        // Spellcasters add their highest spellcasting skill on top of Combat.
        private static double NpcCombatValue(NPC npc)
        {
            int skill = ProjectResolutionService.GetSkillLevel(npc, "Combat") + npc.SpellcastingLevel;
            double skillValue = IsImpaired(npc) ? skill / 2.0 : skill;
            return 0.5 + skillValue;
        }

        private static bool IsImpaired(NPC npc)
        {
            return npc.IsStarving()
                || HasState(npc, NPCStateType.LightlyInjured);
        }

        private static int SumWatchtowerPerception(Stronghold stronghold, DefenseSnapshot snapshot)
        {
            int total = 0;
            var counted = new HashSet<string>();
            foreach (var tower in stronghold.Buildings.Where(b =>
                string.Equals(b.TypeName, "Watchtower", StringComparison.OrdinalIgnoreCase)))
            {
                foreach (var npc in WatchtowerCrew(tower, stronghold))
                {
                    if (!counted.Add(npc.Id)) continue;
                    if (IsAway(npc, stronghold)) continue;
                    int perception = ProjectResolutionService.GetSkillLevel(npc, "Perception");
                    bool patrol = IsOnNamedProject(npc, tower, "Patrol");
                    total += 1 + perception * (patrol ? 3 : 1);
                    snapshot.LookoutCount++;
                }
            }
            return total;
        }

        private static IEnumerable<NPC> WatchtowerCrew(Building tower, Stronghold stronghold)
        {
            var ids = new HashSet<string>(tower.AssignedWorkers ?? new List<string>());
            if (tower.CurrentProject?.AssignedWorkers != null)
            {
                foreach (var id in tower.CurrentProject.AssignedWorkers)
                    ids.Add(id);
            }

            foreach (var id in ids)
            {
                var npc = stronghold.NPCs.Find(n => n.Id == id);
                if (npc != null && npc.IsAlive)
                    yield return npc;
            }
        }

        public static bool ProvidesDefense(string typeName)
        {
            var info = FindBuildingInfo(LoadBuildingData(), typeName);
            return info?.defenseScaling != null && info.defenseScaling.Any(d => d.defense > 0);
        }

        public static int GetCatalogDefenseForLevel(string typeName, int level)
        {
            return GetCatalogDefenseAtLevel(LoadBuildingData(), typeName, level);
        }

        public static int GetBuildingDefenseContribution(Building building)
        {
            var catalog = LoadBuildingData();
            return ApplyCondition(building, GetCatalogDefense(catalog, building));
        }

        public static string DescribeBuildingDefense(Building building)
        {
            if (building == null || !ProvidesDefense(building.TypeName))
                return "None";

            int catalog = GetCatalogDefenseForLevel(building.TypeName, building.Level);
            int applied = GetBuildingDefenseContribution(building);
            string text;

            if (building.ConstructionStatus == BuildingStatus.Planning
                || building.ConstructionStatus == BuildingStatus.UnderConstruction)
            {
                text = catalog > 0
                    ? $"0 until complete ({catalog} at level {building.Level})"
                    : "0 until complete";
            }
            else if (applied != catalog && catalog > 0)
            {
                string reason = building.Condition < 25
                    ? $"condition {building.Condition}%"
                    : $"half, condition {building.Condition}%";
                text = $"{applied} ({reason}; intact {catalog})";
            }
            else
            {
                text = applied.ToString();
            }

            if (building.ConstructionStatus == BuildingStatus.Upgrading)
            {
                int next = GetCatalogDefenseForLevel(building.TypeName, building.Level + 1);
                if (next != catalog)
                {
                    int boost = next - catalog;
                    string sign = boost > 0 ? "+" : "";
                    text += $" → {next} after upgrade ({sign}{boost})";
                }
            }

            return text;
        }

        private static BuildingInfo? FindBuildingInfo(BuildingData? catalog, string typeName)
        {
            return catalog?.buildings?.Find(b =>
                string.Equals(b.type, typeName, StringComparison.OrdinalIgnoreCase));
        }

        private static int GetCatalogDefenseAtLevel(BuildingData? catalog, string typeName, int level)
        {
            var info = FindBuildingInfo(catalog, typeName);
            if (info?.defenseScaling == null || info.defenseScaling.Count == 0)
                return 0;

            var match = info.defenseScaling
                .Where(d => d.level <= level)
                .OrderByDescending(d => d.level)
                .FirstOrDefault();
            return match?.defense ?? 0;
        }

        private static int GetCatalogDefense(BuildingData? catalog, Building building)
        {
            if (building.ConstructionStatus == BuildingStatus.Planning
                || building.ConstructionStatus == BuildingStatus.UnderConstruction)
            {
                return 0;
            }

            return GetCatalogDefenseAtLevel(catalog, building.TypeName, building.Level);
        }

        private static int ApplyCondition(Building building, int raw)
        {
            if (raw <= 0) return 0;
            if (building.Condition < 25) return 0;
            if (building.Condition < 50) return raw / 2;
            return raw;
        }

        private static Building? FindAssignedBuilding(NPC npc, Stronghold stronghold)
        {
            if (!string.IsNullOrEmpty(npc.Assignment.TargetId))
            {
                var byId = stronghold.Buildings.Find(b => b.Id == npc.Assignment.TargetId);
                if (byId != null) return byId;
            }

            return stronghold.Buildings.FirstOrDefault(b =>
                (b.AssignedWorkers != null && b.AssignedWorkers.Contains(npc.Id))
                || (b.CurrentProject?.AssignedWorkers != null && b.CurrentProject.AssignedWorkers.Contains(npc.Id)));
        }

        private static bool IsAssignedToProject(NPC npc, Project project)
        {
            return project.AssignedWorkers != null && project.AssignedWorkers.Contains(npc.Id);
        }

        private static bool IsOnNamedProject(NPC npc, Building building, string projectName)
        {
            return building.CurrentProject != null
                && string.Equals(building.CurrentProject.Name, projectName, StringComparison.OrdinalIgnoreCase)
                && IsAssignedToProject(npc, building.CurrentProject);
        }

        private static bool IsAwayProject(string? name)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;
            return AwayProjects.Any(p => string.Equals(p, name, StringComparison.OrdinalIgnoreCase));
        }

        private static bool HasState(NPC npc, NPCStateType type)
        {
            return npc.States != null && npc.States.Any(s => s.Type == type);
        }

        private static BuildingData? LoadBuildingData()
        {
            string[] possiblePaths =
            {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "BuildingData.json"),
                Path.Combine(Directory.GetCurrentDirectory(), "Data", "BuildingData.json"),
                Path.Combine(Directory.GetCurrentDirectory(), "..", "Data", "BuildingData.json"),
                Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "Data", "BuildingData.json")
            };

            string? jsonPath = possiblePaths.FirstOrDefault(File.Exists);
            if (string.IsNullOrEmpty(jsonPath)) return null;

            try
            {
                return JsonSerializer.Deserialize<BuildingData>(File.ReadAllText(jsonPath));
            }
            catch
            {
                return null;
            }
        }
    }
}
