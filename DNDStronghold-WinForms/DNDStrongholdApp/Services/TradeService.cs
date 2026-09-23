using System;
using System.Collections.Generic;
using System.Linq;
using DNDStrongholdApp.Models;

namespace DNDStrongholdApp.Services
{
    public static class TradeService
    {
        public const decimal DemandBuyMultiplier = 1.4m;
        public const decimal EventDemandMultiplier = 1.5m;
        public const decimal EventSurplusSellMultiplier = 0.6m;
        public const decimal DroughtFoodProductionMultiplier = 0.15m;
        public const decimal DroughtFoodStockMultiplier = 0.20m;
        public const decimal SpecialtyBuyMultiplier = 0.80m;
        public const decimal SpecialtySellMultiplier = 0.70m;
        public const decimal DriftTowardNormal = 0.12m;

        public static decimal ReputationBuyModifier(int reputation)
        {
            return 1m + Math.Min(0.20m, Math.Max(0, reputation) * 0.01m);
        }

        public static decimal WorkerTradeModifier(Project project, List<NPC> allNpcs)
        {
            var workers = project.AssignedWorkers
                .Select(id => allNpcs.Find(n => n.Id == id))
                .Where(n => n != null)
                .Cast<NPC>()
                .ToList();
            if (workers.Count == 0) return 1m;
            // Charismatic and Shy shift how well a trader actually works a market.
            int highest = workers.Max(w => TraitService.EffectiveSocialSkill(w, "Trade"));
            int extras = Math.Max(0, workers.Count(w => TraitService.EffectiveSocialSkill(w, "Trade") >= 1) - (highest >= 1 ? 1 : 0));
            return 1m + highest * 0.03m + extras * 0.01m;
        }

        public static (decimal buyMod, decimal sellMod) FoundingQualityModifiers(ProjectResultTier tier) => tier switch
        {
            ProjectResultTier.Exceptional => (1.20m, 0.85m),
            ProjectResultTier.Success => (1.00m, 1.00m),
            ProjectResultTier.Partial => (0.85m, 1.20m),
            ProjectResultTier.Failure => (0.65m, 1.40m),
            _ => (1.00m, 1.00m)
        };

        /// <summary>
        /// Gold-per-unit fair value from the stronghold economy: farm food ~5/worker vs ~2g salary,
        /// quarry stone ~3/worker, mine iron 1/worker, wood is scarce (greenhouse 1 at L2),
        /// luxury is a high-level 1/week bonus next to 12–18g production.
        /// </summary>
        public static decimal BaseGoldValue(ResourceType type) => type switch
        {
            ResourceType.Gold => 1m,
            ResourceType.Food => 1m,
            ResourceType.Wood => 2m,
            ResourceType.Stone => 1m,
            ResourceType.Iron => 2m,
            ResourceType.Luxury => 4m,
            _ => 1m
        };

        public static (decimal buy, decimal sell) SettlementModifiers(SettlementType settlement) => settlement switch
        {
            SettlementType.Village => (0.85m, 0.80m),
            SettlementType.Town => (1.00m, 1.00m),
            SettlementType.City => (1.15m, 1.05m),
            SettlementType.Castle => (1.10m, 1.20m),
            _ => (1.00m, 1.00m)
        };

        public static decimal SuggestedBuyRate(SettlementType settlement, ResourceType type, bool specialty)
        {
            if (type == ResourceType.Gold) return 1m;
            var (buyMod, _) = SettlementModifiers(settlement);
            decimal rate = BaseGoldValue(type) * buyMod;
            if (specialty) rate *= SpecialtyBuyMultiplier;
            return RoundRate(rate);
        }

        public static decimal SuggestedSellRate(SettlementType settlement, ResourceType type, bool specialty, bool canBuy)
        {
            if (type == ResourceType.Gold) return 1m;
            if (!canBuy) return 0m;
            var (_, sellMod) = SettlementModifiers(settlement);
            decimal rate = BaseGoldValue(type) * sellMod;
            if (specialty) rate *= SpecialtySellMultiplier;
            return RoundRate(rate);
        }

        public static decimal RoundRate(decimal value) =>
            decimal.Round(Math.Max(0m, value), 2, MidpointRounding.AwayFromZero);

