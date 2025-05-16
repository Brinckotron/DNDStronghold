using System;
using System.Collections.Generic;

namespace DNDStrongholdApp.Models
{
    public class NPC
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public NPCType Type { get; set; }
        public string Name { get; set; } = string.Empty;
        public List<Skill> Skills { get; set; } = new List<Skill>();
        public NPCAssignment Assignment { get; set; } = new NPCAssignment();
        public int Happiness { get; set; } = 80; // 0-100%
        public List<ResourceCost> UpkeepCost { get; set; } = new List<ResourceCost>();
        public List<SpecialAbility> SpecialAbilities { get; set; } = new List<SpecialAbility>();
        public int Experience { get; set; } = 0;
        public int Level { get; set; } = 1;

        // Constructor for a new NPC
        public NPC(NPCType type, string name = "")
        {
            Type = type;
            Name = !string.IsNullOrEmpty(name) ? name : GenerateRandomName();
            SetDefaultProperties();
        }

        // Set default properties based on NPC type
        private void SetDefaultProperties()
        {
            switch (Type)
            {
                case NPCType.Peasant:
                    Skills.Add(new Skill { Name = "Labor", Level = 1, Description = "Basic manual labor" });
                    Skills.Add(new Skill { Name = "Survival", Level = 1, Description = "Basic food gathering" });
                    
                    UpkeepCost.Add(new ResourceCost { ResourceType = ResourceType.Gold, Amount = 2 });
                    UpkeepCost.Add(new ResourceCost { ResourceType = ResourceType.Food, Amount = 1 });
                    break;
                    
                case NPCType.Laborer:
                    Skills.Add(new Skill { Name = "Construction", Level = 2, Description = "Building and repair" });
                    Skills.Add(new Skill { Name = "Labor", Level = 2, Description = "Enhanced manual labor" });
                    
                    UpkeepCost.Add(new ResourceCost { ResourceType = ResourceType.Gold, Amount = 3 });
                    UpkeepCost.Add(new ResourceCost { ResourceType = ResourceType.Food, Amount = 2 });
                    break;
                    
                case NPCType.Farmer:
                    Skills.Add(new Skill { Name = "Farming", Level = 2, Description = "Crop cultivation" });
                    Skills.Add(new Skill { Name = "Animal Handling", Level = 2, Description = "Livestock management" });
                    
                    UpkeepCost.Add(new ResourceCost { ResourceType = ResourceType.Gold, Amount = 3 });
                    UpkeepCost.Add(new ResourceCost { ResourceType = ResourceType.Food, Amount = 1 });
                    break;
                    
                case NPCType.Militia:
                    Skills.Add(new Skill { Name = "Combat", Level = 2, Description = "Basic fighting ability" });
                    Skills.Add(new Skill { Name = "Guard Duty", Level = 2, Description = "Vigilance and security" });
                    
                    UpkeepCost.Add(new ResourceCost { ResourceType = ResourceType.Gold, Amount = 4 });
                    UpkeepCost.Add(new ResourceCost { ResourceType = ResourceType.Food, Amount = 2 });
                    break;
                    
                case NPCType.Scout:
                    Skills.Add(new Skill { Name = "Survival", Level = 2, Description = "Wilderness navigation" });
                    Skills.Add(new Skill { Name = "Perception", Level = 3, Description = "Spotting threats and resources" });
                    
                    UpkeepCost.Add(new ResourceCost { ResourceType = ResourceType.Gold, Amount = 4 });
                    UpkeepCost.Add(new ResourceCost { ResourceType = ResourceType.Food, Amount = 2 });
                    break;
                    
                case NPCType.Artisan:
                    Skills.Add(new Skill { Name = "Crafting", Level = 3, Description = "Creating goods and items" });
                    Skills.Add(new Skill { Name = "Appraisal", Level = 2, Description = "Evaluating materials and items" });
                    
                    UpkeepCost.Add(new ResourceCost { ResourceType = ResourceType.Gold, Amount = 5 });
                    UpkeepCost.Add(new ResourceCost { ResourceType = ResourceType.Food, Amount = 1 });
                    break;
                    
                case NPCType.Scholar:
                    Skills.Add(new Skill { Name = "Research", Level = 3, Description = "Discovering new knowledge" });
                    Skills.Add(new Skill { Name = "Lore", Level = 3, Description = "Historical and arcane knowledge" });
                    
                    UpkeepCost.Add(new ResourceCost { ResourceType = ResourceType.Gold, Amount = 6 });
                    UpkeepCost.Add(new ResourceCost { ResourceType = ResourceType.Food, Amount = 1 });
                    break;
                    
                case NPCType.Merchant:
                    Skills.Add(new Skill { Name = "Trade", Level = 3, Description = "Buying and selling goods" });
                    Skills.Add(new Skill { Name = "Negotiation", Level = 3, Description = "Getting better prices" });
                    
                    UpkeepCost.Add(new ResourceCost { ResourceType = ResourceType.Gold, Amount = 7 });
                    UpkeepCost.Add(new ResourceCost { ResourceType = ResourceType.Food, Amount = 1 });
                    break;
            }
        }

