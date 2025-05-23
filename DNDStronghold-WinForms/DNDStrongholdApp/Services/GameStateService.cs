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
        public static GameStateService GetInstance(bool populateTestStronghold = false)
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    if (_instance == null)
                    {
                        _instance = new GameStateService(populateTestStronghold);
                    }
                }
            }
            return _instance;
        }
        
        // Private constructor for singleton
        private GameStateService(bool populateTestStronghold = false)
        {
            try
            {
                if (Program.DebugMode)
                    MessageBox.Show("Starting GameStateService initialization...", "Debug");
                if (populateTestStronghold)
                {
                    // Use the test stronghold data
                    _currentStronghold = TestStrongholdData.GetTestStronghold();
                }
                else
                {
                // Initialize with a new stronghold
                CreateNewStronghold();
                }
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
            // No default buildings
        }
        
        // Add initial NPCs
        private void AddInitialNPCs()
        {
            // No default NPCs
        }
        
        // Add a new building to the stronghold
        public void AddBuilding(Building building)
        {
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
                $"A new building has been planned for construction."
            ));
            
            OnGameStateChanged();
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
        private void OnGameStateChanged()
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