        public static int DefaultStock(SettlementType settlement, ResourceType resource, bool canBuy, bool specialty)
        {
            if (resource == ResourceType.Gold)
            {
                return settlement switch
                {
                    SettlementType.Village => 150,
                    SettlementType.Town => 400,
                    SettlementType.City => 1200,
                    SettlementType.Castle => 300,
                    _ => 200
                };
            }

            if (!canBuy && !specialty) return 0;
            int amount = resource == ResourceType.Luxury
                ? settlement switch
                {
                    SettlementType.Village => 8,
                    SettlementType.Town => 18,
                    SettlementType.City => 40,
                    SettlementType.Castle => 15,
                    _ => 12
                }
                : settlement switch
                {
                    SettlementType.Village => 30,
                    SettlementType.Town => 50,
                    SettlementType.City => 80,
                    SettlementType.Castle => 40,
                    _ => 30
                };
            if (specialty) amount = (int)(amount * 1.6);
            return amount;
        }

        public static int StockFor(TradeRoute route, ResourceType type)
        {
            if (type != ResourceType.Gold && !route.CanBuy(type))
                return 0;
            return Math.Max(0, route.GetRate(type).Available);
        }

        public static void EnsureRouteStock(TradeRoute route)
        {
            foreach (ResourceType type in Enum.GetValues<ResourceType>())
            {
                var rate = route.Rates.Find(r => r.ResourceType == type);
                if (rate == null)
                {
                    bool specialty = route.Specialty.Contains(type);
                    bool sells = type == ResourceType.Gold || specialty;
                    rate = new TradeResourceRate
                    {
                        ResourceType = type,
                        BuyRate = SuggestedBuyRate(route.SettlementType, type, specialty),
                        SellRate = SuggestedSellRate(route.SettlementType, type, specialty, sells),
                        CanBuy = sells,
                        Available = sells
                            ? DefaultStock(route.SettlementType, type, sells, specialty)
                            : 0
                    };
                    route.Rates.Add(rate);
                }
                if (rate.ResourceType == ResourceType.Gold)
                {
                    rate.BuyRate = 1m;
                    rate.SellRate = 1m;
                    rate.CanBuy = true;
                    rate.TargetBuyRate = 1m;
                    rate.TargetSellRate = 1m;
                }
                if (rate.TargetBuyRate <= 0)
                    rate.TargetBuyRate = rate.BuyRate > 0 ? rate.BuyRate : SuggestedBuyRate(route.SettlementType, type, route.Specialty.Contains(type));
                if (rate.CanBuy && rate.TargetSellRate <= 0)
                    rate.TargetSellRate = rate.SellRate > 0
                        ? rate.SellRate
                        : SuggestedSellRate(route.SettlementType, type, route.Specialty.Contains(type), true);
            }
        }

        public static decimal EconomyScale(SettlementType settlement) => settlement switch
        {
            SettlementType.Village => 1m,
            SettlementType.Castle => 1.25m,
            SettlementType.Town => 2m,
            SettlementType.City => 4m,
            _ => 1m
        };

        public static int ProduceBase(ResourceType type) => type switch
        {
            ResourceType.Food => 10,
            ResourceType.Wood => 6,
            ResourceType.Stone => 6,
            ResourceType.Iron => 3,
            ResourceType.Luxury => 2,
            _ => 0
        };

        public static int MaxStock(TradeRoute route, ResourceType type)
        {
            bool specialty = route.Specialty.Contains(type);
            bool sells = type == ResourceType.Gold || route.CanBuy(type) || specialty;
            int typical = DefaultStock(route.SettlementType, type, sells, specialty);
            if (typical <= 0)
                typical = DefaultStock(route.SettlementType, type, canBuy: true, specialty);
            return Math.Max(1, typical * 2);
        }

        public static int WeeklyProduction(TradeRoute route, ResourceType type, Stronghold? stronghold = null)
        {
            if (type == ResourceType.Gold) return 0;
            decimal scale = EconomyScale(route.SettlementType);
            int produced = 0;
            if (route.Specialty.Contains(type))
                produced = RoundUnits(ProduceBase(type) * scale);
            else if (route.CanBuy(type))
                produced = RoundUnits(ProduceBase(type) * scale * 0.25m);
            if (HasActiveEvent(stronghold, route, TradeMarketEventKind.Surplus, type))
                produced = RoundUnits(produced * 1.5m);
            if (type == ResourceType.Food && HasRouteEvent(stronghold, route, TradeMarketEventKind.Drought))
                produced = RoundUnits(produced * DroughtFoodProductionMultiplier);
            return produced;
        }