        // Generate a random name for the NPC
        private string GenerateRandomName()
        {
            // This is a simple implementation - could be expanded with more diverse names
            string[] maleNames = { "John", "William", "James", "Robert", "Michael", "Thomas", "David", "Richard", "Charles", "Joseph" };
            string[] femaleNames = { "Mary", "Patricia", "Jennifer", "Linda", "Elizabeth", "Barbara", "Susan", "Jessica", "Sarah", "Karen" };
            string[] surnames = { "Smith", "Johnson", "Williams", "Jones", "Brown", "Davis", "Miller", "Wilson", "Moore", "Taylor" };
            
            Random random = new Random();
            bool isMale = random.Next(2) == 0;
            
            string firstName = isMale 
                ? maleNames[random.Next(maleNames.Length)] 
                : femaleNames[random.Next(femaleNames.Length)];
                
            string lastName = surnames[random.Next(surnames.Length)];
            
            return $"{firstName} {lastName}";
        }

        // Add experience and potentially level up
        public bool AddExperience(int amount)
        {
            Experience += amount;
            
            // Simple leveling formula: 100 * current level to advance
            int requiredXP = Level * 100;
            
            if (Experience >= requiredXP)
            {
                Level++;
                Experience -= requiredXP;
                return true; // Leveled up
            }
            
            return false; // No level up
        }

        // Improve a specific skill
        public void ImproveSkill(string skillName)
        {
            var skill = Skills.Find(s => s.Name == skillName);
            
            if (skill != null)
            {
                skill.Level++;
            }
            else
            {
                // Add new skill at level 1
                Skills.Add(new Skill { Name = skillName, Level = 1, Description = "Newly acquired skill" });
            }
        }

        // Calculate efficiency for a specific task
        public float GetEfficiencyForTask(string taskName)
        {
            var skill = Skills.Find(s => s.Name == taskName);
            
            if (skill != null)
            {
                // Base efficiency is 0.5 (50%) + 0.1 per skill level
                return 0.5f + (skill.Level * 0.1f);
            }
            
            return 0.5f; // Base efficiency for unskilled tasks
        }

        // Update happiness based on various factors
        public void UpdateHappiness(bool hasProperHousing, bool hasProperFood, bool hasTavern)
        {
            int happinessChange = 0;
            
            // Housing impact
            happinessChange += hasProperHousing ? 5 : -5;
            
            // Food impact
            happinessChange += hasProperFood ? 5 : -10;
            
            // Tavern impact
            happinessChange += hasTavern ? 5 : -2;
            
            // Assignment impact
            if (Assignment.Type != AssignmentType.Unassigned)
            {
                happinessChange += 5; // Being productive makes NPCs happy
            }
            
            // Apply change
            Happiness += happinessChange;
            
            // Clamp happiness to 0-100
            Happiness = Math.Max(0, Math.Min(100, Happiness));
        }
    }

    public enum NPCType
    {
        Peasant,
        Laborer,
        Farmer,
        Militia,
        Scout,
        Artisan,
        Scholar,
        Merchant
    }

    public class Skill
    {
        public string Name { get; set; } = string.Empty;
        public int Level { get; set; } = 1; // 1-5
        public string Description { get; set; } = string.Empty;
    }

    public class NPCAssignment
    {
        public AssignmentType Type { get; set; } = AssignmentType.Unassigned;
        public string TargetId { get; set; } = string.Empty; // Building or Mission ID
        public string TargetName { get; set; } = string.Empty;
    }

    public enum AssignmentType
    {
        Unassigned,
        Building,
        Mission
    }
} 