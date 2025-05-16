using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Windows.Forms;
using DNDStrongholdApp.Models;

namespace DNDStrongholdApp.Services
{
    public class GameStateService
    {
        private Stronghold _currentStronghold;
        
        // Event raised when the game state changes
        public event EventHandler GameStateChanged;
        
        // Singleton instance
        private static GameStateService _instance;
        public static GameStateService Instance => _instance ??= new GameStateService();
        
        // Private constructor for singleton
        private GameStateService()
        {
            // Initialize with a new stronghold
            CreateNewStronghold();
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
            List<Building> initialBuildings = null,
            List<NPC> initialNPCs = null,
            Dictionary<ResourceType, int> initialResources = null)
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
            
            // Process buildings
            ProcessBuildings();
            
            // Process NPCs
            ProcessNPCs();
            
            // Process missions
            ProcessMissions();
            
            // Process resources
            ProcessResources();
            
            // Generate weekly report
            GenerateWeeklyReport(previousResourceAmounts);
            
            // Notify listeners that the game state has changed
            OnGameStateChanged();
        }
        
        // Process buildings (construction progress, production, etc.)
        private void ProcessBuildings()
        {
            List<Building> completedBuildings = new List<Building>();
            List<Building> completedRepairs = new List<Building>();
            
            foreach (var building in _currentStronghold.Buildings)
            {
                if (building.ConstructionStatus == BuildingStatus.UnderConstruction)
                {
                    bool completed = building.AdvanceConstruction();
                    
                    if (completed)
                    {
                        completedBuildings.Add(building);
                        
                        // Add journal entry for completed building
                        _currentStronghold.Journal.Add(new JournalEntry(
                            _currentStronghold.CurrentWeek,
                            _currentStronghold.YearsSinceFoundation,
                            JournalEntryType.BuildingComplete,
                            $"{building.Name} Construction Complete",
                            $"The construction of {building.Name} has been completed."
                        ));
                    }
                }
                else if (building.ConstructionStatus == BuildingStatus.Repairing)
                {
                    bool repaired = building.AdvanceRepair();
                    
                    if (repaired)
                    {
                        completedRepairs.Add(building);
                        
                        // Add journal entry for repaired building
                        _currentStronghold.Journal.Add(new JournalEntry(
                            _currentStronghold.CurrentWeek,
                            _currentStronghold.YearsSinceFoundation,
                            JournalEntryType.BuildingRepaired,
                            $"{building.Name} Repairs Complete",
                            $"The repairs on {building.Name} have been completed."
                        ));
                    }
                }
                else if (building.ConstructionStatus == BuildingStatus.Complete)
                {
                    // Random chance for building to take damage (5% chance)
                    Random random = new Random();
                    if (random.Next(100) < 5)
                    {
                        int damageAmount = random.Next(10, 30);
                        building.Damage(damageAmount);
                        
                        if (building.ConstructionStatus == BuildingStatus.Damaged)
                        {
                            // Add journal entry for damaged building
                            _currentStronghold.Journal.Add(new JournalEntry(
                                _currentStronghold.CurrentWeek,
                                _currentStronghold.YearsSinceFoundation,
                                JournalEntryType.BuildingDamaged,
                                $"{building.Name} Damaged",
                                $"The {building.Name} has been damaged and requires repairs."
                            ));
                        }
                    }
                }
            }
        }
        
        // Start repairs on a building
        public bool StartBuildingRepair(string buildingId)
        {
            var building = _currentStronghold.Buildings.Find(b => b.Id == buildingId);
            if (building == null || building.ConstructionStatus != BuildingStatus.Damaged)
            {
                return false;
            }
            
            bool success = building.StartRepair(_currentStronghold.Resources);
            
            if (success)
            {
                // Add journal entry for repair start
                _currentStronghold.Journal.Add(new JournalEntry(
                    _currentStronghold.CurrentWeek,
                    _currentStronghold.YearsSinceFoundation,
                    JournalEntryType.BuildingRepairStarted,
                    $"{building.Name} Repairs Started",
                    $"Repairs have begun on the {building.Name}."
                ));
                
                // Notify listeners that the game state has changed
                OnGameStateChanged();
            }
            
            return success;
        }
        
        // Process NPCs (happiness, assignments, etc.)
        private void ProcessNPCs()
        {
            bool hasTavern = _currentStronghold.Buildings.Exists(b => 
                b.Type == BuildingType.Tavern && b.ConstructionStatus == BuildingStatus.Complete);
                
            foreach (var npc in _currentStronghold.NPCs)
            {
                // Update happiness
                // For now, assume all NPCs have proper housing and food
                npc.UpdateHappiness(true, true, hasTavern);
                
                // Add some experience
                if (npc.Assignment.Type != AssignmentType.Unassigned)
                {
                    bool leveledUp = npc.AddExperience(10);
                    
                    if (leveledUp)
                    {
                        // Add journal entry for level up
                        _currentStronghold.Journal.Add(new JournalEntry(
                            _currentStronghold.CurrentWeek,
                            _currentStronghold.YearsSinceFoundation,
                            JournalEntryType.Event,
                            $"{npc.Name} Gained a Level",
                            $"{npc.Name} has reached level {npc.Level}."
                        ));
                    }
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
            foreach (var resource in _currentStronghold.Resources)
            {
                resource.UpdateWeeklyRates();
                resource.ApplyWeeklyChange();
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
            // Add a farm
            var farm = new Building(BuildingType.Farm);
            farm.ConstructionStatus = BuildingStatus.Complete;
            _currentStronghold.Buildings.Add(farm);
            
            // Add a watchtower under construction
            var watchtower = new Building(BuildingType.Watchtower);
            watchtower.ConstructionStatus = BuildingStatus.UnderConstruction;
            _currentStronghold.Buildings.Add(watchtower);
        }
        
        // Add initial NPCs
        private void AddInitialNPCs()
        {
            // Add some NPCs
            _currentStronghold.NPCs.Add(new NPC(NPCType.Peasant));
            _currentStronghold.NPCs.Add(new NPC(NPCType.Farmer));
            _currentStronghold.NPCs.Add(new NPC(NPCType.Militia));
            
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
    }
} 