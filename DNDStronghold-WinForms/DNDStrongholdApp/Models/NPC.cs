using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DNDStrongholdApp.Models
{
    public class NPC
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public NPCType Type { get; set; }
        public string Name { get; set; } = string.Empty;
        public NPCGender Gender { get; set; } = NPCGender.Male;
        public List<Skill> Skills { get; set; } = new List<Skill>();
        public NPCAssignment Assignment { get; set; } = new NPCAssignment();
        public List<ResourceCost> UpkeepCost { get; set; } = new List<ResourceCost>();
        public List<NPCState> States { get; set; } = new List<NPCState>();
        public List<NPCTrait> Traits { get; set; } = new List<NPCTrait>();
        public int Age { get; set; } = 20; // Starting age
        public bool IsAlive { get; set; } = true;
        public int Level { get; set; } = 1; // NPC level, starts at 1
        public NPCStatus Status { get; set; } = NPCStatus.Available; // Current status of the NPC
        public Bio Bio { get; set; } = new Bio(); // NPC biography/description

        // Constructor for a new NPC
        public NPC(NPCType type, string name = "")
        {
            Type = type;
            if (!string.IsNullOrEmpty(name))
            {
                Name = name;
            }
            else
            {
                // Randomly assign gender before generating name
                Random random = new Random();
                Gender = random.Next(2) == 0 ? NPCGender.Male : NPCGender.Female;
                Name = GenerateRandomName();
            }
            InitializeSkills();
            UpdateUpkeepCosts(); // Initialize upkeep costs
        }

        // Generate a procedural bio for this NPC
        public void GenerateBio()
        {
            try
            {
                var bioGenerator = new Services.BioGeneratorService();
                Bio.GenerateFromService(bioGenerator, this);
            }
            catch
            {
                // If bio generation fails, leave bio empty
                Bio = new Bio();
            }
        }

        // Initialize all skills at level 0
        private void InitializeSkills()
        {
            // Add all basic skills at level 0
            foreach (string skillName in Enum.GetNames(typeof(BasicSkill)))
            {
                Skills.Add(new Skill 
                { 
                    Name = skillName, 
                    Level = 0, 
                    Description = $"Basic {skillName} skill",
                    Type = SkillType.Basic,
                    Experience = 0,
                    Specialization = 0f
                });
            }

            // Add all advanced skills at level 0
            foreach (string skillName in Enum.GetNames(typeof(AdvancedSkill)))
            {
                Skills.Add(new Skill 
                { 
                    Name = skillName, 
                    Level = 0, 
                    Description = $"Advanced {skillName} skill",
                    Type = SkillType.Advanced,
                    IsLearned = false,
                    Experience = 0,
                    Specialization = 0f
                });
            }

            // Set initial skill levels and specializations based on NPC type
            switch (Type)
            {
                case NPCType.Peasant:
                    // Peasants start with no special skills or specializations
                    break;
                    
                case NPCType.Laborer:
                    Skills.Find(s => s.Name == "Construction").Level = 1;
                    SetSkillSpecialization("Construction", 0.2f);
                    SetSkillSpecialization("Labor", 0.2f);
                    break;
                    
                case NPCType.Farmer:
                    Skills.Find(s => s.Name == "Farming").Level = 1;
                    SetSkillSpecialization("Farming", 0.2f);
                    SetSkillSpecialization("Labor", 0.2f);
                    break;
                    
                case NPCType.Militia:
                    Skills.Find(s => s.Name == "Combat").Level = 1;
                    SetSkillSpecialization("Combat", 0.2f);
                    SetSkillSpecialization("Perception", 0.2f);
                    break;
                    
                case NPCType.Scout:
                    Skills.Find(s => s.Name == "Survival").Level = 1;
                    SetSkillSpecialization("Survival", 0.2f);
                    SetSkillSpecialization("Perception", 0.2f);
                    break;
                    
                case NPCType.Artisan:
                    Skills.Find(s => s.Name == "Crafting").Level = 1;
                    SetSkillSpecialization("Crafting", 0.2f);
                    SetSkillSpecialization("Labor", 0.2f);
                    break;
                    
                case NPCType.Scholar:
                    Skills.Find(s => s.Name == "Lore").Level = 1;
                    SetSkillSpecialization("Lore", 0.2f);
                    SetSkillSpecialization("Research", 0.2f);
                    break;
                    
                case NPCType.Merchant:
                    Skills.Find(s => s.Name == "Trade").Level = 1;
                    SetSkillSpecialization("Trade", 0.2f);
                    SetSkillSpecialization("Connections", 0.2f);
                    break;
            }
        }

        // Set specialization for a skill
        private void SetSkillSpecialization(string skillName, float specialization)
        {
            var skill = Skills.Find(s => s.Name == skillName);
            if (skill != null)
            {
                skill.Specialization = specialization;
            }
        }

        // Calculate upkeep costs based on skills and traits
        public void UpdateUpkeepCosts()
        {
            UpkeepCost.Clear();

            // Base food cost
            int foodCost = 2;
            // Base gold cost is highest skill level (minimum 1)
            int goldCost = Math.Max(1, Skills.Max(s => s.Level));

            // Apply trait modifiers
            foreach (var trait in Traits)
            {
                switch (trait.Type)
                {
                    case NPCTraitType.Expensive:
                        goldCost += 1;
                        break;
                    case NPCTraitType.Glutton:
                        foodCost += 1;
                        break;
                    case NPCTraitType.Charitable:
                        goldCost = Math.Max(1, goldCost - 1);
                        break;
                    case NPCTraitType.Nibbler:
                        foodCost = Math.Max(1, foodCost - 1);
                        break;
                }
            }

            // Add the calculated costs
            UpkeepCost.Add(new ResourceCost { ResourceType = ResourceType.Gold, Amount = goldCost });
            UpkeepCost.Add(new ResourceCost { ResourceType = ResourceType.Food, Amount = foodCost });
        }

        // Add experience to a specific skill
        public bool AddSkillExperience(string skillName, int amount)
        {
            // Check if NPC is at max level (sum of all skill levels >= 10)
            if (GetTotalSkillLevels() >= 10)
            {
                return false;
            }

            var skill = Skills.Find(s => s.Name == skillName);
            if (skill != null)
            {
                skill.Experience += amount;
                
                // Check if skill should level up
                int requiredXP = (skill.Level + 1) * 100;
                if (skill.Experience >= requiredXP)
                {
                    // Keep a percentage of XP based on specialization
                    int keptXP = (int)(skill.Experience * skill.Specialization);
                    skill.Level++;
                    skill.Experience = keptXP;
                    return true; // Skill leveled up
                }
            }
            return false; // No level up
        }

        // Get total of all skill levels
        public int GetTotalSkillLevels()
        {
            return Skills.Sum(s => s.Level);
        }

        // Learn an advanced skill
        public bool LearnAdvancedSkill(string skillName)
        {
            // Check if NPC already has this skill
            var existingSkill = Skills.Find(s => s.Name == skillName);
            if (existingSkill != null)
            {
                return false; // Already has this skill
            }

            // Add new advanced skill
            Skills.Add(new Skill 
            { 
                Name = skillName, 
                Level = 1, 
                Description = "Newly acquired advanced skill",
                Type = SkillType.Advanced,
                IsLearned = true,
                Experience = 0
            });
            
            return true;
        }

        // Update health states
        public void UpdateHealthState()
        {
            if (!IsAlive) return;

            bool hadStates = States.Any();

            // Check for recovery or worsening of conditions
            foreach (var state in States.ToList())
            {
                if (state.Type == NPCStateType.Sick || state.Type == NPCStateType.LightlyInjured)
                {
                    // 20% chance of recovery per week
                    if (new Random().Next(100) < 20)
                    {
                        States.Remove(state);
                    }
                }
                else if (state.Type == NPCStateType.GravelyInjured)
                {
                    // 10% chance of recovery, 5% chance of death
                    int roll = new Random().Next(100);
                    if (roll < 10)
                    {
                        States.Remove(state);
                    }
                    else if (roll < 15)
                    {
                        IsAlive = false;
                        States.Clear();
                    }
                }
            }

            // If health states changed, update status
            if (hadStates != States.Any())
            {
                UpdateStatus();
            }
        }

        // Add a health state
        public void AddHealthState(NPCStateType stateType)
        {
            if (!IsAlive) return;

            // Don't add duplicate states
            if (!States.Any(s => s.Type == stateType))
            {
                States.Add(new NPCState { Type = stateType });
                UpdateStatus(); // Update status when health state changes
            }
        }

        // Add a trait
        public void AddTrait(NPCTrait trait)
        {
            if (!Traits.Any(t => t.Type == trait.Type))
            {
                Traits.Add(trait);
            }
        }

        // Generate a random name for the NPC
        private string GenerateRandomName()
        {
            try
            {
                // Load names from JSON file
                string jsonPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "Names.json");
                if (!File.Exists(jsonPath))
                {
                    // Fallback to simple names if file doesn't exist
                    return GenerateFallbackName();
                }

                string json = File.ReadAllText(jsonPath);
                var nameData = JsonSerializer.Deserialize<NameData>(json);
                
                if (nameData == null || nameData.MaleNames == null || nameData.FemaleNames == null || nameData.Surnames == null)
                {
                    return GenerateFallbackName();
                }

                Random random = new Random();
                
                string firstName = Gender == NPCGender.Male 
                    ? nameData.MaleNames[random.Next(nameData.MaleNames.Count)] 
                    : nameData.FemaleNames[random.Next(nameData.FemaleNames.Count)];
                    
                string lastName = nameData.Surnames[random.Next(nameData.Surnames.Count)];
                
                return $"{firstName} {lastName}";
            }
            catch (Exception)
            {
                // If anything goes wrong, use fallback names
                return GenerateFallbackName();
            }
        }

        // Fallback name generation if JSON file is unavailable
        private string GenerateFallbackName()
        {
            string[] maleNames = { "John", "William", "James", "Robert", "Michael", "Thomas", "David", "Richard", "Charles", "Joseph" };
            string[] femaleNames = { "Mary", "Patricia", "Jennifer", "Linda", "Elizabeth", "Barbara", "Susan", "Jessica", "Sarah", "Karen" };
            string[] surnames = { "Smith", "Johnson", "Williams", "Jones", "Brown", "Davis", "Miller", "Wilson", "Moore", "Taylor" };
            
            Random random = new Random();
            
            string firstName = Gender == NPCGender.Male 
                ? maleNames[random.Next(maleNames.Length)] 
                : femaleNames[random.Next(femaleNames.Length)];
                
            string lastName = surnames[random.Next(surnames.Length)];
            
            return $"{firstName} {lastName}";
        }

        // Update status based on health states
        public void UpdateStatus()
        {
            // If NPC has any health states, they are unavailable
            if (States.Any())
            {
                Status = NPCStatus.Unavailable;
                return;
            }

            // Otherwise, status is determined by assignment
            if (Assignment.Type == AssignmentType.Project)
            {
                Status = NPCStatus.ProjectAssigned;
            }
            else if (Assignment.Type == AssignmentType.Building)
            {
                Status = NPCStatus.BuildingAssigned;
            }
            else if (Assignment.Type == AssignmentType.Mission)
            {
                Status = NPCStatus.OnMission;
            }
            else
            {
                Status = NPCStatus.Available;
            }
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

    public enum NPCGender
    {
        Male,
        Female
    }

    public class Bio
    {
        public string Text { get; set; } = string.Empty;
        public bool IsCustom { get; set; } = false;
        public Dictionary<string, int> SectionIndices { get; set; } = new Dictionary<string, int>();

        public Bio()
        {
            // Initialize with default indices
            SectionIndices["Background"] = 0;
            SectionIndices["Personality"] = 0;
            SectionIndices["Appearance"] = 0;
            SectionIndices["Motivation"] = 0;
            SectionIndices["Quirk"] = 0;
            SectionIndices["Secret"] = 0;
        }

        public void SetAsCustom(string customText)
        {
            Text = customText;
            IsCustom = true;
            // Clear section indices when setting custom text
            foreach (var key in SectionIndices.Keys.ToList())
            {
                SectionIndices[key] = 0;
            }
        }

        public void SetAsGenerated(string generatedText, Dictionary<string, int> indices)
        {
            Text = generatedText;
            IsCustom = false;
            SectionIndices = new Dictionary<string, int>(indices);
        }

        public void GenerateFromService(Services.BioGeneratorService bioGenerator, NPC npc)
        {
            // Generate bio text with matching indices
            string generatedText = bioGenerator.GenerateBioWithIndices(npc, out Dictionary<string, int> actualIndices);
            SetAsGenerated(generatedText, actualIndices);
        }
    }

    public enum BasicSkill
    {
        Labor,
        Survival,
        Construction,
        Farming,
        Combat,
        Perception,
        Crafting,
        Research,
        Lore,
        Trade
    }

    public enum AdvancedSkill
    {
        Stoneworking,
        Mining,
        Medicine,
        Alchemy,
        Jewelcrafting,
        Smithing,
        Arcana,
        Faith,
        Connections
    }

    public enum SkillType
    {
        Basic,
        Advanced
    }

    public class Skill
    {
        public string Name { get; set; } = string.Empty;
        public int Level { get; set; } = 0; // 0-5
        public string Description { get; set; } = string.Empty;
        public SkillType Type { get; set; } = SkillType.Basic;
        public bool IsLearned { get; set; } = false; // For advanced skills
        public int Experience { get; set; } = 0;
        public float Specialization { get; set; } = 0f; // 0-1, percentage of XP kept on level up
    }

    public enum NPCStateType
    {
        Sick,
        LightlyInjured,
        GravelyInjured
    }

    public class NPCState
    {
        public NPCStateType Type { get; set; }
        public DateTime StartDate { get; set; } = DateTime.Now;
    }

    public enum NPCTraitType
    {
        Frail,
        Hardy,
        QuickLearner,
        SlowLearner,
        Strong,
        Weak,
        Charismatic,
        Shy,
        Expensive,
        Glutton,
        Charitable,
        Nibbler
    }

    public class NPCTrait
    {
        public NPCTraitType Type { get; set; }
        public string Description { get; set; } = string.Empty;

        public string GetDescription()
        {
            return Type switch
            {
                NPCTraitType.Expensive => "Requires higher wages (+1 gold upkeep)",
                NPCTraitType.Glutton => "Eats more than usual (+1 food upkeep)",
                NPCTraitType.Charitable => "Willing to work for less (-1 gold upkeep)",
                NPCTraitType.Nibbler => "Eats less than usual (-1 food upkeep)",
                NPCTraitType.Frail => "More susceptible to illness and injury",
                NPCTraitType.Hardy => "More resistant to illness and injury",
                NPCTraitType.QuickLearner => "Gains experience faster",
                NPCTraitType.SlowLearner => "Gains experience slower",
                NPCTraitType.Strong => "More effective at physical tasks",
                NPCTraitType.Weak => "Less effective at physical tasks",
                NPCTraitType.Charismatic => "Better at social interactions",
                NPCTraitType.Shy => "Worse at social interactions",
                _ => Description
            };
        }
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
        Mission,
        Project
    }

    public enum NPCStatus
    {
        Available,        // NPC is available for assignment
        BuildingAssigned, // NPC is assigned to a building (not on a project)
        ProjectAssigned,  // NPC is assigned to a project within a building
        Training,         // NPC is in training
        OnMission,        // NPC is on a mission
        Unavailable       // NPC is unavailable due to health conditions
    }

    // Class to hold name data from JSON file
    public class NameData
    {
        [JsonPropertyName("maleNames")]
        public List<string> MaleNames { get; set; } = new List<string>();
        
        [JsonPropertyName("femaleNames")]
        public List<string> FemaleNames { get; set; } = new List<string>();
        
        [JsonPropertyName("surnames")]
        public List<string> Surnames { get; set; } = new List<string>();
    }
} 