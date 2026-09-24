using System;
using System.Collections.Generic;
using System.Linq;
using DNDStrongholdApp.Models;

namespace DNDStrongholdApp.Services
{
    /// <summary>
    /// Building upkeep affordability. Idle buildings (no workers) cost nothing.
    /// A staffed building that cannot pay Wood/Stone/Iron/Luxury upkeep from
    /// current stocks does not operate (no production, no upkeep) for the week.
    /// Food upkeep is not a shutter gate — food rationing handles food.
    /// </summary>
    public static class UpkeepService
    {
        /// <summary>Lower = keep longer / pay first. Higher = cut / shutter first.</summary>
        public enum KeepPriority
        {
            Food = 0,
            BasicMaterials = 1,
            Valuables = 2,
            ProjectsOther = 3,
            Construction = 4
        }

        /// <summary>Resources that must be in stock for a building to operate.</summary>
        public static bool IsOperatingMaterial(ResourceType type) =>
            type is ResourceType.Wood or ResourceType.Stone or ResourceType.Iron or ResourceType.Luxury;

        public sealed class PayrollSnapshot
        {
            public int Available { get; init; }
            public int Needed { get; init; }
            public int Shortfall => Math.Max(0, Needed - Available);
            public bool HasShortfall => Shortfall > 0;
            public int BaseGoldUpkeep { get; init; }
            public int SalaryGoldUpkeep { get; init; }
        }

        public sealed class MaterialGap
        {
            public ResourceType Resource { get; init; }
            public int Have { get; init; }
            public int Need { get; init; }
            public int Short => Math.Max(0, Need - Have);
        }

        public sealed class PaidWorker
        {
            public string NpcId { get; init; } = "";
            public string NpcName { get; init; } = "";
            public string BuildingId { get; init; } = "";
            public string BuildingName { get; init; } = "";
            public string BuildingType { get; init; } = "";
            public bool IsConstructionCrew { get; init; }
            public int Salary { get; init; }
            public KeepPriority Priority { get; init; }
            public bool IsProtected { get; init; }
            public string ProtectReason { get; init; } = "";
        }

        public static int WorkerSalary(NPC npc)
        {
            if (npc?.Skills == null || npc.Skills.Count == 0)
                return 1;
            int skill = npc.Skills.Max(s => s.Level);
            return Math.Max(1, (int)Math.Ceiling(skill / 2.0));
        }

        public static KeepPriority PriorityForBuilding(Building building, bool constructionCrew)
        {
            if (constructionCrew || !building.IsFunctional())
                return KeepPriority.Construction;

            return building.TypeName switch
            {
                "Farm" or "Greenhouse" or "Granary" => KeepPriority.Food,
                "Quarry" or "MasonsYard" => KeepPriority.BasicMaterials,
                "Mine" or "Smithy" or "Workshop" or "TradeOffice"
                    or "Tavern" or "Laboratory" or "Library" => KeepPriority.Valuables,
                _ => KeepPriority.ProjectsOther
            };
        }

        public static string PriorityLabel(KeepPriority p) => p switch
        {
            KeepPriority.Food => "Food",
            KeepPriority.BasicMaterials => "Wood / Stone",
            KeepPriority.Valuables => "Iron / Gold / Luxury",
            KeepPriority.ProjectsOther => "Projects / other",
            KeepPriority.Construction => "Construction",
            _ => p.ToString()
        };

        public static bool IsStaffed(Building building) =>
            building.IsFunctional()
            && building.AssignedWorkers != null
            && building.AssignedWorkers.Count > 0;

        public static int Stock(Stronghold stronghold, ResourceType type) =>
            stronghold.Resources.Find(r => r.Type == type)?.Amount ?? 0;

        /// <summary>
        /// Wood/Stone/Iron/Luxury upkeep only — these gate whether a staffed building runs.
        /// </summary>
        public static List<ResourceCost> MaterialUpkeep(Building building, Stronghold stronghold) =>
            building.CalculateUpkeep(stronghold.NPCs)
                .Where(c => IsOperatingMaterial(c.ResourceType) && c.Amount > 0)
                .ToList();