        public static int WeeklyConsumption(TradeRoute route, ResourceType type, Stronghold? stronghold = null)
        {
            if (type == ResourceType.Gold) return 0;
            decimal scale = EconomyScale(route.SettlementType);
            int consumed = 0;
            if (type == ResourceType.Food)
            {
                decimal eat = 5m * scale;
                if (route.SettlementType == SettlementType.Castle) eat *= 1.5m;
                if (route.SettlementType == SettlementType.City) eat *= 1.2m;
                consumed += RoundUnits(eat);
            }
            if (HasDemand(route, type, stronghold))
            {
                decimal use = type == ResourceType.Luxury ? 1m : type == ResourceType.Iron ? 2m : 3m;
                consumed += RoundUnits(use * scale);
            }
            if (route.Specialty.Contains(type) && type != ResourceType.Food)
                consumed += Math.Max(1, RoundUnits(ProduceBase(type) * scale * 0.20m));
            if (HasActiveEvent(stronghold, route, TradeMarketEventKind.DemandSpike, type))
                consumed = RoundUnits(consumed * 1.5m);
            return consumed;
        }

        public static bool HasDemand(TradeRoute route, ResourceType type, Stronghold? stronghold = null)
        {
            if (route.CurrentDemand.Contains(type)) return true;
            return type == ResourceType.Food && HasRouteEvent(stronghold, route, TradeMarketEventKind.Drought);
        }

        public static List<ResourceType> ActiveDemand(TradeRoute route, Stronghold? stronghold = null)
        {
            var demand = (route.CurrentDemand ?? new List<ResourceType>()).ToList();
            if (HasRouteEvent(stronghold, route, TradeMarketEventKind.Drought) && !demand.Contains(ResourceType.Food))
                demand.Add(ResourceType.Food);
            return demand;
        }

        public static string FormatDemand(TradeRoute route, Stronghold? stronghold = null)
        {
            var demand = ActiveDemand(route, stronghold);
            return demand.Count == 0 ? "—" : string.Join(", ", demand);
        }

        public static bool CanSendCaravan(TradeRoute route, Stronghold? stronghold = null)
        {
            return route.Status == TradeRouteStatus.Open
                && !route.IsOccupied
                && !HasRouteEvent(stronghold, route, TradeMarketEventKind.Quarantine);
        }

        public static string? DestinationIdForProject(Project project) =>
            project.CustomDestination?.Id ?? project.TradeDestinationId;

        public static bool HasOpenRouteTo(Stronghold? stronghold, string? destinationId)
        {
            if (stronghold?.TradeRoutes == null || string.IsNullOrEmpty(destinationId)) return false;
            return stronghold.TradeRoutes.Any(r =>
                r.DestinationId == destinationId && r.Status == TradeRouteStatus.Open);
        }

        public static bool HasPendingEstablish(Stronghold? stronghold, string? destinationId, string? exceptBuildingId = null)
        {
            if (stronghold?.Buildings == null || string.IsNullOrEmpty(destinationId)) return false;
            return stronghold.Buildings.Any(b =>
                (exceptBuildingId == null || b.Id != exceptBuildingId)
                && b.CurrentProject != null
                && b.CurrentProject.IsEstablishTradeRoute
                && DestinationIdForProject(b.CurrentProject) == destinationId);
        }

        /// <summary>
        /// Frees a route if its caravan project is gone (e.g. older saves where damage dropped the project).
        /// </summary>
        public static void ReconcileOccupancy(Stronghold? stronghold)
        {
            if (stronghold?.TradeRoutes == null || stronghold.Buildings == null) return;
            foreach (var route in stronghold.TradeRoutes)
            {
                if (string.IsNullOrEmpty(route.ActiveMissionProjectId)) continue;
                bool alive = stronghold.Buildings.Any(b =>
                    b.CurrentProject != null && b.CurrentProject.Id == route.ActiveMissionProjectId);
                if (!alive)
                    route.ActiveMissionProjectId = null;
            }
        }

        public static bool IsRouteWideEvent(TradeMarketEventKind kind) => kind is
            TradeMarketEventKind.TradeCollapse
            or TradeMarketEventKind.Drought
            or TradeMarketEventKind.Bandits
            or TradeMarketEventKind.GuildFavor
            or TradeMarketEventKind.Quarantine;

        public static bool HasRouteEvent(Stronghold? stronghold, TradeRoute route, TradeMarketEventKind kind)
        {
            if (stronghold?.TradeMarketEvents == null) return false;
            return stronghold.TradeMarketEvents.Any(e =>
                e.RouteId == route.Id && e.Kind == kind && e.WeeksRemaining > 0);
        }

        private static bool HasActiveEvent(Stronghold? stronghold, TradeRoute route, TradeMarketEventKind kind, ResourceType type)
        {
            if (stronghold?.TradeMarketEvents == null) return false;
            return stronghold.TradeMarketEvents.Any(e =>
                e.RouteId == route.Id && e.Kind == kind && e.ResourceType == type && e.WeeksRemaining > 0);
        }

