using System;
using System.Collections.Generic;
using System.Linq;

namespace DNDStrongholdApp.Models
{
    public class Building
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public BuildingType Type { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Level { get; set; } = 1;
        public BuildingStatus ConstructionStatus { get; set; } = BuildingStatus.Planning;
        public int ConstructionProgress { get; set; } = 0; // 0-100%
        public int ConstructionTimeRemaining { get; set; } = 0; // in weeks
        public List<ResourceCost> ConstructionCost { get; set; } = new List<ResourceCost>();
        public int WorkerSlots { get; set; }
        public List<string> AssignedWorkers { get; set; } = new List<string>(); // NPC IDs
        public List<ResourceProduction> BaseProduction { get; set; } = new List<ResourceProduction>();
        public List<ResourceProduction> ActualProduction { get; set; } = new List<ResourceProduction>();
        public List<ResourceCost> BaseUpkeep { get; set; } = new List<ResourceCost>();
        public List<ResourceCost> ActualUpkeep { get; set; } = new List<ResourceCost>();
        public List<SpecialAbility> SpecialAbilities { get; set; } = new List<SpecialAbility>();
        public int Condition { get; set; } = 100; // 0-100%
        public int RepairTimeRemaining { get; set; } = 0; // in weeks
        public int RepairTime { get; set; } = 0; // Total repair time in weeks
        public List<ResourceCost> RepairCost { get; set; } = new List<ResourceCost>();

        // Constructor for a new building
        public Building(BuildingType type)
        {
            Type = type;
            Name = GetDefaultName(type);
            SetDefaultProperties();
        }

        // Set default properties based on building type
        private void SetDefaultProperties()
        {
            switch (Type)
            {
                case BuildingType.Farm:
                    WorkerSlots = 3;
                    ConstructionTimeRemaining = 2; // 2 weeks
                    ConstructionCost.Add(new ResourceCost { ResourceType = ResourceType.Gold, Amount = 50 });
                    ConstructionCost.Add(new ResourceCost { ResourceType = ResourceType.Wood, Amount = 30 });
                    ConstructionCost.Add(new ResourceCost { ResourceType = ResourceType.Stone, Amount = 10 });
                    
                    BaseProduction.Add(new ResourceProduction { ResourceType = ResourceType.Food, Amount = 20 });
                    BaseUpkeep.Add(new ResourceCost { ResourceType = ResourceType.Gold, Amount = 5 });
                    break;
                    
                case BuildingType.Watchtower:
                    WorkerSlots = 2;
                    ConstructionTimeRemaining = 3; // 3 weeks
                    ConstructionCost.Add(new ResourceCost { ResourceType = ResourceType.Gold, Amount = 40 });
                    ConstructionCost.Add(new ResourceCost { ResourceType = ResourceType.Wood, Amount = 20 });
                    ConstructionCost.Add(new ResourceCost { ResourceType = ResourceType.Stone, Amount = 30 });
                    
                    BaseUpkeep.Add(new ResourceCost { ResourceType = ResourceType.Gold, Amount = 8 });
                    break;

                case BuildingType.Quarry:
                    WorkerSlots = 4;
                    ConstructionTimeRemaining = 4; // 4 weeks
                    ConstructionCost.Add(new ResourceCost { ResourceType = ResourceType.Gold, Amount = 100 });
                    ConstructionCost.Add(new ResourceCost { ResourceType = ResourceType.Wood, Amount = 50 });
                    ConstructionCost.Add(new ResourceCost { ResourceType = ResourceType.Stone, Amount = 20 });
                    
                    BaseProduction.Add(new ResourceProduction { ResourceType = ResourceType.Stone, Amount = 5 }); // Base rate per worker
                    BaseUpkeep.Add(new ResourceCost { ResourceType = ResourceType.Gold, Amount = 1 }); // Base upkeep per level
                    break;
                    
                // Add other building types as needed
                    
                default:
                    WorkerSlots = 1;
                    ConstructionTimeRemaining = 1;
                    break;
            }
            
            // Initialize actual production and upkeep with base values
            ActualProduction = new List<ResourceProduction>(BaseProduction);
            ActualUpkeep = new List<ResourceCost>(BaseUpkeep);
            
            // Set repair costs to 50% of construction costs
            foreach (var cost in ConstructionCost)
            {
                RepairCost.Add(new ResourceCost 
                { 
                    ResourceType = cost.ResourceType, 
                    Amount = (int)(cost.Amount * 0.5f) 
                });
            }
            
            // Default repair time is 50% of construction time
            RepairTime = (int)(ConstructionTimeRemaining * 0.5f);
            if (RepairTime < 1) RepairTime = 1;
            RepairTimeRemaining = RepairTime;
        }

        // Get default name based on building type
        private string GetDefaultName(BuildingType type)
        {
            return type.ToString();
        }

        // Update production based on assigned workers
        public void UpdateProduction(List<NPC> assignedNPCs)
        {
            // Reset actual production to base values
            ActualProduction = new List<ResourceProduction>();
            foreach (var production in BaseProduction)
            {
                ActualProduction.Add(new ResourceProduction
                {
                    ResourceType = production.ResourceType,
                    Amount = production.Amount
                });
            }

            // If building is damaged, no production
            if (ConstructionStatus == BuildingStatus.Damaged)
            {
                foreach (var production in ActualProduction)
                {
                    production.Amount = 0;
                }
                return;
            }

            // Apply worker bonuses
            foreach (var npc in assignedNPCs)
            {
                foreach (var production in ActualProduction)
                {
                    // Add worker's skill level to production
                    var skill = npc.Skills.Find(s => s.Name == production.ResourceType.ToString());
                    if (skill != null)
                    {
                        production.Amount += skill.Level;
                    }
                }
            }
        }

        // Progress construction by one week
        public bool AdvanceConstruction()
        {
            if (ConstructionStatus != BuildingStatus.UnderConstruction)
                return false;

            // No progress if no workers are assigned
            if (AssignedWorkers.Count == 0)
                return false;

            ConstructionTimeRemaining--;
            ConstructionProgress = 100 - (ConstructionTimeRemaining * 100 / GetTotalConstructionTime());

            if (ConstructionTimeRemaining <= 0)
            {
                ConstructionStatus = BuildingStatus.Complete;
                ConstructionProgress = 100;
                return true; // Construction completed
            }

            return false; // Still under construction
        }
        
        // Progress repair by one week
        public bool AdvanceRepair()
        {
            if (ConstructionStatus != BuildingStatus.Repairing)
                return false;

            // No progress if no workers are assigned
            if (AssignedWorkers.Count == 0)
                return false;

            RepairTimeRemaining--;
            
            // If repair is complete
            if (RepairTimeRemaining <= 0)
            {
                ConstructionStatus = BuildingStatus.Complete;
                Condition = 100;
                return true;
            }
            
            return false;
        }
        
        // Damage the building
        public void Damage(int damageAmount)
        {
            // Reduce condition
            Condition -= damageAmount;
            
            // If condition falls below 25%, building becomes damaged
            if (Condition < 25 && ConstructionStatus == BuildingStatus.Complete)
            {
                ConstructionStatus = BuildingStatus.Damaged;
            }
            
            // Ensure condition doesn't go below 0
            if (Condition < 0)
            {
                Condition = 0;
            }
        }
        
        // Start repair process
        public bool StartRepair(List<Resource> availableResources)
        {
            if (ConstructionStatus != BuildingStatus.Damaged)
                return false;
                
            // Check if we have enough resources
            foreach (var cost in RepairCost)
            {
                var resource = availableResources.Find(r => r.Type == cost.ResourceType);
                if (resource == null || resource.Amount < cost.Amount)
                {
                    return false; // Not enough resources
                }
            }
            
            // Consume resources
            foreach (var cost in RepairCost)
            {
                var resource = availableResources.Find(r => r.Type == cost.ResourceType);
                if (resource != null)
                {
                    resource.Amount -= cost.Amount;
                }
            }
            
            // Start repair
            ConstructionStatus = BuildingStatus.Repairing;
            
            return true;
        }

        private int GetTotalConstructionTime()
        {
            switch (Type)
            {
                case BuildingType.Farm: return 2;
                case BuildingType.Watchtower: return 3;
                case BuildingType.Quarry: return 4;
                // Add other building types
                default: return 1;
            }
        }
        
        // Check if the building is functional
        public bool IsFunctional()
        {
            return ConstructionStatus == BuildingStatus.Complete;
        }

        public int GetMaxLevel()
        {
            switch (Type)
            {
                case BuildingType.Quarry:
                    return 4;
                default:
                    return 5; // Default max level for other buildings
            }
        }

        public int GetWorkerSlots()
        {
            int baseSlots = WorkerSlots;
            if (Type == BuildingType.Quarry)
            {
                return baseSlots + (Level - 1); // +1 slot per level after level 1
            }
            return baseSlots;
        }

        public List<ResourceProduction> CalculateProduction(List<NPC> workers)
        {
            var production = new List<ResourceProduction>();
            
            if (Type == BuildingType.Quarry)
            {
                int totalStone = 0;
                foreach (var worker in workers)
                {
                    // Base production of 5 stone per worker
                    int workerProduction = 5;
                    
                    // Add worker's labor skill level
                    var laborSkill = worker.Skills.FirstOrDefault(s => s.Name == "Labor");
                    if (laborSkill != null)
                    {
                        workerProduction += laborSkill.Level;
                    }
                    
                    // Add +1 per building level
                    workerProduction += Level;
                    
                    totalStone += workerProduction;
                }
                
                production.Add(new ResourceProduction { ResourceType = ResourceType.Stone, Amount = totalStone });
            }
            else
            {
                // Default production calculation for other buildings
                production.AddRange(BaseProduction);
            }
            
            return production;
        }

        public List<ResourceCost> CalculateUpkeep(List<NPC> workers)
        {
            var upkeep = new List<ResourceCost>();
            
            if (Type == BuildingType.Quarry)
            {
                // Base upkeep of 1 gold per level
                upkeep.Add(new ResourceCost { ResourceType = ResourceType.Gold, Amount = Level });
                
                // Add worker salaries
                foreach (var worker in workers)
                {
                    upkeep.Add(new ResourceCost { ResourceType = ResourceType.Gold, Amount = 2 }); // Base salary of 2 gold per worker
                }
            }
            else
            {
                // Default upkeep calculation for other buildings
                upkeep.AddRange(BaseUpkeep);
            }
            
            return upkeep;
        }
    }

    public enum BuildingType
    {
        Farm,
        Watchtower,
        Smithy,
        Laboratory,
        Chapel,
        Mine,
        Barracks,
        Library,
        TradeOffice,
        Stables,
        Tavern,
        MasonsYard,
        Workshop,
        Granary,
        Quarry
    }

    public enum BuildingStatus
    {
        Planning,
        UnderConstruction,
        Complete,
        Damaged,
        Repairing
    }

    public class ResourceCost
    {
        public ResourceType ResourceType { get; set; }
        public int Amount { get; set; }
    }

    public class ResourceProduction
    {
        public ResourceType ResourceType { get; set; }
        public int Amount { get; set; }
    }

    public class SpecialAbility
    {
        public string Type { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int MinimumLevel { get; set; } = 2; // Most abilities unlock at level 2+
        public bool IsActive { get; set; } = false;
    }
} 