        /// <summary>Per-resource have/need for a building against current stocks (no pool competition).</summary>
        public static List<MaterialGap> DescribeMaterialGaps(Building building, Stronghold stronghold)
        {
            return MaterialUpkeep(building, stronghold)
                .Select(c => new MaterialGap
                {
                    Resource = c.ResourceType,
                    Have = Stock(stronghold, c.ResourceType),
                    Need = c.Amount
                })
                .ToList();
        }

        public static string FormatMaterialGaps(Building building, Stronghold stronghold)
        {
            var gaps = DescribeMaterialGaps(building, stronghold);
            if (gaps.Count == 0) return "no material upkeep";
            return string.Join(", ", gaps.Select(g =>
                g.Short > 0
                    ? $"{g.Resource} short (have {g.Have}, need {g.Need})"
                    : $"{g.Resource} OK ({g.Have}/{g.Need})"));
        }

        /// <summary>
        /// Buildings that can pay material upkeep from current stocks this week.
        /// Higher-priority buildings reserve materials first; their same-week output
        /// can fund lower-priority buildings.
        /// </summary>
        public static HashSet<string> GetOperableBuildingIds(Stronghold stronghold)
        {
            var operable = new HashSet<string>();
            var pool = Enum.GetValues<ResourceType>()
                .ToDictionary(t => t, t => Stock(stronghold, t));

            var candidates = stronghold.Buildings
                .Where(IsStaffed)
                .OrderBy(b => (int)PriorityForBuilding(b, false))
                .ThenBy(b => b.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var building in candidates)
            {
                var materials = MaterialUpkeep(building, stronghold);
                bool canPay = materials.All(c => pool.GetValueOrDefault(c.ResourceType) >= c.Amount);
                if (!canPay)
                    continue;

                foreach (var cost in materials)
                    pool[cost.ResourceType] -= cost.Amount;

                operable.Add(building.Id);

                var workers = building.AssignedWorkers
                    .Select(id => stronghold.NPCs.Find(n => n.Id == id))
                    .OfType<NPC>()
                    .ToList();
                foreach (var prod in building.CalculateProduction(workers))
                {
                    if (prod.Amount > 0)
                        pool[prod.ResourceType] = pool.GetValueOrDefault(prod.ResourceType) + prod.Amount;
                }
            }

            return operable;
        }

