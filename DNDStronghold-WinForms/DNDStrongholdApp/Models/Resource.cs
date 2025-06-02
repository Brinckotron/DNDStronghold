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
        Luxury
    }

    public static class ResourceTypeExtensions
    {
        public static Color GetColor(this ResourceType resourceType)
        {
            return resourceType switch
            {
                ResourceType.Gold => Color.FromArgb(255, 215, 0),      // Golden
                ResourceType.Food => Color.FromArgb(76, 187, 23),      // Green
                ResourceType.Wood => Color.FromArgb(139, 69, 19),      // Brown
                ResourceType.Stone => Color.FromArgb(169, 169, 169),   // Gray
                ResourceType.Iron => Color.FromArgb(176, 196, 222),    // Steel Blue
                ResourceType.Luxury => Color.FromArgb(106, 90, 205), // Slate Blue
                _ => Color.Black
            };
        }

        public static string GetSymbol(this ResourceType resourceType)
        {
            return resourceType switch
            {
                ResourceType.Gold => "🪙",      // Coin
                ResourceType.Food => "🌾",      // Wheat
                ResourceType.Wood => "🪵",      // Wood
                ResourceType.Stone => "🪨",     // Rock
                ResourceType.Iron => "⚒️",      // Hammer and Pick
                ResourceType.Luxury => "💎",   // Diamond
                _ => "❓"
            };
        }
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