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
        
        // Constructor
        public JournalEntry(int week, int year, JournalEntryType type, string title, string description)
        {
            Week = week;
            Year = year;
            Type = type;
            Title = title;
            Description = description;
            Date = $"Week {week}, Year {year}";
        }
        
        // Add a related entity to the journal entry
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
        WeeklyReport
    }
    
    public enum ImportanceLevel
    {
        Low,
        Medium,
        High
    }
    
    public class WeeklyReport
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public int Week { get; set; }
        public int Year { get; set; }
        public List<CompletedProject> CompletedProjects { get; set; } = new List<CompletedProject>();
        public List<ResourceChange> ResourceChanges { get; set; } = new List<ResourceChange>();
        public IncomeExpenseSummary IncomeExpenseSummary { get; set; } = new IncomeExpenseSummary();
        public List<NPCStatusChange> NPCStatusChanges { get; set; } = new List<NPCStatusChange>();
        public List<UpcomingCompletion> UpcomingCompletions { get; set; } = new List<UpcomingCompletion>();
        public List<EventSummary> Events { get; set; } = new List<EventSummary>();
        
        // Constructor
        public WeeklyReport(int week, int year)
        {
            Week = week;
            Year = year;
        }
        
        // Generate a summary string for display
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
        public int NetChange => CurrentAmount - PreviousAmount;
        public List<ResourceChangeBreakdown> Breakdown { get; set; } = new List<ResourceChangeBreakdown>();
    }
    
    public class ResourceChangeBreakdown
    {
        public string Source { get; set; } = string.Empty;
        public int Amount { get; set; }
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