        private static int RoundUnits(decimal value) =>
            (int)decimal.Round(Math.Max(0m, value), 0, MidpointRounding.AwayFromZero);

        private static int StockJitter(TradeRoute route, ResourceType type, int current, Stronghold? stronghold = null)
        {
            if (type != ResourceType.Gold
                && !route.CanBuy(type)
                && !route.Specialty.Contains(type)
                && !HasDemand(route, type, stronghold)
                && current <= 0)
            {
                return 0;
            }

            if (type == ResourceType.Food && HasRouteEvent(stronghold, route, TradeMarketEventKind.Drought))
                return Random.Shared.Next(-2, 1);

            return Random.Shared.Next(-2, 3);
        }

        public static void TickSettlementStocks(Stronghold stronghold)
        {
            foreach (var route in stronghold.TradeRoutes.Where(r => r.Status == TradeRouteStatus.Open))
            {
                EnsureRouteStock(route);
                foreach (ResourceType type in Enum.GetValues<ResourceType>())
                {
                    var rate = route.GetRate(type);
                    if (route.Rates.All(r => r.ResourceType != type))
                        route.Rates.Add(rate);

                    if (type == ResourceType.Gold)
                    {
                        int target = DefaultStock(route.SettlementType, ResourceType.Gold, true, false);
                        int next = RoundUnits(rate.Available + (target - rate.Available) * DriftTowardNormal);
                        next += StockJitter(route, type, rate.Available, stronghold);
                        rate.Available = Math.Clamp(next, 0, MaxStock(route, type));
                        continue;
                    }

                    int nextStock = rate.Available
                        + WeeklyProduction(route, type, stronghold)
                        - WeeklyConsumption(route, type, stronghold)
                        + StockJitter(route, type, rate.Available, stronghold);
                    rate.Available = Math.Clamp(nextStock, 0, MaxStock(route, type));
                }
            }
        }

        public static void ApplyMissionStock(
            TradeRoute route,
            Project project,
            List<ResourceCost> yieldGranted,
            List<ResourceCost>? returnedCargo = null)
        {
            void Adjust(ResourceType type, int delta)
            {
                var rate = route.Rates.Find(r => r.ResourceType == type);
                if (rate == null)
                {
                    rate = route.GetRate(type);
                    route.Rates.Add(rate);
                }
                rate.Available = Math.Clamp(rate.Available + delta, 0, MaxStock(route, type));
            }

            foreach (var sent in project.CargoOut ?? new List<ResourceCost>())
            {
                int returned = returnedCargo?.Find(c => c.ResourceType == sent.ResourceType)?.Amount ?? 0;
                int kept = Math.Max(0, sent.Amount - Math.Max(0, returned));
                if (kept > 0)
                    Adjust(sent.ResourceType, kept);
            }

            foreach (var bought in yieldGranted ?? new List<ResourceCost>())
                Adjust(bought.ResourceType, -bought.Amount);
        }

        public static TradeRoute CreateRoute(TradeDestination destination, ProjectResultTier quality, int week, int year)
        {
            var (buyMod, sellMod) = FoundingQualityModifiers(quality);
            var route = new TradeRoute
            {
                DestinationId = destination.Id,
                Name = destination.Name,
                SettlementType = destination.SettlementType,
                DistanceWeeks = Math.Max(1, destination.DistanceWeeks),
                FoundingQuality = quality,
                Specialty = destination.Specialty?.ToList() ?? new List<ResourceType>(),
                CurrentDemand = destination.DefaultDemand?.ToList() ?? new List<ResourceType>(),
                Status = TradeRouteStatus.Open,
                EstablishedWeek = week,
                EstablishedYear = year
            };

            TradeDestinationService.EnsureDefaultRates(destination);
            foreach (var rate in destination.Resources)
            {
                bool specialty = route.Specialty.Contains(rate.ResourceType);
                bool gold = rate.ResourceType == ResourceType.Gold;
                bool sells = gold || rate.CanBuy || specialty;
                decimal targetBuy = gold ? 1m : (rate.BuyRate > 0 ? rate.BuyRate : SuggestedBuyRate(destination.SettlementType, rate.ResourceType, specialty));
                decimal targetSell = gold ? 1m : (sells
                    ? (rate.SellRate > 0 ? rate.SellRate : SuggestedSellRate(destination.SettlementType, rate.ResourceType, specialty, true))
                    : 0m);
                route.Rates.Add(new TradeResourceRate
                {
                    ResourceType = rate.ResourceType,
                    BuyRate = gold ? 1m : RoundRate(Math.Max(0.15m, targetBuy * buyMod)),
                    SellRate = gold ? 1m : (sells ? RoundRate(Math.Max(0.15m, targetSell * sellMod)) : 0m),
                    TargetBuyRate = targetBuy,
                    TargetSellRate = targetSell,
                    CanBuy = sells,
                    Available = rate.Available > 0
                        ? rate.Available
                        : DefaultStock(destination.SettlementType, rate.ResourceType, rate.CanBuy, specialty)
                });
            }

            return route;
        }

