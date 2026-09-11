using System;
using System.Collections.Generic;

namespace DNDStrongholdApp.Models
{
    public enum ProjectOutcomeType
    {
        Table,
        Hybrid,
        InApp
    }

    public enum ProjectRollMode
    {
        None,
        Optional,
        Required
    }

    public enum DifficultyTier
    {
        Trivial,
        Easy,
        Medium,
        Hard,
        VeryHard,
        Legendary
    }

    public enum ProjectResultTier
    {
        Exceptional,
        Success,
        Partial,
        Failure
    }

    public static class DifficultyTierExtensions
    {
        public static int GetDC(this DifficultyTier tier) => tier switch
        {
            DifficultyTier.Trivial => 8,
            DifficultyTier.Easy => 11,
            DifficultyTier.Medium => 14,
            DifficultyTier.Hard => 18,
            DifficultyTier.VeryHard => 22,
            DifficultyTier.Legendary => 26,
            _ => 14
        };

        public static string DisplayName(this DifficultyTier tier) => tier switch
        {
            DifficultyTier.VeryHard => "Very Hard",
            _ => tier.ToString()
        };
    }

    public class ProjectCompletionResult
    {
        public ProjectResultTier Tier { get; set; } = ProjectResultTier.Success;
        public int? D20Total { get; set; }
        public int Bonus { get; set; }
        public int DC { get; set; }
        public List<ResourceCost> YieldGranted { get; set; } = new List<ResourceCost>();
        public string Summary { get; set; } = string.Empty;
        public string Mishap { get; set; } = string.Empty;
        public TradeRoute? CreatedRoute { get; set; }
        public bool RouteCreated => CreatedRoute != null;
    }
}
