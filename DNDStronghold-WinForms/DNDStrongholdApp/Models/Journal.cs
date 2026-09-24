using System;
using System.Collections.Generic;

namespace DNDStrongholdApp.Models
{
    public class JournalEntry
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public int Week { get; set; }
        public int Year { get; set; }
        public string Date { get; set; } = string.Empty; // For display purposes
        public JournalEntryType Type { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<RelatedEntity> RelatedEntities { get; set; } = new List<RelatedEntity>();
        public ImportanceLevel Importance { get; set; } = ImportanceLevel.Medium;

        public JournalEntry()
        {
        }

        public JournalEntry(int week, int year, JournalEntryType type, string title, string description)
        {
            Week = week;
            Year = year;
            Type = type;
            Title = title;
            Description = description;
            Date = $"Week {week}, Year {year}";
        }

        public void AddRelatedEntity(EntityType entityType, string entityId)
        {
            RelatedEntities.Add(new RelatedEntity
            {
                Type = entityType,
                Id = entityId
            });
        }
    }

    public class RelatedEntity
    {
        public EntityType Type { get; set; }
        public string Id { get; set; } = string.Empty;
    }

    public enum EntityType
    {
        Building,
        NPC,
        Mission,
        Resource
    }

    public enum JournalEntryType
    {
        BuildingPlanned,
        BuildingStart,
        BuildingComplete,
        BuildingDamaged,
        BuildingRepairStarted,
        BuildingRepaired,
        BuildingRepairComplete,
        BuildingUpgradeComplete,
        NPCRecruited,
        NPCAssigned,
        ResourceChange,
        Event,
        MissionStart,
        MissionComplete,
        WeeklyReport,
        ProjectComplete,
        TradeRouteEstablished,
        TradeRouteClosed,
        TradeMarketEvent,
        Raid
    }

    public enum ImportanceLevel
    {
        Low,
        Medium,
        High
    }

    public enum JournalKindGroup
    {
        All,
        Buildings,
        People,
        Resources,
        Trade,
        ProjectsAndMissions,
        Raids,
        Other
    }

    public static class JournalDisplay
    {
        public static string KindLabel(JournalEntryType type) => type switch
        {
            JournalEntryType.BuildingPlanned => "Building planned",
            JournalEntryType.BuildingStart => "Construction started",
            JournalEntryType.BuildingComplete => "Building complete",
            JournalEntryType.BuildingDamaged => "Building damaged",
            JournalEntryType.BuildingRepairStarted => "Repair started",
            JournalEntryType.BuildingRepaired => "Building repaired",
            JournalEntryType.BuildingRepairComplete => "Repair complete",
            JournalEntryType.BuildingUpgradeComplete => "Upgrade complete",
            JournalEntryType.NPCRecruited => "NPC recruited",
            JournalEntryType.NPCAssigned => "NPC assigned",
            JournalEntryType.ResourceChange => "Resource change",
            JournalEntryType.Event => "Event",
            JournalEntryType.MissionStart => "Mission started",
            JournalEntryType.MissionComplete => "Mission complete",
            JournalEntryType.WeeklyReport => "Weekly report",
            JournalEntryType.ProjectComplete => "Project complete",
            JournalEntryType.TradeRouteEstablished => "Trade route opened",
            JournalEntryType.TradeRouteClosed => "Trade route closed",
            JournalEntryType.TradeMarketEvent => "Market event",
            JournalEntryType.Raid => "Raid",
            _ => type.ToString()
        };

        public static string KindGroupLabel(JournalKindGroup group) => group switch
        {
            JournalKindGroup.All => "All",
            JournalKindGroup.Buildings => "Buildings",
            JournalKindGroup.People => "People",
            JournalKindGroup.Resources => "Resources",
            JournalKindGroup.Trade => "Trade",
            JournalKindGroup.ProjectsAndMissions => "Projects & missions",
            JournalKindGroup.Raids => "Raids",
            JournalKindGroup.Other => "Other",
            _ => group.ToString()
        };