        /// <summary>Whole-number gold total. A positive amount never rounds to 0.</summary>
        public static int RoundTotal(decimal value)
        {
            if (value <= 0m) return 0;
            int rounded = (int)decimal.Round(value, 0, MidpointRounding.AwayFromZero);
            return rounded == 0 ? 1 : rounded;
        }

        public static decimal RoundMoney(decimal value) => RoundTotal(value);

        public static decimal ReturnCost(TradeRoute route, Stronghold stronghold, IEnumerable<ResourceCost> bringBack)
        {
            decimal value = 0m;
            foreach (var item in bringBack.Where(c => c.Amount > 0))
            {
                if (item.ResourceType == ResourceType.Gold)
                    value += item.Amount;
                else
                    value += item.Amount * EffectiveSellRate(route, item.ResourceType, stronghold);
            }
            return value;
        }

        public static bool TransactionIsBalanced(decimal outbound, decimal returnCost)
        {
            return RoundTotal(outbound) == RoundTotal(returnCost);
        }

        public static decimal EffectiveBuyRate(TradeRoute route, ResourceType type, Stronghold stronghold, Project? project, List<NPC> allNpcs)
        {
            if (type == ResourceType.Gold) return 1m;
            decimal rate = route.GetRate(type).BuyRate;
            if (HasDemand(route, type, stronghold))
                rate *= DemandBuyMultiplier;
            var ev = stronghold.TradeMarketEvents.Find(e =>
                e.RouteId == route.Id && e.ResourceType == type && e.Kind == TradeMarketEventKind.DemandSpike && e.WeeksRemaining > 0);
            if (ev != null)
                rate *= EventDemandMultiplier;
            rate *= ReputationBuyModifier(stronghold.Reputation);
            if (project != null)
                rate *= WorkerTradeModifier(project, allNpcs);
            return rate;
        }

        public static decimal EffectiveSellRate(TradeRoute route, ResourceType type, Stronghold stronghold)
        {
            if (type == ResourceType.Gold) return 1m;
            if (!route.CanBuy(type)) return 0m;
            decimal rate = route.GetRate(type).SellRate;
            var ev = stronghold.TradeMarketEvents.Find(e =>
                e.RouteId == route.Id && e.ResourceType == type && e.Kind == TradeMarketEventKind.Surplus && e.WeeksRemaining > 0);
            if (ev != null)
                rate *= EventSurplusSellMultiplier;
            return Math.Max(0.15m, rate);
        }

        public static decimal OutboundValue(TradeRoute route, Stronghold stronghold, Project project, List<NPC> allNpcs)
        {
            decimal value = 0m;
            foreach (var cargo in project.CargoOut.Where(c => c.Amount > 0))
            {
                if (cargo.ResourceType == ResourceType.Gold)
                    value += cargo.Amount;
                else
                    value += cargo.Amount * EffectiveBuyRate(route, cargo.ResourceType, stronghold, project, allNpcs);
            }
            return value;
        }

        public static List<ResourceCost> ComputeExpectedReturn(
            TradeRoute route,
            Stronghold stronghold,
            Project project,
            List<NPC> allNpcs,
            List<ResourceType> requestedReturns)
        {
            var result = new List<ResourceCost>();
            var requested = requestedReturns
                .Where(t => route.CanBuy(t) || t == ResourceType.Gold)
                .Distinct()
                .ToList();
            if (requested.Count == 0) return result;

            decimal value = OutboundValue(route, stronghold, project, allNpcs);
            if (value <= 0) return result;

            decimal share = value / requested.Count;
            foreach (var type in requested)
            {
                decimal sell = EffectiveSellRate(route, type, stronghold);
                int amount = (int)Math.Ceiling(share / Math.Max(0.15m, sell));
                if (amount > 0)
                    result.Add(new ResourceCost { ResourceType = type, Amount = amount });
            }
            return result;
        }