        public static List<Building> ListShutteredBuildings(Stronghold stronghold)
        {
            var operable = GetOperableBuildingIds(stronghold);
            return stronghold.Buildings
                .Where(IsStaffed)
                .Where(b => !operable.Contains(b.Id))
                .OrderByDescending(b => (int)PriorityForBuilding(b, false))
                .ThenBy(b => b.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public static bool HasMaterialOperatingShortfall(Stronghold stronghold) =>
            ListShutteredBuildings(stronghold).Count > 0;

        public static bool HasAnyUpkeepShortfall(Stronghold stronghold) =>
            HasGoldShortfall(stronghold) || HasMaterialOperatingShortfall(stronghold);

        public static PayrollSnapshot GetGoldPayroll(Stronghold stronghold)
        {
            var gold = stronghold.Resources.Find(r => r.Type == ResourceType.Gold);
            // Gold available for the coming turn = current stock + projected production.
            int available = gold == null ? 0 : gold.Amount + Math.Max(0, gold.WeeklyProduction);

            var operable = GetOperableBuildingIds(stronghold);
            int salary = 0;
            int totalBuildingGold = 0;
            foreach (var building in stronghold.Buildings.Where(IsStaffed))
            {
                if (!operable.Contains(building.Id))
                    continue;

                var goldCost = building.CalculateUpkeep(stronghold.NPCs)
                    .Find(c => c.ResourceType == ResourceType.Gold);
                if (goldCost == null) continue;

                int buildingSalary = ListPaidWorkers(stronghold, building).Sum(w => w.Salary);
                salary += buildingSalary;
                totalBuildingGold += goldCost.Amount;
            }

            // Construction / repair / upgrade salaries
            foreach (var building in stronghold.Buildings.Where(b => !b.IsFunctional()))
            {
                var goldCost = building.CalculateUpkeep(stronghold.NPCs)
                    .Find(c => c.ResourceType == ResourceType.Gold);
                if (goldCost == null) continue;
                int buildingSalary = ListPaidWorkers(stronghold, building).Sum(w => w.Salary);
                salary += buildingSalary;
                totalBuildingGold += goldCost.Amount;
            }

            int baseGold = Math.Max(0, totalBuildingGold - salary);

            return new PayrollSnapshot
            {
                Available = available,
                Needed = totalBuildingGold,
                BaseGoldUpkeep = baseGold,
                SalaryGoldUpkeep = salary
            };
        }

        public static bool HasGoldShortfall(Stronghold stronghold) =>
            GetGoldPayroll(stronghold).HasShortfall;

        public static List<PaidWorker> ListCutCandidates(Stronghold stronghold)
        {
            return stronghold.Buildings
                .SelectMany(b => ListPaidWorkers(stronghold, b))
                .Where(w => !w.IsProtected)
                .OrderByDescending(w => (int)w.Priority)
                .ThenByDescending(w => w.Salary)
                .ThenBy(w => w.NpcName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public static List<PaidWorker> ListPaidWorkers(Stronghold stronghold, Building building)
        {
            var result = new List<PaidWorker>();

            void Add(string npcId, bool construction)
            {
                var npc = stronghold.NPCs.Find(n => n.Id == npcId);
                if (npc == null || !npc.IsAlive) return;

                bool away = CombatService.IsAway(npc, stronghold);
                bool steward = building.TypeName == "Keep"
                    && string.Equals(npc.Title, "Steward", StringComparison.OrdinalIgnoreCase);

                result.Add(new PaidWorker
                {
                    NpcId = npc.Id,
                    NpcName = npc.Name,
                    BuildingId = building.Id,
                    BuildingName = string.IsNullOrWhiteSpace(building.Name) ? building.TypeName : building.Name,
                    BuildingType = building.TypeName,
                    IsConstructionCrew = construction,
                    Salary = WorkerSalary(npc),
                    Priority = PriorityForBuilding(building, construction),
                    IsProtected = away || steward,
                    ProtectReason = away ? "Away on mission" : steward ? "Steward" : ""
                });
            }

            foreach (var id in building.DedicatedConstructionCrew ?? new List<string>())
                Add(id, true);
            foreach (var id in building.AssignedWorkers ?? new List<string>())
            {
                if (building.DedicatedConstructionCrew != null
                    && building.DedicatedConstructionCrew.Contains(id))
                    continue;
                Add(id, false);
            }

            return result;
        }

        public static List<PaidWorker> PlanAutoCuts(Stronghold stronghold)
        {
            var plan = new List<PaidWorker>();
            var plannedIds = new HashSet<string>();

            foreach (var building in ListShutteredBuildings(stronghold))
            {
                foreach (var worker in ListPaidWorkers(stronghold, building).Where(w => !w.IsProtected))
                {
                    if (plannedIds.Add(worker.NpcId))
                        plan.Add(worker);
                }
            }

            var gold = stronghold.Resources.Find(r => r.Type == ResourceType.Gold);
            int goldAvailable = gold == null ? 0 : gold.Amount + Math.Max(0, gold.WeeklyProduction);
            var operable = GetOperableBuildingIds(stronghold);

            // Gold still owed after already-planned cuts. Protected posts (Steward, away)
            // always remain, so Keep base + steward salary never drop out of the bill.
            int goldNeeded = GoldNeededAfterCuts(stronghold, plannedIds, operable);

            int goldShortfall = goldNeeded - goldAvailable;
            if (goldShortfall <= 0)
                return plan;

            foreach (var worker in ListCutCandidates(stronghold))
            {
                if (!plannedIds.Add(worker.NpcId))
                    continue;
                plan.Add(worker);
                // Re-check remaining bill: emptying a building also drops its base upkeep.
                goldNeeded = GoldNeededAfterCuts(stronghold, plannedIds, operable);
                if (goldNeeded <= goldAvailable)
                    break;
            }

            return plan;
        }

        /// <summary>
        /// Gold upkeep still owed if the given NPCs are unassigned.
        /// A building only goes idle (no upkeep) when nobody remains — protected
        /// workers such as the Keep Steward always keep their building on the bill.
        /// </summary>
        public static int ProjectedNeededAfterCuts(Stronghold stronghold, IEnumerable<string> npcIdsToCut)
        {
            return GoldNeededAfterCuts(stronghold, new HashSet<string>(npcIdsToCut), GetOperableBuildingIds(stronghold));
        }

        private static int GoldNeededAfterCuts(
            Stronghold stronghold,
            HashSet<string> cutNpcIds,
            HashSet<string> operable)
        {
            int needed = 0;

            foreach (var building in stronghold.Buildings)
            {
                var allPaid = ListPaidWorkers(stronghold, building);
                if (allPaid.Count == 0)
                    continue;

                var remaining = allPaid
                    .Where(w => w.IsProtected || !cutNpcIds.Contains(w.NpcId))
                    .ToList();

                // Truly empty after cuts → idle, no upkeep (base or salaries).
                if (remaining.Count == 0)
                    continue;

                if (!building.IsFunctional())
                {
                    needed += remaining.Sum(w => w.Salary);
                    continue;
                }

                // Match GetGoldPayroll: material-shuttered buildings do not draw gold this turn.
                if (!operable.Contains(building.Id))
                    continue;

                var goldCost = building.CalculateUpkeep(stronghold.NPCs)
                    .Find(c => c.ResourceType == ResourceType.Gold);
                if (goldCost == null) continue;

                int cutSalaries = allPaid
                    .Where(w => !w.IsProtected && cutNpcIds.Contains(w.NpcId))
                    .Sum(w => w.Salary);
                needed += Math.Max(0, goldCost.Amount - cutSalaries);
            }

            return needed;
        }

        /// <summary>
        /// Gold still owed if every cuttable worker is laid off (Keep + Steward and
        /// any other protected posts remain).
        /// </summary>
        public static int GetUnavoidableGoldUpkeep(Stronghold stronghold)
        {
            var cutAll = new HashSet<string>(
                ListCutCandidates(stronghold).Select(w => w.NpcId));
            return GoldNeededAfterCuts(stronghold, cutAll, GetOperableBuildingIds(stronghold));
        }

        public static int GetGoldWeeklyProduction(Stronghold stronghold)
        {
            var gold = stronghold.Resources.Find(r => r.Type == ResourceType.Gold);
            return gold == null ? 0 : Math.Max(0, gold.WeeklyProduction);
        }

        /// <summary>Coffers + projected production — same basis as payroll "Available".</summary>
        public static int GetGoldAvailable(Stronghold stronghold)
        {
            var gold = stronghold.Resources.Find(r => r.Type == ResourceType.Gold);
            return gold == null ? 0 : gold.Amount + Math.Max(0, gold.WeeklyProduction);
        }

        /// <summary>
        /// Structural insolvency: no one left to lay off, and gold available
        /// (coffers + production) cannot cover Keep/Steward (and other protected) upkeep.
        /// Injecting gold into the coffers can clear this without raising production.
        /// </summary>
        public static bool IsStructurallyBankrupt(Stronghold stronghold)
        {
            if (ListCutCandidates(stronghold).Count > 0)
                return false;
            int unavoidable = GetUnavoidableGoldUpkeep(stronghold);
            if (unavoidable <= 0)
                return false;
            return GetGoldAvailable(stronghold) < unavoidable;
        }
    }
}
