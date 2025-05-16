using System;
using System.Collections.Generic;

namespace DNDStrongholdApp.Models
{
    public class Mission
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public MissionRequirements Requirements { get; set; } = new MissionRequirements();
        public int Duration { get; set; } // in weeks
        public int Progress { get; set; } = 0; // 0-100%
        public int WeeksRemaining { get; set; }
        public MissionStatus Status { get; set; } = MissionStatus.Available;
        public int SuccessProbability { get; set; } = 75; // 0-100%
        public MissionRewards Rewards { get; set; } = new MissionRewards();
        public List<string> AssignedNPCs { get; set; } = new List<string>(); // NPC IDs
        
        // Constructor
        public Mission(string name, string description, int duration)
        {
            Name = name;
            Description = description;
            Duration = duration;
            WeeksRemaining = duration;
        }
        
        // Add resource requirement
        public void AddResourceRequirement(ResourceType resourceType, int amount)
        {
            Requirements.Resources.Add(new ResourceCost
            {
                ResourceType = resourceType,
                Amount = amount
            });
        }
        
        // Add NPC requirement
        public void AddNPCRequirement(NPCType? type, int count, string skillName = "", int minSkillLevel = 0)
        {
            var requirement = new NPCRequirement
            {
                Type = type,
                Count = count
            };
            
            if (!string.IsNullOrEmpty(skillName) && minSkillLevel > 0)
            {
                requirement.MinSkillLevel = new SkillRequirement
                {
                    SkillName = skillName,
                    Level = minSkillLevel
                };
            }
            
            Requirements.NPCs.Add(requirement);
        }
        
        // Add building requirement
        public void AddBuildingRequirement(BuildingType type, int minLevel = 1)
        {
            Requirements.Buildings.Add(new BuildingRequirement
            {
                Type = type,
                MinLevel = minLevel
            });
        }
        
        // Add resource reward
        public void AddResourceReward(ResourceType resourceType, int amount)
        {
            Rewards.Resources.Add(new ResourceProduction
            {
                ResourceType = resourceType,
                Amount = amount
            });
        }
        
        // Check if all requirements are met
        public bool AreRequirementsMet(List<Resource> availableResources, List<NPC> availableNPCs, List<Building> buildings)
        {
            // Check resource requirements
            foreach (var requirement in Requirements.Resources)
            {
                var resource = availableResources.Find(r => r.Type == requirement.ResourceType);
                if (resource == null || resource.Amount < requirement.Amount)
                {
                    return false;
                }
            }
            
            // Check NPC requirements
            foreach (var requirement in Requirements.NPCs)
            {
                int matchingNPCs = 0;
                
                foreach (var npc in availableNPCs)
                {
                    // Skip NPCs that are already assigned
                    if (npc.Assignment.Type != AssignmentType.Unassigned)
                    {
                        continue;
                    }
                    
                    // Check NPC type requirement
                    if (requirement.Type != null && npc.Type != requirement.Type)
                    {
                        continue;
                    }
                    
                    // Check skill requirement
                    if (requirement.MinSkillLevel != null)
                    {
                        var skill = npc.Skills.Find(s => s.Name == requirement.MinSkillLevel.SkillName);
                        if (skill == null || skill.Level < requirement.MinSkillLevel.Level)
                        {
                            continue;
                        }
                    }
                    
                    matchingNPCs++;
                }
                
                if (matchingNPCs < requirement.Count)
                {
                    return false;
                }
            }
            
            // Check building requirements
            foreach (var requirement in Requirements.Buildings)
            {
                var building = buildings.Find(b => b.Type == requirement.Type && b.Level >= requirement.MinLevel);
                if (building == null)
                {
                    return false;
                }
            }
            
            return true;
        }
        
        // Start the mission
        public bool Start(List<Resource> availableResources)
        {
            if (Status != MissionStatus.Available)
            {
                return false;
            }
            
            // Consume required resources
            foreach (var requirement in Requirements.Resources)
            {
                var resource = availableResources.Find(r => r.Type == requirement.ResourceType);
                if (resource != null)
                {
                    resource.Amount -= requirement.Amount;
                }
            }
            
            Status = MissionStatus.InProgress;
            return true;
        }
        
        // Advance mission progress by one week
        public bool AdvanceProgress()
        {
            if (Status != MissionStatus.InProgress)
            {
                return false;
            }
            
            WeeksRemaining--;
            Progress = 100 - (WeeksRemaining * 100 / Duration);
            
            if (WeeksRemaining <= 0)
            {
                // Mission completed
                WeeksRemaining = 0;
                Progress = 100;
                
                // Determine success based on probability
                Random random = new Random();
                bool success = random.Next(100) < SuccessProbability;
                
                Status = success ? MissionStatus.Completed : MissionStatus.Failed;
                return true; // Mission ended
            }
            
            return false; // Mission still in progress
        }
        
        // Calculate success probability based on assigned NPCs
        public void UpdateSuccessProbability(List<NPC> assignedNPCs)
        {
            int baseSuccessChance = 75; // Base 75% success chance
            int bonusFromNPCs = 0;
            
            foreach (var npc in assignedNPCs)
            {
                // Check for relevant skills
                foreach (var skill in npc.Skills)
                {
                    // This is a simplified calculation - could be more specific based on mission type
                    bonusFromNPCs += skill.Level;
                }
            }
            
            // Cap the bonus at 20%
            bonusFromNPCs = Math.Min(bonusFromNPCs, 20);
            
            SuccessProbability = Math.Min(baseSuccessChance + bonusFromNPCs, 95); // Cap at 95%
        }
    }
    
    public class MissionRequirements
    {
        public List<ResourceCost> Resources { get; set; } = new List<ResourceCost>();
        public List<NPCRequirement> NPCs { get; set; } = new List<NPCRequirement>();
        public List<BuildingRequirement> Buildings { get; set; } = new List<BuildingRequirement>();
    }
    
    public class NPCRequirement
    {
        public NPCType? Type { get; set; } // Null means any type
        public int Count { get; set; }
        public SkillRequirement? MinSkillLevel { get; set; }
    }
    
    public class SkillRequirement
    {
        public string SkillName { get; set; } = string.Empty;
        public int Level { get; set; }
    }
    
    public class BuildingRequirement
    {
        public BuildingType Type { get; set; }
        public int MinLevel { get; set; } = 1;
    }
    
    public class MissionRewards
    {
        public List<ResourceProduction> Resources { get; set; } = new List<ResourceProduction>();
        public int ReputationGain { get; set; }
        public List<SpecialReward> SpecialRewards { get; set; } = new List<SpecialReward>();
    }
    
    public class SpecialReward
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
    }
    
    public enum MissionStatus
    {
        Available,
        InProgress,
        Completed,
        Failed
    }
} 