        public static (List<ResourceCost> packed, List<ResourceCost> returnedCargo, string note) PackReturn(
            TradeRoute route,
            Stronghold stronghold,
            Project project,
            List<NPC> allNpcs)
        {
            var packed = new Dictionary<ResourceType, int>();
            void AddPacked(ResourceType type, int amount)
            {
                if (amount <= 0) return;
                packed.TryGetValue(type, out int current);
                packed[type] = current + amount;
            }

            var shorts = new List<string>();
            decimal goodsShortValue = 0m;
            int askedGold = 0;
            var expected = project.ExpectedReturn ?? new List<ResourceCost>();
            foreach (var item in expected.Where(c => c.Amount > 0))
            {
                if (item.ResourceType == ResourceType.Gold)
                {
                    askedGold += item.Amount;
                    continue;
                }

                int stock = StockFor(route, item.ResourceType);
                int take = Math.Min(item.Amount, stock);
                AddPacked(item.ResourceType, take);
                int missing = item.Amount - take;
                if (missing <= 0) continue;

                decimal rate = EffectiveSellRate(route, item.ResourceType, stronghold);
                if (rate <= 0m)
                    rate = BaseGoldValue(item.ResourceType);
                goodsShortValue += missing * rate;
                shorts.Add($"{missing} {item.ResourceType}");
            }

            int goldOwed = askedGold + (goodsShortValue > 0m ? RoundTotal(goodsShortValue) : 0);
            int goldHave = StockFor(route, ResourceType.Gold);
            int goldPaid = Math.Min(goldOwed, goldHave);
            AddPacked(ResourceType.Gold, goldPaid);

            decimal unpaid = goldOwed - goldPaid;
            var returnedCargo = unpaid > 0
                ? TakeCargoWorth(route, stronghold, project, allNpcs, unpaid)
                : new List<ResourceCost>();

            string note = string.Empty;
            if (shorts.Count > 0 || returnedCargo.Count > 0 || goldPaid < goldOwed)
            {
                var parts = new List<string>();
                if (shorts.Count > 0)
                    parts.Add($"{route.Name} did not have {string.Join(", ", shorts)} ready");
                else
                    parts.Add($"{route.Name} could not cover the payment");

                int goldForGoods = Math.Max(0, goldPaid - askedGold);
                if (goldForGoods > 0 && goodsShortValue > 0m)
                    parts.Add($"{goldForGoods} Gold was paid instead");
                if (returnedCargo.Count > 0)
                    parts.Add($"{ProjectResolutionService.FormatCosts(returnedCargo)} was returned unpaid");
                note = string.Join(". ", parts) + ".";
            }

            var packedList = packed
                .Where(kv => kv.Value > 0)
                .Select(kv => new ResourceCost { ResourceType = kv.Key, Amount = kv.Value })
                .ToList();
            return (packedList, returnedCargo, note);
        }

        private static List<ResourceCost> TakeCargoWorth(
            TradeRoute route,
            Stronghold stronghold,
            Project project,
            List<NPC> allNpcs,
            decimal unpaid)
        {
            var leftover = (project.CargoOut ?? new List<ResourceCost>())
                .Where(c => c.Amount > 0)
                .Select(c => new ResourceCost { ResourceType = c.ResourceType, Amount = c.Amount })
                .ToList();
            var taken = new Dictionary<ResourceType, int>();
            void Take(ResourceType type, int amount)
            {
                if (amount <= 0) return;
                taken.TryGetValue(type, out int current);
                taken[type] = current + amount;
            }

            var gold = leftover.Find(c => c.ResourceType == ResourceType.Gold);
            if (gold != null && unpaid > 0m)
            {
                int give = Math.Min(gold.Amount, (int)Math.Ceiling(unpaid));
                Take(ResourceType.Gold, give);
                gold.Amount -= give;
                unpaid -= give;
            }

            foreach (var item in leftover
                .Where(c => c.ResourceType != ResourceType.Gold && c.Amount > 0)
                .OrderBy(c => EffectiveBuyRate(route, c.ResourceType, stronghold, project, allNpcs)))
            {
                decimal rate = EffectiveBuyRate(route, item.ResourceType, stronghold, project, allNpcs);
                if (rate <= 0m) rate = BaseGoldValue(item.ResourceType);
                while (unpaid > 0m && item.Amount > 0)
                {
                    item.Amount--;
                    Take(item.ResourceType, 1);
                    unpaid -= rate;
                }
                if (unpaid <= 0m) break;
            }

            return taken
                .Where(kv => kv.Value > 0)
                .Select(kv => new ResourceCost { ResourceType = kv.Key, Amount = kv.Value })
                .ToList();
        }

