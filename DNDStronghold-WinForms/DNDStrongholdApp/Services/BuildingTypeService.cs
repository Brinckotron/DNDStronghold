using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using DNDStrongholdApp.Models;

namespace DNDStrongholdApp.Services
{
    public class BuildingTypeService
    {
        private static BuildingTypeService _instance;
        private List<string> _availableBuildingTypes;
        private string _jsonPath;

        private BuildingTypeService()
        {
            InitializeBuildingTypes();
        }

        public static BuildingTypeService GetInstance()
        {
            if (_instance == null)
            {
                _instance = new BuildingTypeService();
            }
            return _instance;
        }

        private void InitializeBuildingTypes()
        {
            _availableBuildingTypes = new List<string>();
            
            // Try multiple possible locations for the JSON file
            string[] possiblePaths = new[]
            {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "BuildingData.json"),
                Path.Combine(Directory.GetCurrentDirectory(), "Data", "BuildingData.json"),
                Path.Combine(Directory.GetCurrentDirectory(), "..", "Data", "BuildingData.json"),
                Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "Data", "BuildingData.json")
            };

            _jsonPath = possiblePaths.FirstOrDefault(File.Exists);

            if (!string.IsNullOrEmpty(_jsonPath) && File.Exists(_jsonPath))
            {
                LoadBuildingTypesFromJson();
            }
            else
            {
                LoadBuildingTypesFromEnum();
            }
        }

        private void LoadBuildingTypesFromJson()
        {
            try
            {
                string json = File.ReadAllText(_jsonPath);
                var buildingData = JsonSerializer.Deserialize<BuildingData>(json);
                if (buildingData?.buildings != null)
                {
                    _availableBuildingTypes = buildingData.buildings
                        .Select(b => b.type)
                        .Where(type => !string.IsNullOrEmpty(type))
                        .Distinct()
                        .OrderBy(type => type)
                        .ToList();
                }
                
                // If no building types found in JSON, fall back to enum
                if (_availableBuildingTypes.Count == 0)
                {
                    LoadBuildingTypesFromEnum();
                }
            }
            catch (Exception)
            {
                // If there's an error reading/parsing the JSON, fall back to enum
                LoadBuildingTypesFromEnum();
            }
        }

        private void LoadBuildingTypesFromEnum()
        {
            // Fallback to enum values for backward compatibility
            _availableBuildingTypes = Enum.GetNames(typeof(BuildingType)).ToList();
        }

        /// <summary>
        /// Gets all available building types
        /// </summary>
        public List<string> GetAvailableBuildingTypes()
        {
            return new List<string>(_availableBuildingTypes);
        }

        /// <summary>
        /// Checks if a building type is valid/available
        /// </summary>
        public bool IsBuildingTypeValid(string typeName)
        {
            if (string.IsNullOrWhiteSpace(typeName))
                return false;

            return _availableBuildingTypes.Contains(typeName, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Adds a new building type to the available list
        /// </summary>
        public bool AddBuildingType(string typeName)
        {
            if (string.IsNullOrWhiteSpace(typeName))
                return false;

            // Check if it already exists (case insensitive)
            if (_availableBuildingTypes.Any(t => string.Equals(t, typeName, StringComparison.OrdinalIgnoreCase)))
                return false;

            _availableBuildingTypes.Add(typeName);
            _availableBuildingTypes.Sort();
            return true;
        }

        /// <summary>
        /// Removes a building type from the available list
        /// </summary>
        public bool RemoveBuildingType(string typeName)
        {
            if (string.IsNullOrWhiteSpace(typeName))
                return false;

            var typeToRemove = _availableBuildingTypes.FirstOrDefault(t => 
                string.Equals(t, typeName, StringComparison.OrdinalIgnoreCase));

            if (typeToRemove != null)
            {
                _availableBuildingTypes.Remove(typeToRemove);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Refreshes the building types list from the JSON file
        /// </summary>
        public void RefreshBuildingTypes()
        {
            InitializeBuildingTypes();
        }

        /// <summary>
        /// Gets building type suggestions that match the input (for autocomplete)
        /// </summary>
        public List<string> GetBuildingTypeSuggestions(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return GetAvailableBuildingTypes();

            return _availableBuildingTypes
                .Where(type => type.Contains(input, StringComparison.OrdinalIgnoreCase))
                .OrderBy(type => type)
                .ToList();
        }

        /// <summary>
        /// Validates a building type name for creation (checks format, length, etc.)
        /// </summary>
        public (bool IsValid, string ErrorMessage) ValidateBuildingTypeName(string typeName)
        {
            if (string.IsNullOrWhiteSpace(typeName))
                return (false, "Building type name cannot be empty.");

            if (typeName.Length < 2)
                return (false, "Building type name must be at least 2 characters long.");

            if (typeName.Length > 50)
                return (false, "Building type name cannot exceed 50 characters.");

            // Check for invalid characters (only allow letters, numbers, spaces, and basic punctuation)
            if (!typeName.All(c => char.IsLetterOrDigit(c) || char.IsWhiteSpace(c) || c == '-' || c == '_' || c == '\''))
                return (false, "Building type name contains invalid characters. Only letters, numbers, spaces, hyphens, underscores, and apostrophes are allowed.");

            // Check if it already exists
            if (_availableBuildingTypes.Any(t => string.Equals(t, typeName, StringComparison.OrdinalIgnoreCase)))
                return (false, $"Building type '{typeName}' already exists.");

            return (true, string.Empty);
        }

        /// <summary>
        /// Gets the path to the building data JSON file
        /// </summary>
        public string GetBuildingDataPath()
        {
            return _jsonPath;
        }
    }
}
