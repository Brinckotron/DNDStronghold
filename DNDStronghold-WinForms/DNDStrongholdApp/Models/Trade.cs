using System;
using System.Collections.Generic;
using System.Linq;

namespace DNDStrongholdApp.Models
{
    public enum SettlementType
    {
        Village,
        Town,
        City,
        Castle
    }

    public enum TradeRouteStatus
    {
        Open,
        Closed
    }

    public enum TradeMarketEventKind
    {
        DemandSpike,
        Surplus,
        Notice,
        TradeCollapse,
        Drought,
        Bandits,
        GuildFavor,
        Quarantine
    }

    public class TradeResourceRate
    {
        public ResourceType ResourceType { get; set; }
        public decimal BuyRate { get; set; } = 1m;
        public decimal SellRate { get; set; } = 1m;
        public decimal TargetBuyRate { get; set; }
        public decimal TargetSellRate { get; set; }
        public bool CanBuy { get; set; }
        public int Available { get; set; }
    }

    public class TradeDestination
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public SettlementType SettlementType { get; set; } = SettlementType.Village;
        public int DistanceWeeks { get; set; } = 1;
        public int MinReputation { get; set; } = 0;
        public List<ResourceType> Specialty { get; set; } = new List<ResourceType>();
        public List<ResourceType> DefaultDemand { get; set; } = new List<ResourceType>();
        public List<TradeResourceRate> Resources { get; set; } = new List<TradeResourceRate>();
        public string Notes { get; set; } = string.Empty;
    }

    public class TradeDestinationCatalog
    {
        public List<TradeDestination> Destinations { get; set; } = new List<TradeDestination>();
    }

    public class TradeRoute
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string DestinationId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public SettlementType SettlementType { get; set; }
        public int DistanceWeeks { get; set; } = 1;
        public ProjectResultTier FoundingQuality { get; set; } = ProjectResultTier.Success;
        public List<TradeResourceRate> Rates { get; set; } = new List<TradeResourceRate>();
        public List<ResourceType> CurrentDemand { get; set; } = new List<ResourceType>();
        public List<ResourceType> Specialty { get; set; } = new List<ResourceType>();
        public TradeRouteStatus Status { get; set; } = TradeRouteStatus.Open;
        public string? ActiveMissionProjectId { get; set; }
        public int EstablishedWeek { get; set; }
        public int EstablishedYear { get; set; }
        public string Notes { get; set; } = string.Empty;

        public bool IsOccupied => !string.IsNullOrEmpty(ActiveMissionProjectId);

        public TradeResourceRate GetRate(ResourceType type)
        {
            var rate = Rates.Find(r => r.ResourceType == type);
            if (rate != null) return rate;
            return new TradeResourceRate
            {
                ResourceType = type,
                BuyRate = 1m,
                SellRate = 1m,
                CanBuy = type == ResourceType.Gold,
                Available = 0
            };
        }

        public bool CanBuy(ResourceType type)
        {
            if (type == ResourceType.Gold) return true;
            return GetRate(type).CanBuy;
        }
    }

    public class TradeMarketEvent
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string RouteId { get; set; } = string.Empty;
        public string RouteName { get; set; } = string.Empty;
        public ResourceType ResourceType { get; set; }
        public TradeMarketEventKind Kind { get; set; }
        public int WeeksRemaining { get; set; } = 3;
        public string Notes { get; set; } = string.Empty;

        public string DefaultSummary => Kind switch
        {
            TradeMarketEventKind.DemandSpike => $"{RouteName} is paying well for {ResourceType}",
            TradeMarketEventKind.Surplus => $"{RouteName} has a surplus of {ResourceType}",
            TradeMarketEventKind.TradeCollapse => $"{RouteName}'s trade has collapsed",
            TradeMarketEventKind.Drought => $"{RouteName} is in drought",
            TradeMarketEventKind.Bandits => $"Bandits on the road to {RouteName}",
            TradeMarketEventKind.GuildFavor => $"{RouteName}'s merchant guild favors you",
            TradeMarketEventKind.Quarantine => $"{RouteName} is under quarantine",
            _ => string.IsNullOrWhiteSpace(Notes) ? $"{RouteName}: market notice" : Notes.Trim()
        };

        public string Summary
        {
            get
            {
                if (string.IsNullOrWhiteSpace(Notes))
                    return DefaultSummary;
                string first = Notes.Trim();
                int breakAt = first.IndexOfAny(new[] { '\r', '\n' });
                return breakAt >= 0 ? first[..breakAt] : first;
            }
        }
    }
}
