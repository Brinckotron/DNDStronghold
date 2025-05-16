using System;
using System.Collections.Generic;

namespace DNDStrongholdApp.Models
{
    public class Resource
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public ResourceType Type { get; set; }
        public int Amount { get; set; }
        public int WeeklyProduction { get; set; }
        public int WeeklyConsumption { get; set; }
        public List<ResourceSource> Sources { get; set; } = new List<ResourceSource>();

        public int NetWeeklyChange => WeeklyProduction - WeeklyConsumption;

        public void UpdateWeeklyRates()
        {
            WeeklyProduction = 0;
            WeeklyConsumption = 0;

            foreach (var source in Sources)
            {
                if (source.IsProduction)
                {
                    WeeklyProduction += source.Amount;
                }
                else
                {
                    WeeklyConsumption += source.Amount;
                }
            }
        }

        public void ApplyWeeklyChange()
        {
            Amount += NetWeeklyChange;
            // Ensure we don't go negative
            if (Amount < 0)
            {
                Amount = 0;
            }
        }
    }

    public enum ResourceType
    {
        Gold,
        Food,
        Wood,
        Stone,
        Iron,
        Luxury,
        Special
    }

    public class ResourceSource
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public ResourceSourceType SourceType { get; set; }
        public string SourceId { get; set; } = string.Empty;
        public string SourceName { get; set; } = string.Empty;
        public int Amount { get; set; }
        public bool IsProduction { get; set; }
    }

    public enum ResourceSourceType
    {
        Building,
        Mission,
        Event,
        Manual
    }
} 