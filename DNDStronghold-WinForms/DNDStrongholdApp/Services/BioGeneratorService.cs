using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using DNDStrongholdApp.Models;

namespace DNDStrongholdApp.Services
{
    public class BioGeneratorService
    {
        private BioData _bioData;
        private static Random _random = new Random();

        public BioGeneratorService()
        {
            LoadBioData();
        }

        private void LoadBioData()
        {
            try
            {
                string[] possiblePaths = new[]
                {
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "BioData.json"),
                    Path.Combine(Directory.GetCurrentDirectory(), "Data", "BioData.json"),
                    Path.Combine(Directory.GetCurrentDirectory(), "..", "Data", "BioData.json"),
                    Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "Data", "BioData.json")
                };

                string jsonPath = possiblePaths.FirstOrDefault(File.Exists);
                
                if (!string.IsNullOrEmpty(jsonPath))
                {
                    string json = File.ReadAllText(jsonPath);
                    _bioData = JsonSerializer.Deserialize<BioData>(json);
                    
                    // Verify data was loaded properly
                    if (_bioData?.BioSections?.Backgrounds == null || !_bioData.BioSections.Backgrounds.Any())
                    {
                        _bioData = CreateDefaultBioData();
                    }
                }
                else
                {
                    // Create default bio data if file doesn't exist
                    _bioData = CreateDefaultBioData();
                }
            }
            catch (Exception)
            {
                // Fallback to default data if loading fails
                _bioData = CreateDefaultBioData();
            }
        }

        public string GenerateBio(NPC npc)
        {
            if (_bioData?.BioSections == null) return "A mysterious individual with an unknown past.";

            var sections = new List<string>();

            // Generate each section
            var background = SelectRandomOption(_bioData.BioSections.Backgrounds, npc);
            if (!string.IsNullOrEmpty(background))
                sections.Add(background);

            var personality = SelectRandomOption(_bioData.BioSections.Personalities, npc);
            if (!string.IsNullOrEmpty(personality))
                sections.Add(personality);

            var appearance = SelectRandomOption(_bioData.BioSections.Appearances, npc);
            if (!string.IsNullOrEmpty(appearance))
                sections.Add(appearance);

            var motivation = SelectRandomOption(_bioData.BioSections.Motivations, npc);
            if (!string.IsNullOrEmpty(motivation))
                sections.Add(motivation);

            // Add quirks and secrets with lower probability
            if (_random.NextDouble() < 0.7) // 70% chance of quirk
            {
                var quirk = SelectRandomOption(_bioData.BioSections.Quirks, npc);
                if (!string.IsNullOrEmpty(quirk))
                    sections.Add(quirk);
            }

            if (_random.NextDouble() < 0.3) // 30% chance of secret
            {
                var secret = SelectRandomOption(_bioData.BioSections.Secrets, npc);
                if (!string.IsNullOrEmpty(secret))
                    sections.Add(secret);
            }

                    // Join sections with proper spacing
        return string.Join(" ", sections);
    }

    // Public method to get filtered section data for DM Mode
    public Dictionary<string, List<BioOption>> GetFilteredSectionData(NPC npc)
    {
        var sectionData = new Dictionary<string, List<BioOption>>();
        
        if (_bioData?.BioSections != null)
        {
            sectionData["Background"] = _bioData.BioSections.Backgrounds?.Where(option => IsValidForNPC(option, npc)).ToList() ?? new List<BioOption>();
            sectionData["Personality"] = _bioData.BioSections.Personalities?.Where(option => IsValidForNPC(option, npc)).ToList() ?? new List<BioOption>();
            sectionData["Appearance"] = _bioData.BioSections.Appearances?.Where(option => IsValidForNPC(option, npc)).ToList() ?? new List<BioOption>();
            sectionData["Motivation"] = _bioData.BioSections.Motivations?.Where(option => IsValidForNPC(option, npc)).ToList() ?? new List<BioOption>();
            sectionData["Quirk"] = _bioData.BioSections.Quirks?.Where(option => IsValidForNPC(option, npc)).ToList() ?? new List<BioOption>();
            sectionData["Secret"] = _bioData.BioSections.Secrets?.Where(option => IsValidForNPC(option, npc)).ToList() ?? new List<BioOption>();
        }
        else
        {
            // Fallback to empty lists
            foreach (var section in new[] { "Background", "Personality", "Appearance", "Motivation", "Quirk", "Secret" })
            {
                sectionData[section] = new List<BioOption>();
            }
        }
        
        return sectionData;
    }

    // Generate bio with tracking of selected indices
    public string GenerateBioWithIndices(NPC npc, out Dictionary<string, int> selectedIndices)
    {
        selectedIndices = new Dictionary<string, int>();
        
        if (_bioData?.BioSections == null) 
        {
            // Initialize with default indices for all sections
            foreach (var section in new[] { "Background", "Personality", "Appearance", "Motivation", "Quirk", "Secret" })
            {
                selectedIndices[section] = 0;
            }
            return "A mysterious individual with an unknown past.";
        }

        var sections = new List<string>();

        // Get filtered section data
        var sectionData = GetFiltereredSectionData(npc);

        // Generate each section with index tracking
        var background = SelectRandomOptionWithIndex(sectionData["Background"], npc, out int backgroundIndex);
        selectedIndices["Background"] = backgroundIndex;
        if (!string.IsNullOrEmpty(background))
            sections.Add(background);

        var personality = SelectRandomOptionWithIndex(sectionData["Personality"], npc, out int personalityIndex);
        selectedIndices["Personality"] = personalityIndex;
        if (!string.IsNullOrEmpty(personality))
            sections.Add(personality);

        var appearance = SelectRandomOptionWithIndex(sectionData["Appearance"], npc, out int appearanceIndex);
        selectedIndices["Appearance"] = appearanceIndex;
        if (!string.IsNullOrEmpty(appearance))
            sections.Add(appearance);

        var motivation = SelectRandomOptionWithIndex(sectionData["Motivation"], npc, out int motivationIndex);
        selectedIndices["Motivation"] = motivationIndex;
        if (!string.IsNullOrEmpty(motivation))
            sections.Add(motivation);

        // Add quirks and secrets with lower probability
        if (_random.NextDouble() < 0.7) // 70% chance of quirk
        {
            var quirk = SelectRandomOptionWithIndex(sectionData["Quirk"], npc, out int quirkIndex);
            selectedIndices["Quirk"] = quirkIndex;
            if (!string.IsNullOrEmpty(quirk))
                sections.Add(quirk);
        }
        else
        {
            selectedIndices["Quirk"] = 0; // No quirk selected
        }

        if (_random.NextDouble() < 0.3) // 30% chance of secret
        {
            var secret = SelectRandomOptionWithIndex(sectionData["Secret"], npc, out int secretIndex);
            selectedIndices["Secret"] = secretIndex;
            if (!string.IsNullOrEmpty(secret))
                sections.Add(secret);
        }
        else
        {
            selectedIndices["Secret"] = 0; // No secret selected
        }

        // Join sections with proper spacing
        return string.Join(" ", sections);
    }

    // Generate bio from specific section indices
    public string GenerateBioFromIndices(NPC npc, Dictionary<string, int> indices)
    {
        if (_bioData?.BioSections == null) return "A mysterious individual with an unknown past.";

        var sections = new List<string>();
        var sectionData = GetFiltereredSectionData(npc);

        foreach (var sectionName in new[] { "Background", "Personality", "Appearance", "Motivation", "Quirk", "Secret" })
        {
            if (indices.ContainsKey(sectionName) && sectionData.ContainsKey(sectionName))
            {
                var options = sectionData[sectionName];
                int index = indices[sectionName];
                
                if (options.Any() && index >= 0 && index < options.Count)
                {
                    var selectedOption = options[index];
                    string processedText = ProcessPlaceholders(selectedOption.Text, npc);
                    if (!string.IsNullOrEmpty(processedText))
                        sections.Add(processedText);
                }
            }
        }

        return string.Join(" ", sections);
    }

    private Dictionary<string, List<BioOption>> GetFiltereredSectionData(NPC npc)
    {
        return GetFilteredSectionData(npc);
    }

    private string SelectRandomOptionWithIndex(List<BioOption> options, NPC npc, out int selectedIndex)
    {
        selectedIndex = 0;
        
        if (options == null || !options.Any()) return "";

        // Weight-based selection
        double totalWeight = options.Sum(o => o.Weight);
        double randomValue = _random.NextDouble() * totalWeight;
        double currentWeight = 0;

        for (int i = 0; i < options.Count; i++)
        {
            currentWeight += options[i].Weight;
            if (randomValue <= currentWeight)
            {
                selectedIndex = i;
                return ProcessPlaceholders(options[i].Text, npc);
            }
        }

        // Fallback to last option
        selectedIndex = options.Count - 1;
        return ProcessPlaceholders(options.Last().Text, npc);
    }

    private string SelectRandomOption(List<BioOption> options, NPC npc)
        {
            if (options == null || !options.Any()) return "";

            // Filter options based on NPC type and gender
            var validOptions = options.Where(option => 
                IsValidForNPC(option, npc)).ToList();

            if (!validOptions.Any()) return "";

            // Weight-based selection
            double totalWeight = validOptions.Sum(o => o.Weight);
            double randomValue = _random.NextDouble() * totalWeight;
            double currentWeight = 0;

            foreach (var option in validOptions)
            {
                currentWeight += option.Weight;
                if (randomValue <= currentWeight)
                {
                    return ProcessPlaceholders(option.Text, npc);
                }
            }

            // Fallback to last option
            return ProcessPlaceholders(validOptions.Last().Text, npc);
        }

        private bool IsValidForNPC(BioOption option, NPC npc)
        {
            // Check gender
            if (option.Gender != "any")
            {
                if (option.Gender == "male" && npc.Gender != NPCGender.Male) return false;
                if (option.Gender == "female" && npc.Gender != NPCGender.Female) return false;
            }

            // Check NPC type
            if (option.NpcTypes != null && option.NpcTypes.Any())
            {
                if (!option.NpcTypes.Contains("any") && !option.NpcTypes.Contains(npc.Type.ToString()))
                    return false;
            }

            return true;
        }

        private string ProcessPlaceholders(string text, NPC npc)
        {
            string result = text;

            // Replace name placeholders
            result = result.Replace("{name}", npc.Name);
            result = result.Replace("{Name}", npc.Name);

            // Replace gender-specific pronouns
            if (npc.Gender == NPCGender.Male)
            {
                result = result.Replace("{he/she}", "he");
                result = result.Replace("{He/She}", "He");
                result = result.Replace("{his/her}", "his");
                result = result.Replace("{His/Her}", "His");
                result = result.Replace("{him/her}", "him");
                result = result.Replace("{Him/Her}", "Him");
                result = result.Replace("{himself/herself}", "himself");
                result = result.Replace("{Himself/Herself}", "Himself");
            }
            else
            {
                result = result.Replace("{he/she}", "she");
                result = result.Replace("{He/She}", "She");
                result = result.Replace("{his/her}", "her");
                result = result.Replace("{His/Her}", "Her");
                result = result.Replace("{him/her}", "her");
                result = result.Replace("{Him/Her}", "Her");
                result = result.Replace("{himself/herself}", "herself");
                result = result.Replace("{Himself/Herself}", "Herself");
            }

            return result;
        }

        private BioData CreateDefaultBioData()
        {
            return new BioData
            {
                BioSections = new BioSections
                {
                    Backgrounds = new List<BioOption>
                    {
                        new BioOption { Text = "{Name} comes from humble beginnings.", Weight = 1.0, NpcTypes = new List<string> { "any" }, Gender = "any" }
                    },
                    Personalities = new List<BioOption>
                    {
                        new BioOption { Text = "{He/She} is a hardworking individual.", Weight = 1.0, NpcTypes = new List<string> { "any" }, Gender = "any" }
                    },
                    Appearances = new List<BioOption>
                    {
                        new BioOption { Text = "{He/She} has the look of someone who works with their hands.", Weight = 1.0, NpcTypes = new List<string> { "any" }, Gender = "any" }
                    },
                    Motivations = new List<BioOption>
                    {
                        new BioOption { Text = "{He/She} works to support {his/her} family.", Weight = 1.0, NpcTypes = new List<string> { "any" }, Gender = "any" }
                    },
                    Quirks = new List<BioOption>
                    {
                        new BioOption { Text = "{He/She} has a habit of whistling while working.", Weight = 0.5, NpcTypes = new List<string> { "any" }, Gender = "any" }
                    },
                    Secrets = new List<BioOption>
                    {
                        new BioOption { Text = "{He/She} keeps a small journal of daily events.", Weight = 0.3, NpcTypes = new List<string> { "any" }, Gender = "any" }
                    }
                }
            };
        }
    }

    // Data models for JSON deserialization
    public class BioData
    {
        [JsonPropertyName("bioSections")]
        public BioSections BioSections { get; set; }
    }

    public class BioSections
    {
        [JsonPropertyName("backgrounds")]
        public List<BioOption> Backgrounds { get; set; } = new List<BioOption>();
        
        [JsonPropertyName("personalities")]
        public List<BioOption> Personalities { get; set; } = new List<BioOption>();
        
        [JsonPropertyName("appearances")]
        public List<BioOption> Appearances { get; set; } = new List<BioOption>();
        
        [JsonPropertyName("motivations")]
        public List<BioOption> Motivations { get; set; } = new List<BioOption>();
        
        [JsonPropertyName("quirks")]
        public List<BioOption> Quirks { get; set; } = new List<BioOption>();
        
        [JsonPropertyName("secrets")]
        public List<BioOption> Secrets { get; set; } = new List<BioOption>();
    }

    public class BioOption
    {
        [JsonPropertyName("text")]
        public string Text { get; set; } = "";
        
        [JsonPropertyName("weight")]
        public double Weight { get; set; } = 1.0;
        
        [JsonPropertyName("npcTypes")]
        public List<string> NpcTypes { get; set; } = new List<string>();
        
        [JsonPropertyName("gender")]
        public string Gender { get; set; } = "any";
    }
} 