        public static JournalKindGroup GetKindGroup(JournalEntry entry)
        {
            switch (entry.Type)
            {
                case JournalEntryType.BuildingPlanned:
                case JournalEntryType.BuildingStart:
                case JournalEntryType.BuildingComplete:
                case JournalEntryType.BuildingDamaged:
                case JournalEntryType.BuildingRepairStarted:
                case JournalEntryType.BuildingRepaired:
                case JournalEntryType.BuildingRepairComplete:
                case JournalEntryType.BuildingUpgradeComplete:
                    return JournalKindGroup.Buildings;

                case JournalEntryType.NPCRecruited:
                case JournalEntryType.NPCAssigned:
                    return JournalKindGroup.People;

                case JournalEntryType.ResourceChange:
                    return JournalKindGroup.Resources;

                case JournalEntryType.TradeRouteEstablished:
                case JournalEntryType.TradeRouteClosed:
                case JournalEntryType.TradeMarketEvent:
                    return JournalKindGroup.Trade;

                case JournalEntryType.ProjectComplete:
                case JournalEntryType.MissionStart:
                case JournalEntryType.MissionComplete:
                    return JournalKindGroup.ProjectsAndMissions;

                case JournalEntryType.Raid:
                    return JournalKindGroup.Raids;

                case JournalEntryType.Event:
                    return IsPeopleEvent(entry) ? JournalKindGroup.People : JournalKindGroup.Other;

                default:
                    return JournalKindGroup.Other;
            }
        }

        private static bool IsPeopleEvent(JournalEntry entry)
        {
            string title = entry.Title ?? string.Empty;
            return title.Contains("Hunger", StringComparison.OrdinalIgnoreCase)
                || title.Contains("Starvation", StringComparison.OrdinalIgnoreCase)
                || title.Contains("NPC Death", StringComparison.OrdinalIgnoreCase)
                || title.Contains("NPC Abandonment", StringComparison.OrdinalIgnoreCase)
                || title.Contains("Food Situation", StringComparison.OrdinalIgnoreCase)
                || title.Contains("Steward", StringComparison.OrdinalIgnoreCase)
                || title.Contains("Emergency Rationing", StringComparison.OrdinalIgnoreCase);
        }
    }

    public class WeeklyReport
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public int Week { get; set; }
        public int Year { get; set; }
        public Season Season { get; set; }

        public List<CompletedProject> CompletedProjects { get; set; } = new List<CompletedProject>();
        public List<ResourceChange> ResourceChanges { get; set; } = new List<ResourceChange>();
        public IncomeExpenseSummary IncomeExpenseSummary { get; set; } = new IncomeExpenseSummary();
        public List<NPCStatusChange> NPCStatusChanges { get; set; } = new List<NPCStatusChange>();
        public List<UpcomingCompletion> UpcomingCompletions { get; set; } = new List<UpcomingCompletion>();
        public List<EventSummary> Events { get; set; } = new List<EventSummary>();

        public int MoraleBefore { get; set; }
        public int MoraleAfter { get; set; }
        public int PopulationBefore { get; set; }
        public int PopulationAfter { get; set; }
        public int HungryAfter { get; set; }
        public int StarvingAfter { get; set; }
        public List<string> DepartedNpcNames { get; set; } = new List<string>();

        /// <summary>Journal entry ids written during this Next Turn (including pre-turn raids).</summary>
        public List<string> TurnJournalEntryIds { get; set; } = new List<string>();

        /// <summary>Outlook frozen when the turn ended. Live outlook on the latest report uses current rates instead.</summary>
        public List<ResourceOutlook> SavedOutlook { get; set; } = new List<ResourceOutlook>();

        public WeeklyReport()
        {
        }

        public WeeklyReport(int week, int year)
        {
            Week = week;
            Year = year;
        }