        public static (decimal fraction, string mishap) RollCargoLoss() =>
            RollCargoLoss(null, null);

        public static (decimal fraction, string mishap) RollCargoLoss(TradeRoute? route, Stronghold? stronghold)
        {
            int roll = Random.Shared.Next(1, 101);
            bool bandits = route != null && HasRouteEvent(stronghold, route, TradeMarketEventKind.Bandits);
            if (bandits)
            {
                if (roll <= 12)
                    return (0m, "Bandits hit the caravan on the road. The cargo is gone.");
                if (roll <= 45)
                    return (0.5m, "Bandits took a cut. Some wagons never arrived.");
                return (1m, string.Empty);
            }

            if (roll <= 3)
                return (0m, "The caravan was robbed on the road. The cargo is gone.");
            if (roll <= 15)
                return (0.5m, "Weather and a toll took a cut. Some wagons never arrived.");
            return (1m, string.Empty);
        }

        public static List<ResourceCost> ApplyLoss(List<ResourceCost> expected, decimal fraction)
        {
            if (fraction <= 0) return new List<ResourceCost>();
            if (fraction >= 1m) return expected.Select(c => new ResourceCost { ResourceType = c.ResourceType, Amount = c.Amount }).ToList();
            var result = new List<ResourceCost>();
            foreach (var cost in expected)
            {
                int amount = (int)Math.Ceiling(cost.Amount * fraction);
                if (amount > 0)
                    result.Add(new ResourceCost { ResourceType = cost.ResourceType, Amount = amount });
            }
            return result;
        }

        public static void DriftOpenRoutes(Stronghold stronghold)
        {
            var catalog = TradeDestinationService.GetInstance();
            foreach (var route in stronghold.TradeRoutes.Where(r => r.Status == TradeRouteStatus.Open))
            {
                EnsureRouteStock(route);
                var dest = catalog.GetById(route.DestinationId);
                foreach (var rate in route.Rates)
                {
                    if (rate.ResourceType == ResourceType.Gold)
                    {
                        rate.BuyRate = 1m;
                        rate.SellRate = 1m;
                        continue;
                    }

                    var destRate = dest?.Resources.Find(r => r.ResourceType == rate.ResourceType);
                    decimal targetBuy = rate.TargetBuyRate > 0
                        ? rate.TargetBuyRate
                        : (destRate != null && destRate.BuyRate > 0 ? destRate.BuyRate : rate.BuyRate);
                    if (rate.TargetBuyRate <= 0)
                        rate.TargetBuyRate = targetBuy;

                    if (rate.CanBuy)
                    {
                        decimal targetSell = rate.TargetSellRate > 0
                            ? rate.TargetSellRate
                            : (destRate != null && destRate.SellRate > 0 ? destRate.SellRate : rate.SellRate);
                        if (rate.TargetSellRate <= 0)
                            rate.TargetSellRate = targetSell;
                    }
                    else
                    {
                        rate.SellRate = 0m;
                        rate.TargetSellRate = 0m;
                    }

                    if (!HasRouteEvent(stronghold, route, TradeMarketEventKind.TradeCollapse)
                        && !HasRouteEvent(stronghold, route, TradeMarketEventKind.GuildFavor))
                    {
                        rate.BuyRate = DriftToward(rate.BuyRate, rate.TargetBuyRate > 0 ? rate.TargetBuyRate : targetBuy);
                        if (rate.CanBuy)
                            rate.SellRate = DriftToward(rate.SellRate, rate.TargetSellRate > 0 ? rate.TargetSellRate : rate.SellRate);
                    }
                }

                if (HasRouteEvent(stronghold, route, TradeMarketEventKind.TradeCollapse))
                    ApplyFoundingTierRates(route, ProjectResultTier.Failure);
                else if (HasRouteEvent(stronghold, route, TradeMarketEventKind.GuildFavor))
                    ApplyFoundingTierRates(route, ProjectResultTier.Exceptional);

                if (!HasRouteEvent(stronghold, route, TradeMarketEventKind.Drought)
                    && Random.Shared.NextDouble() < 0.10)
                {
                    var options = Enum.GetValues<ResourceType>()
                        .Where(t => t != ResourceType.Gold && !route.Specialty.Contains(t))
                        .ToList();
                    if (options.Count > 0)
                    {
                        route.CurrentDemand = new List<ResourceType> { options[Random.Shared.Next(options.Count)] };
                    }
                }
            }
        }

