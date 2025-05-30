using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Windows.Forms;
using DNDStrongholdApp.Models;
using System.Linq;

namespace DNDStrongholdApp.Services
{
    public class GameStateService
    {
        private Stronghold _currentStronghold;
        
        // Event raised when the game state changes
        public event EventHandler GameStateChanged;
        
        // Singleton instance
        private static readonly object _lock = new object();
        private static GameStateService _instance;
        
        // DM Mode flag
        private bool _dmMode = false;
        public bool DMMode
        {
            get => _dmMode;
            set
            {
                if (_dmMode != value)
                {
                    _dmMode = value;
                    OnGameStateChanged();
                }
            }
        }
        
        public static GameStateService GetInstance()
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    if (_instance == null)
                    {
                        _instance = new GameStateService();
                    }
                }
            }
            return _instance;
        }
        
        // Private constructor for singleton
        private GameStateService()
        {
            try
            {
                if (Program.DebugMode)
                    MessageBox.Show("Starting GameStateService initialization...", "Debug");
                
                // Initialize with a new stronghold
                CreateNewStronghold();
                
                if (Program.DebugMode)
                    MessageBox.Show("GameStateService initialization complete.", "Debug");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error in GameStateService initialization: {ex.Message}\n\nStack Trace:\n{ex.StackTrace}", 
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                throw;
            }
        }
        
        // Get current stronghold
        public Stronghold GetCurrentStronghold()
        {
            return _currentStronghold;
        }
        
        // Create a new stronghold
        public void CreateNewStronghold(
            string name = "New Stronghold", 
            string location = "Unknown",
            List<Building>? initialBuildings = null,
            List<NPC>? initialNPCs = null,
            Dictionary<ResourceType, int>? initialResources = null)
        {
            _currentStronghold = new Stronghold
            {
                Name = name,
                Location = location
            };
            
            // Clear default resources
            _currentStronghold.Resources.Clear();
            
            // Add resources based on initialResources or defaults
            if (initialResources != null)
            {
                foreach (var resource in initialResources)
                {
                    _currentStronghold.Resources.Add(new Resource 
                    { 
                        Type = resource.Key, 
                        Amount = resource.Value 
                    });
                }
            }
            else
            {
                // Add default resources
                _currentStronghold.Resources.Add(new Resource { Type = ResourceType.Gold, Amount = 500 });
                _currentStronghold.Resources.Add(new Resource { Type = ResourceType.Food, Amount = 100 });
                _currentStronghold.Resources.Add(new Resource { Type = ResourceType.Wood, Amount = 50 });
                _currentStronghold.Resources.Add(new Resource { Type = ResourceType.Stone, Amount = 30 });
                _currentStronghold.Resources.Add(new Resource { Type = ResourceType.Iron, Amount = 10 });
            }
            
            // Add buildings if provided, otherwise add default buildings
            if (initialBuildings != null && initialBuildings.Count > 0)
            {
                _currentStronghold.Buildings.AddRange(initialBuildings);
            }
            else
            {
                AddInitialBuildings();
            }
            
            // Add NPCs if provided, otherwise add default NPCs
            if (initialNPCs != null && initialNPCs.Count > 0)
            {
                _currentStronghold.NPCs.AddRange(initialNPCs);
            }
            else
            {
                AddInitialNPCs();
            }
            
            // Add initial journal entry
            _currentStronghold.Journal.Add(new JournalEntry(
                _currentStronghold.CurrentWeek,
                _currentStronghold.YearsSinceFoundation,
                JournalEntryType.Event,
                "Stronghold Founded",
                $"The stronghold {_currentStronghold.Name} has been founded in {_currentStronghold.Location}."
            ));
            
            // Notify listeners that the game state has changed
            OnGameStateChanged();

            // Assign farmer to farm
            var farmer = _currentStronghold.NPCs.Find(n => n.Type == NPCType.Farmer);
            var farm = _currentStronghold.Buildings.Find(b => b.Type == BuildingType.Farm);
            
            if (farmer != null && farm != null)
            {
                farmer.Assignment = new NPCAssignment
                {
                    Type = AssignmentType.Building,
                    TargetId = farm.Id,
                    TargetName = farm.Name
                };
                
                farm.AssignedWorkers.Add(farmer.Id);
            }

            // Assign one unassigned worker to the Watchtower if it is UnderConstruction
            var watchtower = _currentStronghold.Buildings.FirstOrDefault(b => b.Type == BuildingType.Watchtower && b.ConstructionStatus == BuildingStatus.UnderConstruction);
            var unassignedWorker = _currentStronghold.NPCs.FirstOrDefault(n => n.Assignment.Type == AssignmentType.Unassigned);
            if (watchtower != null && unassignedWorker != null)
            {
                unassignedWorker.Assignment = new NPCAssignment
                {
                    Type = AssignmentType.Building,
                    TargetId = watchtower.Id,
                    TargetName = watchtower.Name
                };
                watchtower.AssignedWorkers.Add(unassignedWorker.Id);
            }
        }
        
        // Advance the game by one week
        public void AdvanceWeek()
        {
            // Store previous resource amounts for reporting
            Dictionary<ResourceType, int> previousResourceAmounts = new Dictionary<ResourceType, int>();
            foreach (var resource in _currentStronghold.Resources)
            {
                previousResourceAmounts[resource.Type] = resource.Amount;
            }
            
            // Advance the week
            _currentStronghold.AdvanceWeek();

            // Award XP to building workers for skills
            foreach (var building in _currentStronghold.Buildings)
            {
                building.AwardWorkerSkillXP(_currentStronghold.NPCs);
            }

            // Process buildings
            ProcessConstructionAndRepairs();
            
            // Process missions
            ProcessMissions();
            
            // Process resources
            ProcessResources();
            
            // Generate weekly report
            GenerateWeeklyReport(previousResourceAmounts);
            
            // Notify listeners that the game state has changed
            OnGameStateChanged();
        }
        
        // Process construction and repairs
        private void ProcessConstructionAndRepairs()
        {
            foreach (var building in _currentStronghold.Buildings)
            {
                // Check if building in Planning state has workers assigned
                if (building.ConstructionStatus == BuildingStatus.Planning && building.AssignedWorkers.Any())
                {
                    // Start construction if workers are assigned
                    if (building.StartConstruction())
                        {
                        // Calculate and apply first week's construction points
                        building.UpdateConstructionProgress(_currentStronghold.NPCs);
                        building.AdvanceConstruction();
                    }
                }
                // Update construction progress for buildings already under construction
                else if (building.ConstructionStatus == BuildingStatus.UnderConstruction)
                {
                    building.UpdateConstructionProgress(_currentStronghold.NPCs);
                    building.AdvanceConstruction();
                }
                
                // Process repairs
                if (building.ConstructionStatus == BuildingStatus.Repairing)
                {
                    building.UpdateConstructionProgress(_currentStronghold.NPCs);
                    building.AdvanceRepair();
                }
                
                // Process upgrades
                if (building.ConstructionStatus == BuildingStatus.Upgrading)
                {
                    building.UpdateConstructionProgress(_currentStronghold.NPCs);
                    building.AdvanceUpgrade();
                }
            }
        }
        
        // Process missions (progress, completion, etc.)
        private void ProcessMissions()
        {
            List<Mission> completedMissions = new List<Mission>();
            
            foreach (var mission in _currentStronghold.ActiveMissions)
            {
                bool completed = mission.AdvanceProgress();
                
                if (completed)
                {
                    completedMissions.Add(mission);
                    
                    // Add journal entry for completed mission
                    _currentStronghold.Journal.Add(new JournalEntry(
                        _currentStronghold.CurrentWeek,
                        _currentStronghold.YearsSinceFoundation,
                        JournalEntryType.MissionComplete,
                        $"Mission {mission.Name} {mission.Status}",
                        $"The mission {mission.Name} has been {mission.Status.ToString().ToLower()}."
                    ));
                    
                    // If mission was successful, add rewards
                    if (mission.Status == MissionStatus.Completed)
                    {
                        // Add resources
                        foreach (var reward in mission.Rewards.Resources)
                        {
                            var resource = _currentStronghold.Resources.Find(r => r.Type == reward.ResourceType);
                            if (resource != null)
                            {
                                resource.Amount += reward.Amount;
                            }
                        }
                        
                        // Add reputation
                        _currentStronghold.Reputation += mission.Rewards.ReputationGain;
                    }
                }
            }
            
            // Remove completed missions from active list
            foreach (var mission in completedMissions)
            {
                _currentStronghold.ActiveMissions.Remove(mission);
            }
        }
        
        // Process resources (production, consumption, etc.)
        private void ProcessResources()
        {
            // Reset all resource rates
            foreach (var resource in _currentStronghold.Resources)
            {
                resource.WeeklyProduction = 0;
                resource.WeeklyConsumption = 0;
                resource.Sources.Clear();
            }

            // Aggregate building production and upkeep
            foreach (var building in _currentStronghold.Buildings)
            {
                if (building.IsFunctional())
                {
                    // Production
                    foreach (var prod in building.ActualProduction)
                    {
                        var resource = _currentStronghold.Resources.Find(r => r.Type == prod.ResourceType);
                        if (resource != null && prod.Amount > 0)
                        {
                            resource.WeeklyProduction += prod.Amount;
                            resource.Sources.Add(new ResourceSource
                            {
                                SourceType = ResourceSourceType.Building,
                                SourceId = building.Id,
                                SourceName = building.Name,
                                Amount = prod.Amount,
                                IsProduction = true
                            });
                        }
                    }
                    // Upkeep
                    foreach (var upkeep in building.ActualUpkeep)
                    {
                        var resource = _currentStronghold.Resources.Find(r => r.Type == upkeep.ResourceType);
                        if (resource != null && upkeep.Amount > 0)
                        {
                            resource.WeeklyConsumption += upkeep.Amount;
                            resource.Sources.Add(new ResourceSource
                            {
                                SourceType = ResourceSourceType.Building,
                                SourceId = building.Id,
                                SourceName = building.Name,
                                Amount = upkeep.Amount,
                                IsProduction = false
                            });
                        }
                    }
                }
            }

            // Food consumption: 1 per NPC (assigned or not)
            var foodResource = _currentStronghold.Resources.Find(r => r.Type == ResourceType.Food);
            if (foodResource != null)
            {
                int foodPerNPC = 1;
                int totalNPCs = _currentStronghold.NPCs.Count;
                foodResource.WeeklyConsumption += totalNPCs * foodPerNPC;
                if (totalNPCs > 0)
                {
                    foodResource.Sources.Add(new ResourceSource
                    {
                        SourceType = ResourceSourceType.Manual,
                        SourceId = "NPCs",
                        SourceName = "Population",
                        Amount = totalNPCs * foodPerNPC,
                        IsProduction = false
                    });
                }
                // TODO: Add special building food consumption (tavern, inn, etc.)
            }
        }
        
        // Generate weekly report
        private void GenerateWeeklyReport(Dictionary<ResourceType, int> previousResourceAmounts)
        {
            WeeklyReport report = new WeeklyReport(_currentStronghold.CurrentWeek, _currentStronghold.YearsSinceFoundation);
            
            // Add resource changes
            foreach (var resource in _currentStronghold.Resources)
            {
                int previousAmount = previousResourceAmounts.ContainsKey(resource.Type) ? previousResourceAmounts[resource.Type] : 0;
                
                report.ResourceChanges.Add(new ResourceChange
                {
                    ResourceType = resource.Type,
                    PreviousAmount = previousAmount,
                    CurrentAmount = resource.Amount
                });
            }
            
            // Add income/expense summary
            var goldResource = _currentStronghold.Resources.Find(r => r.Type == ResourceType.Gold);
            if (goldResource != null)
            {
                report.IncomeExpenseSummary.TotalIncome = goldResource.WeeklyProduction;
                report.IncomeExpenseSummary.TotalExpenses = goldResource.WeeklyConsumption;
            }
            
            // Add upcoming completions
            foreach (var building in _currentStronghold.Buildings)
            {
                if (building.ConstructionStatus == BuildingStatus.UnderConstruction)
                {
                    report.UpcomingCompletions.Add(new UpcomingCompletion
                    {
                        Id = building.Id,
                        Name = building.Name,
                        Type = "Building Construction",
                        WeeksRemaining = building.ConstructionTimeRemaining
                    });
                }
                else if (building.ConstructionStatus == BuildingStatus.Repairing)
                {
                    report.UpcomingCompletions.Add(new UpcomingCompletion
                    {
                        Id = building.Id,
                        Name = building.Name,
                        Type = "Building Repair",
                        WeeksRemaining = building.RepairTimeRemaining
                    });
                }
            }
            
            foreach (var mission in _currentStronghold.ActiveMissions)
            {
                report.UpcomingCompletions.Add(new UpcomingCompletion
                {
                    Id = mission.Id,
                    Name = mission.Name,
                    Type = "Mission",
                    WeeksRemaining = mission.WeeksRemaining
                });
            }
            
            // Set current weekly report
            _currentStronghold.CurrentWeeklyReport = report;
            
            // Add journal entry for weekly report
            _currentStronghold.Journal.Add(new JournalEntry(
                _currentStronghold.CurrentWeek,
                _currentStronghold.YearsSinceFoundation,
                JournalEntryType.WeeklyReport,
                $"Week {_currentStronghold.CurrentWeek} Report",
                report.GenerateSummary()
            ));
        }
        
        // Save game to file
        public void SaveGame(string filePath)
        {
            try
            {
                string json = JsonSerializer.Serialize(_currentStronghold, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                
                File.WriteAllText(filePath, json);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving game: {ex.Message}", "Save Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        
        // Load game from file
        public void LoadGame(string filePath)
        {
            try
            {
                string json = File.ReadAllText(filePath);
                _currentStronghold = JsonSerializer.Deserialize<Stronghold>(json);
                
                // Notify listeners that the game state has changed
                OnGameStateChanged();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading game: {ex.Message}", "Load Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        
        // Add initial buildings
        private void AddInitialBuildings()
        {
            // Add one of each building type from BuildingData.json
            var farm = new Building(BuildingType.Farm) { 
                Name = "Central Farm",
                ConstructionStatus = BuildingStatus.Complete
            };
            var watchtower = new Building(BuildingType.Watchtower) { 
                Name = "North Watchtower",
                ConstructionStatus = BuildingStatus.Complete
            };
            var smithy = new Building(BuildingType.Smithy) { 
                Name = "Town Smithy",
                ConstructionStatus = BuildingStatus.Complete
            };
            var laboratory = new Building(BuildingType.Laboratory) { 
                Name = "Research Laboratory",
                ConstructionStatus = BuildingStatus.Complete
            };

            // Add buildings to stronghold
            _currentStronghold.Buildings.Add(farm);
            _currentStronghold.Buildings.Add(watchtower);
            _currentStronghold.Buildings.Add(smithy);
            _currentStronghold.Buildings.Add(laboratory);
        }
        
        // Add initial NPCs
        private void AddInitialNPCs()
        {
            // Create 20 NPCs with a mix of types
            var npcTypes = new[]
            {
                NPCType.Peasant, NPCType.Peasant, NPCType.Peasant, NPCType.Peasant, // 4 peasants
                NPCType.Laborer, NPCType.Laborer, NPCType.Laborer, // 3 laborers
                NPCType.Farmer, NPCType.Farmer, NPCType.Farmer, // 3 farmers
                NPCType.Militia, NPCType.Militia, // 2 militia
                NPCType.Scout, NPCType.Scout, // 2 scouts
                NPCType.Artisan, NPCType.Artisan, // 2 artisans
                NPCType.Scholar, NPCType.Scholar, // 2 scholars
                NPCType.Merchant, NPCType.Merchant // 2 merchants
            };

            foreach (var type in npcTypes)
            {
                var npc = new NPC(type);
                _currentStronghold.NPCs.Add(npc);
            }

            // Assign NPCs to buildings
            var buildings = _currentStronghold.Buildings;
            
            // Assign farmers to farm
            var farm = buildings.Find(b => b.Type == BuildingType.Farm);
            var farmers = _currentStronghold.NPCs.Where(n => n.Type == NPCType.Farmer).Take(3).ToList();
            if (farm != null && farmers.Any())
            {
                foreach (var farmer in farmers)
                {
                    farmer.Assignment = new NPCAssignment
                    {
                        Type = AssignmentType.Building,
                        TargetId = farm.Id,
                        TargetName = farm.Name
                    };
                    farm.AssignedWorkers.Add(farmer.Id);
                }
            }

            // Assign militia and scouts to watchtower
            var watchtower = buildings.Find(b => b.Type == BuildingType.Watchtower);
            var guards = _currentStronghold.NPCs.Where(n => n.Type == NPCType.Militia || n.Type == NPCType.Scout).Take(2).ToList();
            if (watchtower != null && guards.Any())
            {
                foreach (var guard in guards)
                {
                    guard.Assignment = new NPCAssignment
                    {
                        Type = AssignmentType.Building,
                        TargetId = watchtower.Id,
                        TargetName = watchtower.Name
                    };
                    watchtower.AssignedWorkers.Add(guard.Id);
                }
            }

            // Assign artisans to smithy
            var smithy = buildings.Find(b => b.Type == BuildingType.Smithy);
            var artisans = _currentStronghold.NPCs.Where(n => n.Type == NPCType.Artisan).Take(2).ToList();
            if (smithy != null && artisans.Any())
            {
                foreach (var artisan in artisans)
                {
                    artisan.Assignment = new NPCAssignment
                    {
                        Type = AssignmentType.Building,
                        TargetId = smithy.Id,
                        TargetName = smithy.Name
                    };
                    smithy.AssignedWorkers.Add(artisan.Id);
                }
            }

            // Assign scholars to laboratory
            var laboratory = buildings.Find(b => b.Type == BuildingType.Laboratory);
            var scholars = _currentStronghold.NPCs.Where(n => n.Type == NPCType.Scholar).Take(2).ToList();
            if (laboratory != null && scholars.Any())
            {
                foreach (var scholar in scholars)
                {
                    scholar.Assignment = new NPCAssignment
                    {
                        Type = AssignmentType.Building,
                        TargetId = laboratory.Id,
                        TargetName = laboratory.Name
                    };
                    laboratory.AssignedWorkers.Add(scholar.Id);
                }
            }
        }
        
        // Add a new building to the stronghold
        public bool AddBuildingAndDeductCosts(Building building)
        {
            // In DM Mode, skip resource checks and deductions
            if (!_dmMode)
            {
                // Check if we have enough resources
                if (!HasEnoughResources(building.ConstructionCost))
                {
                    return false;
                }

                // Deduct resources
                foreach (var cost in building.ConstructionCost)
                {
                    var resource = _currentStronghold.Resources.Find(r => r.Type == cost.ResourceType);
                    if (resource != null)
                    {
                        resource.Amount -= cost.Amount;
                    }
                }
            }

            // Add the building
            _currentStronghold.Buildings.Add(building);
            
            // Add journal entry
            string title = string.IsNullOrWhiteSpace(building.Name) || building.Name == building.Type.ToString() ?
                $"{building.Type} construction planned" :
                $"{building.Name} ({building.Type}) construction planned";
                
            _currentStronghold.Journal.Add(new JournalEntry(
                _currentStronghold.CurrentWeek,
                _currentStronghold.YearsSinceFoundation,
                JournalEntryType.BuildingPlanned,
                title,
                $"A new building has been planned for construction. Resources have been allocated."
            ));
            
            OnGameStateChanged();
            return true;
        }

        // Existing AddBuilding method marked as obsolete
        [Obsolete("Use AddBuildingAndDeductCosts instead")]
        public void AddBuilding(Building building)
        {
            AddBuildingAndDeductCosts(building);
        }
        
        // Add a new NPC to the stronghold
        public void AddNPC(NPC npc)
        {
            _currentStronghold.NPCs.Add(npc);
            
            // Add journal entry
            _currentStronghold.Journal.Add(new JournalEntry(
                _currentStronghold.CurrentWeek,
                _currentStronghold.YearsSinceFoundation,
                JournalEntryType.NPCRecruited,
                $"{npc.Name} Joined",
                $"{npc.Name}, a {npc.Type}, has joined the stronghold."
            ));
            
            OnGameStateChanged();
        }
        
        // Assign workers to a building
        public void AssignWorkersToBuilding(string buildingId, List<string> npcIds)
        {
            // Find the building
            var building = _currentStronghold.Buildings.Find(b => b.Id == buildingId);
            if (building == null)
            {
                return;
            }
            
            // First, unassign any workers currently assigned to this building
            foreach (var npc in _currentStronghold.NPCs)
            {
                if (npc.Assignment.Type == AssignmentType.Building && npc.Assignment.TargetId == buildingId)
                {
                    npc.Assignment = new NPCAssignment
                    {
                        Type = AssignmentType.Unassigned,
                        TargetId = string.Empty,
                        TargetName = string.Empty
                    };
                }
            }
            
            // Clear the building's assigned workers list
            building.AssignedWorkers.Clear();
            
            // Assign the new workers
            foreach (var npcId in npcIds)
            {
                var npc = _currentStronghold.NPCs.Find(n => n.Id == npcId);
                if (npc != null)
                {
                    // Assign NPC to building
                    npc.Assignment = new NPCAssignment
                    {
                        Type = AssignmentType.Building,
                        TargetId = buildingId,
                        TargetName = building.Name
                    };
                    
                    // Add NPC to building's worker list
                    building.AssignedWorkers.Add(npcId);
                }
            }

            // If building is in Planning state and has workers assigned, start construction
            if (building.ConstructionStatus == BuildingStatus.Planning && building.AssignedWorkers.Count > 0)
            {
                building.StartConstruction();
            }
            
            // Update construction progress
            building.UpdateConstructionProgress(_currentStronghold.NPCs);
            
            // Add journal entry
            _currentStronghold.Journal.Add(new JournalEntry(
                _currentStronghold.CurrentWeek,
                _currentStronghold.YearsSinceFoundation,
                JournalEntryType.Event,
                $"Workers Assigned to {building.Name}",
                $"{building.AssignedWorkers.Count} workers have been assigned to {building.Name}."
            ));
            
            OnGameStateChanged();
        }
        
        // Cancel building construction and refund costs
        public bool CancelBuildingConstruction(string buildingId)
        {
            var building = _currentStronghold.Buildings.Find(b => b.Id == buildingId);
            if (building == null || building.ConstructionStatus != BuildingStatus.Planning)
            {
                return false;
            }
            
            // Refund construction costs
            foreach (var cost in building.ConstructionCost)
            {
                var resource = _currentStronghold.Resources.Find(r => r.Type == cost.ResourceType);
                if (resource != null)
                {
                    resource.Amount += cost.Amount;
                }
            }
            
            // Add journal entry
            string title = string.IsNullOrWhiteSpace(building.Name) || building.Name == building.Type.ToString() ?
                $"{building.Type} construction canceled" :
                $"{building.Name} ({building.Type}) construction canceled";
                
            _currentStronghold.Journal.Add(new JournalEntry(
                _currentStronghold.CurrentWeek,
                _currentStronghold.YearsSinceFoundation,
                JournalEntryType.Event,
                title,
                $"The planned construction has been canceled and resources have been refunded."
            ));
            
            // Remove the building
            _currentStronghold.Buildings.Remove(building);
            
            OnGameStateChanged();
            return true;
        }
        
        // Notify listeners that the game state has changed
        public void OnGameStateChanged()
        {
            GameStateChanged?.Invoke(this, EventArgs.Empty);
        }

        // Check if we have enough resources for a list of costs
        private bool HasEnoughResources(List<ResourceCost> costs)
        {
            foreach (var cost in costs)
            {
                var resource = _currentStronghold.Resources.Find(r => r.Type == cost.ResourceType);
                if (resource == null || resource.Amount < cost.Amount)
                {
                    return false;
                }
            }
            return true;
        }

        // Public method to check if we can afford to build a building
        public bool CanAffordBuilding(BuildingType buildingType)
        {
            var building = new Building(buildingType);
            return HasEnoughResources(building.ConstructionCost);
        }

        // Start building repair
        public bool StartBuildingRepair(string buildingId)
        {
            var building = _currentStronghold.Buildings.Find(b => b.Id == buildingId);
            if (building == null || building.ConstructionStatus != BuildingStatus.Damaged)
                return false;

            // Start repair process
            if (building.StartRepair(_currentStronghold.Resources))
                {
            // Add journal entry
            _currentStronghold.Journal.Add(new JournalEntry(
                _currentStronghold.CurrentWeek,
                _currentStronghold.YearsSinceFoundation,
                JournalEntryType.Event,
                $"Started repairing {building.Name}",
                    $"Repairs have begun on {building.Name}. Expected completion in {building.ConstructionTimeRemaining} weeks."
            ));

            OnGameStateChanged();
            return true;
            }

            return false;
        }
    }
} 