        public string GenerateSummary()
        {
            string summary = $"Week {Week}, Year {Year} Summary:\n\n";

            if (CompletedProjects.Count > 0)
            {
                summary += "Completed Projects:\n";
                foreach (var project in CompletedProjects)
                {
                    summary += $"- {project.Name} ({project.Type})\n";
                }
                summary += "\n";
            }

            summary += "Resource Changes:\n";
            foreach (var change in ResourceChanges)
            {
                string direction = change.NetChange >= 0 ? "+" : "";
                summary += $"- {change.ResourceType}: {change.PreviousAmount} → {change.CurrentAmount} ({direction}{change.NetChange})\n";
            }
            summary += "\n";

            summary += $"Income: {IncomeExpenseSummary.TotalIncome} Gold\n";
            summary += $"Expenses: {IncomeExpenseSummary.TotalExpenses} Gold\n";
            summary += $"Net Change: {IncomeExpenseSummary.NetChange} Gold\n\n";

            summary += $"Morale: {MoraleBefore} → {MoraleAfter}\n";
            summary += $"Population: {PopulationBefore} → {PopulationAfter}\n\n";

            if (NPCStatusChanges.Count > 0)
            {
                summary += "NPC Status Changes:\n";
                foreach (var change in NPCStatusChanges)
                {
                    summary += $"- {change.NPCName}: ";
                    foreach (var attribute in change.Changes)
                    {
                        summary += $"{attribute.Attribute} changed from {attribute.OldValue} to {attribute.NewValue}, ";
                    }
                    summary = summary.TrimEnd(' ', ',') + "\n";
                }
                summary += "\n";
            }

            if (UpcomingCompletions.Count > 0)
            {
                summary += "Upcoming Completions:\n";
                foreach (var completion in UpcomingCompletions)
                {
                    summary += $"- {completion.Name} ({completion.Type}): {completion.WeeksRemaining} week(s) remaining\n";
                }
            }

            return summary;
        }
    }

    public class CompletedProject
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty; // "Building" or "Mission"
    }

    public class ResourceChange
    {
        public ResourceType ResourceType { get; set; }
        public int PreviousAmount { get; set; }
        public int CurrentAmount { get; set; }
        public int Produced { get; set; }
        public int Consumed { get; set; }
        public int NetChange => CurrentAmount - PreviousAmount;
        public List<ResourceChangeBreakdown> Breakdown { get; set; } = new List<ResourceChangeBreakdown>();
    }

    public class ResourceChangeBreakdown
    {
        public string Source { get; set; } = string.Empty;
        public int Amount { get; set; }
    }

    public class ResourceOutlook
    {
        public ResourceType ResourceType { get; set; }
        public int ProjectedAmount { get; set; }
        public int WeeklyProduction { get; set; }
        public int WeeklyConsumption { get; set; }
    }

    public class IncomeExpenseSummary
    {
        public int TotalIncome { get; set; }
        public int TotalExpenses { get; set; }
        public int NetChange => TotalIncome - TotalExpenses;
        public List<IncomeExpenseBreakdown> Breakdown { get; set; } = new List<IncomeExpenseBreakdown>();
    }

    public class IncomeExpenseBreakdown
    {
        public string Category { get; set; } = string.Empty;
        public int Income { get; set; }
        public int Expenses { get; set; }
    }

    public class NPCStatusChange
    {
        public string NPCId { get; set; } = string.Empty;
        public string NPCName { get; set; } = string.Empty;
        public List<AttributeChange> Changes { get; set; } = new List<AttributeChange>();
    }

    public class AttributeChange
    {
        public string Attribute { get; set; } = string.Empty;
        public object OldValue { get; set; } = null!;
        public object NewValue { get; set; } = null!;
    }

    public class UpcomingCompletion
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty; // "Building" or "Mission"
        public int WeeksRemaining { get; set; }
    }

    public class EventSummary
    {
        public string EventId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
    }
}