        private static decimal DriftToward(decimal current, decimal target)
        {
            if (target <= 0m) return RoundRate(current);
            decimal next = current + (target - current) * DriftTowardNormal;
            decimal jitter = 1m + (decimal)(Random.Shared.NextDouble() * 0.04 - 0.02);
            next *= jitter;
            decimal min = Math.Max(0.15m, target * 0.40m);
            decimal max = Math.Max(min + 0.05m, target * 2.00m);
            return RoundRate(Math.Clamp(next, min, max));
        }

        public static void ApplyFailureRates(TradeRoute route) =>
            ApplyFoundingTierRates(route, ProjectResultTier.Failure);

        public static void ApplyFoundingTierRates(TradeRoute route, ProjectResultTier tier)
        {
            var (buyMod, sellMod) = FoundingQualityModifiers(tier);
            foreach (var rate in route.Rates)
            {
                if (rate.ResourceType == ResourceType.Gold)
                {
                    rate.BuyRate = 1m;
                    rate.SellRate = 1m;
                    continue;
                }

                decimal targetBuy = rate.TargetBuyRate > 0 ? rate.TargetBuyRate : rate.BuyRate;
                rate.BuyRate = RoundRate(Math.Max(0.15m, targetBuy * buyMod));
                if (rate.CanBuy)
                {
                    decimal targetSell = rate.TargetSellRate > 0 ? rate.TargetSellRate : rate.SellRate;
                    rate.SellRate = RoundRate(Math.Max(0.15m, targetSell * sellMod));
                }
                else
                {
                    rate.SellRate = 0m;
                }
            }
        }

        public static void ApplyEventImmediateEffects(TradeRoute route, TradeMarketEvent ev)
        {
            if (ev.Kind == TradeMarketEventKind.TradeCollapse)
            {
                ApplyFoundingTierRates(route, ProjectResultTier.Failure);
                return;
            }

            if (ev.Kind == TradeMarketEventKind.GuildFavor)
            {
                ApplyFoundingTierRates(route, ProjectResultTier.Exceptional);
                return;
            }

            if (ev.Kind == TradeMarketEventKind.Drought)
            {
                EnsureRouteStock(route);
                var food = route.GetRate(ResourceType.Food);
                food.Available = Math.Max(0, (int)Math.Floor(food.Available * DroughtFoodStockMultiplier));
            }
        }

        public static TradeMarketEvent? TryCreateMarketEvent(Stronghold stronghold)
        {
            var open = stronghold.TradeRoutes.Where(r => r.Status == TradeRouteStatus.Open).ToList();
            if (open.Count == 0) return null;
            if (Random.Shared.NextDouble() >= 0.10) return null;

            var route = open[Random.Shared.Next(open.Count)];
            bool demandSpike = Random.Shared.Next(2) == 0;
            ResourceType resource;
            if (demandSpike)
            {
                var options = Enum.GetValues<ResourceType>().Where(t => t != ResourceType.Gold).ToList();
                resource = options[Random.Shared.Next(options.Count)];
            }
            else
            {
                var options = route.Specialty.Count > 0
                    ? route.Specialty
                    : Enum.GetValues<ResourceType>().Where(t => t != ResourceType.Gold).ToList();
                resource = options[Random.Shared.Next(options.Count)];
            }

            var ev = new TradeMarketEvent
            {
                RouteId = route.Id,
                RouteName = route.Name,
                ResourceType = resource,
                Kind = demandSpike ? TradeMarketEventKind.DemandSpike : TradeMarketEventKind.Surplus,
                WeeksRemaining = 3
            };
            stronghold.TradeMarketEvents.Add(ev);
            return ev;
        }

        public static void TickMarketEvents(Stronghold stronghold)
        {
            foreach (var ev in stronghold.TradeMarketEvents.ToList())
            {
                ev.WeeksRemaining--;
                if (ev.WeeksRemaining <= 0)
                    stronghold.TradeMarketEvents.Remove(ev);
            }
        }

        public static string CloseRoute(Stronghold stronghold, string routeId)
        {
            var route = stronghold.TradeRoutes.Find(r => r.Id == routeId);
            if (route == null) return "Route not found.";
            if (route.Status == TradeRouteStatus.Closed) return "Route is already closed.";
            if (route.IsOccupied) return "A trade mission is still using this route.";
            route.Status = TradeRouteStatus.Closed;
            stronghold.Journal.Add(new JournalEntry(
                stronghold.CurrentWeek,
                stronghold.YearsSinceFoundation,
                JournalEntryType.TradeRouteClosed,
                $"Trade route to {route.Name} closed",
                $"The trade route to {route.Name} has been closed. It can be re-established later."));
            return string.Empty;
        }
    }
}
