using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Windows.Forms;
using DNDStrongholdApp.Models;
using DNDStrongholdApp.Commands;
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
        
        // Command invoker for managing operations
        private readonly CommandInvoker _commandInvoker = new CommandInvoker();
        
        // DM Mode flag
        private bool _dmMode = false;
        private readonly List<(string BuildingId, string ProjectId)> _pendingProjectCompletions = new();
        private int _turnJournalStartIndex = -1;
        private readonly List<CompletedProject> _turnCompletions = new();
        private readonly List<string> _turnDepartures = new();
        private int _turnMoraleBefore;
        private int _turnPopulationBefore;
        private bool _suppressMoraleJournal;
        private Dictionary<ResourceType, int> _turnPreviousResources;

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

        /// <summary>
        /// Call before pre-turn raids so journal lines from the whole Next Turn
        /// (raid + AdvanceWeek) are attributed to the weekly report.
        /// </summary>
        public void BeginTurnJournalCapture()
        {
            if (_currentStronghold == null) return;
            _turnJournalStartIndex = _currentStronghold.Journal?.Count ?? 0;
            _turnCompletions.Clear();
            _turnDepartures.Clear();
            _turnMoraleBefore = _currentStronghold.CurrentMorale;
            _turnPopulationBefore = _currentStronghold.NPCs.Count;
            _turnPreviousResources = new Dictionary<ResourceType, int>();
            foreach (var resource in _currentStronghold.Resources)
                _turnPreviousResources[resource.Type] = resource.Amount;
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
                
                // Initialize with test data if TestMode is enabled, otherwise create new stronghold
                if (Program.TestMode)
                {
                    LoadTestStrongholdData();
                    if (Program.PlaythroughMode)
                        ApplyPlaythroughWeek4Scenario();
                }
                else
                {
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
        
        // Get command invoker for undo/redo operations
        public CommandInvoker GetCommandInvoker()
        {
            return _commandInvoker;
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
                
                // Set treasury to match gold resource if provided
                if (initialResources.ContainsKey(ResourceType.Gold))
                {
                    _currentStronghold.Treasury = initialResources[ResourceType.Gold];
                }
            }
            else
            {
                // Add default resources with 0 amounts
                _currentStronghold.Resources.Add(new Resource { Type = ResourceType.Gold, Amount = 0 });
                _currentStronghold.Resources.Add(new Resource { Type = ResourceType.Food, Amount = 0 });
                _currentStronghold.Resources.Add(new Resource { Type = ResourceType.Wood, Amount = 0 });
                _currentStronghold.Resources.Add(new Resource { Type = ResourceType.Stone, Amount = 0 });
                _currentStronghold.Resources.Add(new Resource { Type = ResourceType.Iron, Amount = 0 });
                _currentStronghold.Resources.Add(new Resource { Type = ResourceType.Luxury, Amount = 0 });
                
                // Set treasury to 0 as well
                _currentStronghold.Treasury = 0;
            }
            
            // Add buildings if provided
            if (initialBuildings != null && initialBuildings.Count > 0)
            {
                _currentStronghold.Buildings.AddRange(initialBuildings);
            }
            
            // Always add a Keep as the central building for any stronghold
            var keep = new Building("Keep")
            {
                Name = "Keep",
                ConstructionStatus = BuildingStatus.Complete,
                Level = 1
            };
            _currentStronghold.Buildings.Add(keep);
            
            // Ensure the Keep has a Steward assigned
            EnsureKeepHasSteward();
            
            // Add NPCs if provided
            if (initialNPCs != null && initialNPCs.Count > 0)
            {
                _currentStronghold.NPCs.AddRange(initialNPCs);
            }
            
            // Add initial journal entry
            _currentStronghold.Journal.Add(new JournalEntry(
                _currentStronghold.CurrentWeek,
                _currentStronghold.YearsSinceFoundation,
                JournalEntryType.Event,
                "Stronghold Founded",
                $"The stronghold {_currentStronghold.Name} has been founded in {_currentStronghold.Location}."
            ));

            _currentStronghold.EnsureCombatDefaults();
            
            // Notify listeners that the game state has changed
            OnGameStateChanged();
        }
        
        // Advance the game by one week
        public void AdvanceWeek()
        {
            var command = new AdvanceWeekCommand(this);
            _commandInvoker.ExecuteCommand(command);
        }
        
        // Internal method called by AdvanceWeekCommand
        internal void ExecuteAdvanceWeek()
        {
            if (_turnJournalStartIndex < 0)
            {
                _turnJournalStartIndex = _currentStronghold.Journal?.Count ?? 0;
                _turnCompletions.Clear();
                _turnDepartures.Clear();
                _turnMoraleBefore = _currentStronghold.CurrentMorale;
                _turnPopulationBefore = _currentStronghold.NPCs.Count;
                _turnPreviousResources = new Dictionary<ResourceType, int>();
                foreach (var resource in _currentStronghold.Resources)
                    _turnPreviousResources[resource.Type] = resource.Amount;
            }

            Dictionary<ResourceType, int> previousResourceAmounts =
                _turnPreviousResources ?? new Dictionary<ResourceType, int>();
            if (previousResourceAmounts.Count == 0)
            {
                foreach (var resource in _currentStronghold.Resources)
                    previousResourceAmounts[resource.Type] = resource.Amount;
            }
            // Advance the week
            _currentStronghold.AdvanceWeek();

            // Refresh weekly magic pools. Raids resolve before AdvanceWeek, so a raid
            // spends the ending week's points and anything already spent is gone.
            foreach (var npc in _currentStronghold.NPCs)
            {
                npc.HealPointsUsed = 0;
                npc.SpellPointsUsed = 0;
            }

            // Award XP to building workers for skills
            foreach (var building in _currentStronghold.Buildings)
            {
                building.AwardWorkerSkillXP(_currentStronghold.NPCs);
            }

            // Process buildings
            ProcessConstructionAndRepairs();
            
            TradeService.ReconcileOccupancy(_currentStronghold);

            // Process building projects
            ProcessProjects();

            // Process missions
            ProcessMissions();

            // Background trade drift and notable market events
            ProcessTradeRoutes();
            
            // Process resources
            ProcessResources();

            // Freeze what this turn actually produced/spent before rates are refreshed for outlook
            var turnResourceFlows = _currentStronghold.Resources.ToDictionary(
                r => r.Type,
                r => (
                    Produced: r.WeeklyProduction,
                    Consumed: r.WeeklyConsumption,
                    Sources: r.Sources.Select(s => new ResourceChangeBreakdown
                    {
                        Source = s.IsProduction ? $"+ {s.SourceName}" : $"- {s.SourceName}",
                        Amount = s.IsProduction ? s.Amount : -s.Amount
                    }).ToList()
                ));
            
            // Process hunger progression after resource consumption
            ProcessHungerProgression();

            // Grave injuries may kill; any death uses the same morale penalty as raids
            ProcessHealthStates();

            // Bankruptcy: one additional abandonment per week while insolvent
            ProcessBankruptcy();
            
            // Process morale changes
            ProcessWeeklyMorale();

            // Refresh rates for next-week outlook without writing extra journal lines
            _suppressMoraleJournal = true;
            UpdateProductionAndConsumptionRates();
            _suppressMoraleJournal = false;
            
            // Generate weekly report
            GenerateWeeklyReport(previousResourceAmounts, turnResourceFlows);
            _turnJournalStartIndex = -1;
            _turnPreviousResources = null;
            
            // Notify listeners that the game state has changed
            OnGameStateChanged();
        }
        
        // Process construction and repairs
        private void ProcessConstructionAndRepairs()
        {
            foreach (var building in _currentStronghold.Buildings)
            {
                // Check if building in Planning state has workers assigned (regular workers or construction crew)
                if (building.ConstructionStatus == BuildingStatus.Planning && building.GetTotalAssignedWorkers() > 0)
                {
                    // Start construction if workers are assigned
                    if (building.StartConstruction())
                    {
                        // Calculate and apply first week's construction points
                        building.UpdateConstructionProgress(_currentStronghold.NPCs);
                        ApplyMagicAssist(building);
                        building.AdvanceConstruction();
                    }
                }
                // Update construction progress for buildings already under construction
                else if (building.ConstructionStatus == BuildingStatus.UnderConstruction)
                {
                    building.UpdateConstructionProgress(_currentStronghold.NPCs);
                    ApplyMagicAssist(building);
                    if (building.AdvanceConstruction())
                    {
                        ClearConstructionCrewFromBuilding(building.Id);
                        _turnCompletions.Add(new CompletedProject
                        {
                            Id = building.Id,
                            Name = building.Name,
                            Type = "Building Construction"
                        });
                    }
                }
                
                // Process repairs
                if (building.ConstructionStatus == BuildingStatus.Repairing)
                {
                    building.UpdateConstructionProgress(_currentStronghold.NPCs);
                    ApplyMagicAssist(building);
                    if (building.AdvanceRepair())
                    {
                        ClearConstructionCrewFromBuilding(building.Id);
                        _turnCompletions.Add(new CompletedProject
                        {
                            Id = building.Id,
                            Name = building.Name,
                            Type = "Building Repair"
                        });
                    }
                }
                
                // Process upgrades
                if (building.ConstructionStatus == BuildingStatus.Upgrading)
                {
                    building.UpdateConstructionProgress(_currentStronghold.NPCs);
                    ApplyMagicAssist(building);
                    if (building.AdvanceUpgrade())
                    {
                        ClearConstructionCrewFromBuilding(building.Id);
                        _turnCompletions.Add(new CompletedProject
                        {
                            Id = building.Id,
                            Name = building.Name,
                            Type = "Building Upgrade"
                        });
                    }
                }
            }
        }

        // Fold paid-for spellcaster boosts into this week's construction points. Called
        // after UpdateConstructionProgress (which zeroes the weekly total) and before the
        // matching Advance* call that banks it.
        private void ApplyMagicAssist(Building building)
        {
            if (building.PendingMagicAssist == null || building.PendingMagicAssist.Count == 0)
                return;

            int points = MagicService.ConsumeMagicAssist(building, _currentStronghold, out var voided);
            if (points > 0)
            {
                building.WeeklyConstructionPoints += points;
                _currentStronghold.Journal.Add(new JournalEntry(
                    _currentStronghold.CurrentWeek,
                    _currentStronghold.YearsSinceFoundation,
                    JournalEntryType.Event,
                    $"Magic assist at {building.Name}",
                    $"Spellcasters added {points} construction points to {building.Name} this week."
                ));
            }

            foreach (var name in voided)
            {
                _currentStronghold.Journal.Add(new JournalEntry(
                    _currentStronghold.CurrentWeek,
                    _currentStronghold.YearsSinceFoundation,
                    JournalEntryType.Event,
                    $"Magic assist wasted at {building.Name}",
                    $"{name} was no longer working on {building.Name}, so their magic assist was wasted."
                ));
            }
        }

        private void ProcessProjects()
        {
            if (_currentStronghold?.Buildings == null) return;

            foreach (var building in _currentStronghold.Buildings)
            {
                if (building.CurrentProject == null) continue;
                building.CurrentProject.HasTicked = true;
                building.CurrentProject.TimeRemaining = Math.Max(0, building.CurrentProject.TimeRemaining - 1);
                if (building.CurrentProject.TimeRemaining <= 0)
                {
                    _pendingProjectCompletions.Add((building.Id, building.CurrentProject.Id));
                }
            }
        }

        public List<(Building Building, Project Project)> DrainPendingProjectCompletions()
        {
            var result = new List<(Building, Project)>();
            if (_currentStronghold?.Buildings == null)
            {
                _pendingProjectCompletions.Clear();
                return result;
            }

            foreach (var building in _currentStronghold.Buildings)
            {
                if (building.CurrentProject != null && building.CurrentProject.TimeRemaining <= 0)
                    result.Add((building, building.CurrentProject));
            }

            _pendingProjectCompletions.Clear();
            return result;
        }

        public ProjectCompletionResult FinishProject(string buildingId, int? d20Total, bool skipped, bool notify = true)
        {
            var result = new ProjectCompletionResult();
            var building = _currentStronghold?.Buildings.Find(b => b.Id == buildingId);
            if (building?.CurrentProject == null)
            {
                result.Summary = "No active project.";
                return result;
            }

            var project = building.CurrentProject;
            var buildingData = new LoadBuildingDataCommand().Execute();
            var buildingInfo = buildingData.buildings.Find(b => b.type == building.TypeName);
            int bonus = ProjectResolutionService.ComputeBonus(building, buildingInfo, project, _currentStronghold.NPCs);
            result.Bonus = bonus;
            result.DC = project.DC;

            if (string.Equals(project.Name, "Trade Fair", StringComparison.OrdinalIgnoreCase))
            {
                result.Tier = ProjectResultTier.Success;
                result.YieldGranted = ProjectResolutionService.ComputeTradeFairYield(
                    building, project, _currentStronghold.NPCs, _currentStronghold.Reputation);
                string broughtIn = ProjectResolutionService.FormatCosts(result.YieldGranted);
                result.Summary = project.FairFocusResource is ResourceType focus
                    ? $"The fair focused on {focus} and brought in {broughtIn}."
                    : $"The fair brought in {broughtIn}.";
            }
            else if (string.Equals(project.Name, "Trade Mission", StringComparison.OrdinalIgnoreCase))
            {
                result = ResolveTradeMission(building, project);
            }
            else if (string.Equals(project.Name, "Establish Trade Route", StringComparison.OrdinalIgnoreCase))
            {
                result = ResolveEstablishTradeRoute(building, project, d20Total, skipped, bonus);
            }
            else
            {
                result = ResolveGenericProject(building, project, d20Total, skipped, bonus);
            }

            GrantResources(result.YieldGranted);
            ProjectResolutionService.AwardProjectSkillXp(project, buildingInfo, _currentStronghold.NPCs);

            bool hasCargoOut = project.CargoOut != null && project.CargoOut.Any(c => c.Amount > 0);
            string journalBody =
                $"{project.Name} at {building.Name} finished as {result.Tier}." +
                (string.IsNullOrEmpty(project.Commission) ? "" : $" Commission: {project.Commission}.") +
                $" Workers: {project.AssignedWorkers.Count}." +
                (project.RollMode != ProjectRollMode.None && result.D20Total.HasValue
                    ? $" Roll {result.D20Total}+{result.Bonus} vs DC {result.DC}."
                    : "") +
                (hasCargoOut
                    ? $" Sent: {ProjectResolutionService.FormatCosts(project.CargoOut)}. Returned: {ProjectResolutionService.FormatCosts(result.YieldGranted)}."
                    : $" Yield: {ProjectResolutionService.FormatCosts(result.YieldGranted)}.") +
                (string.IsNullOrEmpty(result.Mishap) ? "" : $" {result.Mishap}") +
                (string.IsNullOrEmpty(result.Summary) ? "" : $" {result.Summary}");

            _currentStronghold.Journal.Add(new JournalEntry(
                _currentStronghold.CurrentWeek,
                _currentStronghold.YearsSinceFoundation,
                JournalEntryType.ProjectComplete,
                $"{project.Name} complete",
                journalBody.Trim()));

            var latestReport = _currentStronghold.CurrentWeeklyReport;
            if (latestReport != null
                && latestReport.Week == _currentStronghold.CurrentWeek
                && latestReport.Year == _currentStronghold.YearsSinceFoundation)
            {
                latestReport.CompletedProjects.Add(new CompletedProject
                {
                    Id = project.Id,
                    Name = $"{project.Name} ({building.Name})",
                    Type = $"Project ({result.Tier})"
                });
                latestReport.UpcomingCompletions.RemoveAll(u => u.Id == project.Id);
                var journalEntry = _currentStronghold.Journal[_currentStronghold.Journal.Count - 1];
                if (!latestReport.TurnJournalEntryIds.Contains(journalEntry.Id))
                    latestReport.TurnJournalEntryIds.Add(journalEntry.Id);
            }

            ClearTradeOccupancy(project);
            building.CurrentProject = null;
            if (notify)
                OnGameStateChanged();
            return result;
        }

        private ProjectCompletionResult ResolveGenericProject(
            Building building, Project project, int? d20Total, bool skipped, int bonus)
        {
            var result = new ProjectCompletionResult { Bonus = bonus, DC = project.DC };
            ProjectResultTier tier = ProjectResultTier.Success;

            if (project.RollMode == ProjectRollMode.Required)
            {
                int roll = d20Total ?? 1;
                result.D20Total = roll;
                tier = ProjectResolutionService.GetResultTier(roll, bonus, project.DC);
            }
            else if (project.RollMode == ProjectRollMode.Optional && !skipped && d20Total.HasValue)
            {
                result.D20Total = d20Total;
                tier = ProjectResolutionService.GetResultTier(d20Total.Value, bonus, project.DC);
            }

            result.Tier = tier;
            result.YieldGranted = ProjectResolutionService.ScaleYield(project.SuccessYield, tier);
            result.Mishap = ProjectResolutionService.DefaultMishap(project.Name, tier);
            result.Summary = project.OutcomeType == ProjectOutcomeType.Table
                ? $"Recorded as {tier} for the table."
                : $"Resolved as {tier}. Returned {ProjectResolutionService.FormatCosts(result.YieldGranted)}.";
            return result;
        }

        private ProjectCompletionResult ResolveTradeMission(Building building, Project project)
        {
            var result = new ProjectCompletionResult { Tier = ProjectResultTier.Success, DC = 0 };
            var route = _currentStronghold.TradeRoutes.Find(r => r.Id == project.TradeRouteId);
            var (fraction, mishap) = TradeService.RollCargoLoss(route, _currentStronghold);
            var expected = project.ExpectedReturn ?? new List<ResourceCost>();
            result.Mishap = mishap;
            if (fraction <= 0)
            {
                result.Tier = ProjectResultTier.Failure;
                result.YieldGranted = new List<ResourceCost>();
                result.Summary =
                    $"The caravan to {route?.Name ?? "the destination"} was lost. " +
                    $"Sent {ProjectResolutionService.FormatCosts(project.CargoOut)} is gone.";
                return result;
            }

            var packed = expected;
            var returnedCargo = new List<ResourceCost>();
            string shortfallNote = string.Empty;
            if (route != null)
            {
                (packed, returnedCargo, shortfallNote) = TradeService.PackReturn(
                    route, _currentStronghold, project, _currentStronghold.NPCs);
                TradeService.ApplyMissionStock(route, project, packed, returnedCargo);
            }

            var comingBack = packed
                .Select(c => new ResourceCost { ResourceType = c.ResourceType, Amount = c.Amount })
                .ToList();
            foreach (var item in returnedCargo)
            {
                var existing = comingBack.Find(c => c.ResourceType == item.ResourceType);
                if (existing != null)
                    existing.Amount += item.Amount;
                else
                    comingBack.Add(new ResourceCost { ResourceType = item.ResourceType, Amount = item.Amount });
            }
            result.YieldGranted = TradeService.ApplyLoss(comingBack, fraction);
            string returned = ProjectResolutionService.FormatCosts(result.YieldGranted);
            string dest = route?.Name ?? "the destination";
            if (fraction < 1m)
            {
                result.Tier = ProjectResultTier.Partial;
                result.Summary = $"The caravan to {dest} returned with {returned} (partial).";
            }
            else
            {
                result.Tier = ProjectResultTier.Success;
                result.Summary = $"The caravan to {dest} returned with {returned}.";
            }
            if (!string.IsNullOrEmpty(shortfallNote))
                result.Summary += " " + shortfallNote;
            return result;
        }

        private ProjectCompletionResult ResolveEstablishTradeRoute(
            Building building, Project project, int? d20Total, bool skipped, int bonus)
        {
            var result = new ProjectCompletionResult { Bonus = bonus, DC = project.DC };
            int roll = d20Total ?? 1;
            result.D20Total = roll;
            var tier = ProjectResolutionService.GetResultTier(roll, bonus, project.DC);
            result.Tier = tier;

            var dest = project.CustomDestination
                ?? TradeDestinationService.GetInstance().GetById(project.TradeDestinationId ?? "");
            if (dest == null)
            {
                result.YieldGranted = CopyCosts(project.InitialCost);
                result.Summary = "The destination is no longer in the catalog. The establishment cost was returned.";
                result.Tier = ProjectResultTier.Failure;
                return result;
            }

            if (TradeService.HasOpenRouteTo(_currentStronghold, dest.Id))
            {
                result.YieldGranted = CopyCosts(project.InitialCost);
                result.Summary = $"A route to {dest.Name} is already open. The establishment cost was returned.";
                return result;
            }

            var route = TradeService.CreateRoute(
                dest, tier, _currentStronghold.CurrentWeek, _currentStronghold.YearsSinceFoundation);
            _currentStronghold.TradeRoutes.Add(route);
            result.CreatedRoute = route;
            result.Mishap = tier switch
            {
                ProjectResultTier.Failure => "First contact went poorly. The route is open, but prices are bad until the relationship settles.",
                ProjectResultTier.Partial => "Terms are uneven. Rates will drift toward the local market over the coming weeks.",
                _ => string.Empty
            };
            result.Summary = tier switch
            {
                ProjectResultTier.Exceptional => $"Opened a route to {dest.Name} on excellent terms ({dest.DistanceWeeks} week trip).",
                ProjectResultTier.Success => $"Opened a route to {dest.Name} ({dest.DistanceWeeks} week trip).",
                ProjectResultTier.Partial => $"Opened a route to {dest.Name} on uneven terms ({dest.DistanceWeeks} week trip).",
                _ => $"Opened a route to {dest.Name} on poor terms ({dest.DistanceWeeks} week trip). Rates will improve as the market settles."
            };
            _currentStronghold.Journal.Add(new JournalEntry(
                _currentStronghold.CurrentWeek,
                _currentStronghold.YearsSinceFoundation,
                JournalEntryType.TradeRouteEstablished,
                $"Trade route to {dest.Name} established",
                $"Founding terms: {tier}. Distance: {route.DistanceWeeks} weeks. Demand: {string.Join(", ", route.CurrentDemand)}."));
            return result;
        }

        private void ClearTradeOccupancy(Project project)
        {
            if (string.IsNullOrEmpty(project.TradeRouteId) || _currentStronghold.TradeRoutes == null) return;
            var route = _currentStronghold.TradeRoutes.Find(r => r.Id == project.TradeRouteId);
            if (route != null && route.ActiveMissionProjectId == project.Id)
                route.ActiveMissionProjectId = null;
        }

        public void CancelBuildingProject(string buildingId)
        {
            var building = _currentStronghold?.Buildings.Find(b => b.Id == buildingId);
            if (building?.CurrentProject == null) return;
            var project = building.CurrentProject;
            if (!project.CanCancelSameTurn) return;

            GrantResources(project.InitialCost);
            ClearTradeOccupancy(project);
            building.CancelProject();
            OnGameStateChanged();
        }

        public void DamageBuilding(string buildingId, int damageAmount)
        {
            var building = _currentStronghold?.Buildings.Find(b => b.Id == buildingId);
            if (building == null) return;

            var cancelled = building.Damage(damageAmount);
            if (cancelled != null)
                ClearTradeOccupancy(cancelled);
            TradeService.ReconcileOccupancy(_currentStronghold);
            OnGameStateChanged();
        }

        private static List<ResourceCost> CopyCosts(List<ResourceCost>? costs)
        {
            if (costs == null) return new List<ResourceCost>();
            return costs
                .Select(c => new ResourceCost { ResourceType = c.ResourceType, Amount = c.Amount })
                .ToList();
        }

        private void GrantResources(List<ResourceCost> grants)
        {
            if (grants == null) return;
            foreach (var grant in grants)
            {
                var resource = _currentStronghold.Resources.Find(r => r.Type == grant.ResourceType);
                if (resource == null) continue;
                resource.Amount += grant.Amount;
                if (grant.ResourceType == ResourceType.Gold)
                    _currentStronghold.Treasury = resource.Amount;
            }
        }

        private void ProcessTradeRoutes()
        {
            if (_currentStronghold == null) return;
            _currentStronghold.TradeRoutes ??= new List<TradeRoute>();
            _currentStronghold.TradeMarketEvents ??= new List<TradeMarketEvent>();

            TradeService.ReconcileOccupancy(_currentStronghold);
            TradeService.DriftOpenRoutes(_currentStronghold);
            TradeService.TickSettlementStocks(_currentStronghold);
            TradeService.TickMarketEvents(_currentStronghold);
            var ev = TradeService.TryCreateMarketEvent(_currentStronghold);
            if (ev != null)
                RecordTradeMarketEvent(ev, notify: false);
        }

        public void RecordTradeMarketEvent(TradeMarketEvent ev, bool notify = true)
        {
            if (ev == null || _currentStronghold == null) return;
            _currentStronghold.TradeMarketEvents ??= new List<TradeMarketEvent>();
            if (!_currentStronghold.TradeMarketEvents.Contains(ev))
                _currentStronghold.TradeMarketEvents.Add(ev);

            var route = _currentStronghold.TradeRoutes?.Find(r => r.Id == ev.RouteId);
            if (route != null)
                TradeService.ApplyEventImmediateEffects(route, ev);

            string description = !string.IsNullOrWhiteSpace(ev.Notes)
                ? ev.Notes.Trim()
                : ev.Kind switch
                {
                    TradeMarketEventKind.DemandSpike =>
                        $"{ev.RouteName} will pay well for {ev.ResourceType} for {ev.WeeksRemaining} weeks.",
                    TradeMarketEventKind.Surplus =>
                        $"{ev.RouteName} has a surplus of {ev.ResourceType} for {ev.WeeksRemaining} weeks.",
                    TradeMarketEventKind.TradeCollapse =>
                        $"{ev.RouteName}'s rates have collapsed to failure-tier terms for {ev.WeeksRemaining} weeks.",
                    TradeMarketEventKind.Drought =>
                        $"{ev.RouteName} is in drought. Food stores and harvests are failing, and they will pay well for food for {ev.WeeksRemaining} weeks.",
                    TradeMarketEventKind.Bandits =>
                        $"Bandits plague the road to {ev.RouteName} for {ev.WeeksRemaining} weeks. Caravans are more likely to lose cargo.",
                    TradeMarketEventKind.GuildFavor =>
                        $"{ev.RouteName}'s merchant guild favors you. Rates are exceptional for {ev.WeeksRemaining} weeks.",
                    TradeMarketEventKind.Quarantine =>
                        $"{ev.RouteName} is under quarantine for {ev.WeeksRemaining} weeks. No new caravans can be sent.",
                    _ => ev.DefaultSummary
                };

            _currentStronghold.Journal.Add(new JournalEntry(
                _currentStronghold.CurrentWeek,
                _currentStronghold.YearsSinceFoundation,
                JournalEntryType.TradeMarketEvent,
                ev.Summary,
                description));

            if (notify)
                OnGameStateChanged();
        }

        public string CloseTradeRoute(string routeId)
        {
            var error = TradeService.CloseRoute(_currentStronghold, routeId);
            if (string.IsNullOrEmpty(error))
                OnGameStateChanged();
            return error;
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

                    _turnCompletions.Add(new CompletedProject
                    {
                        Id = mission.Id,
                        Name = mission.Name,
                        Type = $"Mission ({mission.Status})"
                    });
                    
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
            var shuttered = UpkeepService.ListShutteredBuildings(_currentStronghold);

            // Recalculate rates with operable filter so this tick matches material affordability.
            _suppressMoraleJournal = true;
            UpdateProductionAndConsumptionRates();
            _suppressMoraleJournal = false;

            foreach (var building in shuttered)
            {
                string name = string.IsNullOrWhiteSpace(building.Name) ? building.TypeName : building.Name;
                _currentStronghold.Journal.Add(new JournalEntry(
                    _currentStronghold.CurrentWeek,
                    _currentStronghold.YearsSinceFoundation,
                    JournalEntryType.Event,
                    $"{name} idle",
                    $"{name} cannot operate: {UpkeepService.FormatMaterialGaps(building, _currentStronghold)}. " +
                    "No production or upkeep while shuttered."));
            }

            // Apply the weekly changes to all resources
            foreach (var resource in _currentStronghold.Resources)
            {
                resource.ApplyWeeklyChange();
                
                // Update treasury if it's gold
                if (resource.Type == ResourceType.Gold)
                {
                    _currentStronghold.Treasury = resource.Amount;
                }
            }
        }
        
        // Generate weekly report
        private void GenerateWeeklyReport(
            Dictionary<ResourceType, int> previousResourceAmounts,
            Dictionary<ResourceType, (int Produced, int Consumed, List<ResourceChangeBreakdown> Sources)> turnResourceFlows)
        {
            _currentStronghold.WeeklyReports ??= new List<WeeklyReport>();

            WeeklyReport report = new WeeklyReport(_currentStronghold.CurrentWeek, _currentStronghold.YearsSinceFoundation)
            {
                Season = _currentStronghold.CurrentSeason,
                MoraleBefore = _turnMoraleBefore,
                MoraleAfter = _currentStronghold.CurrentMorale,
                PopulationBefore = _turnPopulationBefore,
                PopulationAfter = _currentStronghold.NPCs.Count,
                HungryAfter = _currentStronghold.NPCs.Count(n => n.HungerState == HungerStatus.Hungry),
                StarvingAfter = _currentStronghold.NPCs.Count(n => n.HungerState == HungerStatus.Starving),
                DepartedNpcNames = _turnDepartures.ToList(),
                CompletedProjects = _turnCompletions.ToList()
            };

            // Resource changes with production, consumption, and source breakdown from this turn
            foreach (var resource in _currentStronghold.Resources)
            {
                int previousAmount = previousResourceAmounts.ContainsKey(resource.Type)
                    ? previousResourceAmounts[resource.Type]
                    : 0;

                turnResourceFlows.TryGetValue(resource.Type, out var flow);

                var change = new ResourceChange
                {
                    ResourceType = resource.Type,
                    PreviousAmount = previousAmount,
                    CurrentAmount = resource.Amount,
                    Produced = flow.Produced,
                    Consumed = flow.Consumed,
                    Breakdown = flow.Sources ?? new List<ResourceChangeBreakdown>()
                };

                report.ResourceChanges.Add(change);

                // Outlook uses current (post-turn) rates
                int projected = Math.Max(0, resource.Amount + resource.WeeklyProduction - resource.WeeklyConsumption);
                report.SavedOutlook.Add(new ResourceOutlook
                {
                    ResourceType = resource.Type,
                    ProjectedAmount = projected,
                    WeeklyProduction = resource.WeeklyProduction,
                    WeeklyConsumption = resource.WeeklyConsumption
                });
            }

            // Gold income/expense summary from this turn's flows
            var goldChange = report.ResourceChanges.Find(c => c.ResourceType == ResourceType.Gold);
            if (goldChange != null)
            {
                report.IncomeExpenseSummary.TotalIncome = goldChange.Produced;
                report.IncomeExpenseSummary.TotalExpenses = goldChange.Consumed;
                foreach (var source in goldChange.Breakdown)
                {
                    report.IncomeExpenseSummary.Breakdown.Add(new IncomeExpenseBreakdown
                    {
                        Category = source.Source.TrimStart('+', '-', ' '),
                        Income = source.Amount > 0 ? source.Amount : 0,
                        Expenses = source.Amount < 0 ? -source.Amount : 0
                    });
                }
            }

            // Upcoming completions (including projects ready to resolve)
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
                else if (building.ConstructionStatus == BuildingStatus.Upgrading)
                {
                    report.UpcomingCompletions.Add(new UpcomingCompletion
                    {
                        Id = building.Id,
                        Name = building.Name,
                        Type = "Building Upgrade",
                        WeeksRemaining = building.ConstructionTimeRemaining
                    });
                }

                if (building.CurrentProject != null)
                {
                    report.UpcomingCompletions.Add(new UpcomingCompletion
                    {
                        Id = building.CurrentProject.Id,
                        Name = $"{building.CurrentProject.Name} ({building.Name})",
                        Type = building.CurrentProject.TimeRemaining <= 0
                            ? "Project (ready to resolve)"
                            : "Project",
                        WeeksRemaining = building.CurrentProject.TimeRemaining
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

            // Journal entries written during this Next Turn
            int start = Math.Max(0, _turnJournalStartIndex);
            for (int i = start; i < _currentStronghold.Journal.Count; i++)
            {
                report.TurnJournalEntryIds.Add(_currentStronghold.Journal[i].Id);
            }

            _currentStronghold.WeeklyReports.Add(report);
            _currentStronghold.CurrentWeeklyReport = report;
        }
        
        // Save game to file
        public void SaveGame(string filePath)
        {
            var command = new SaveGameCommand(this, filePath);
            _commandInvoker.ExecuteCommand(command);
        }
        
        // Internal method called by SaveGameCommand
        internal void ExecuteSaveGame(string filePath)
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
            var command = new LoadGameCommand(this, filePath);
            _commandInvoker.ExecuteCommand(command);
        }
        
        // Internal method called by LoadGameCommand
        internal void ExecuteLoadGame(string filePath)
        {
            try
            {
                string json = File.ReadAllText(filePath);
                _currentStronghold = JsonSerializer.Deserialize<Stronghold>(json);

                // Older saves predate skills that have since been added
                if (_currentStronghold != null)
                {
                    _currentStronghold.TradeRoutes ??= new List<TradeRoute>();
                    _currentStronghold.TradeMarketEvents ??= new List<TradeMarketEvent>();
                    _currentStronghold.EnsureCombatDefaults();
                    TradeService.ReconcileOccupancy(_currentStronghold);
                    foreach (var npc in _currentStronghold.NPCs)
                    {
                        npc.EnsureSkillsInitialized();
                    }
                }
                
                // Notify listeners that the game state has changed
                OnGameStateChanged();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading game: {ex.Message}", "Load Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // Load test stronghold data from JSON file
        public void LoadTestStrongholdData()
        {
            try
            {
                string[] possiblePaths = new[]
                {
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "TestStrongholdData.json"),
                    Path.Combine(Directory.GetCurrentDirectory(), "Data", "TestStrongholdData.json"),
                    Path.Combine(Directory.GetCurrentDirectory(), "..", "Data", "TestStrongholdData.json"),
                    Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "Data", "TestStrongholdData.json")
                };

                string jsonPath = possiblePaths.FirstOrDefault(File.Exists);
                
                if (string.IsNullOrEmpty(jsonPath))
                {
                    // Fallback to regular stronghold creation if test data file not found
                    CreateNewStronghold();
                    return;
                }

                string json = File.ReadAllText(jsonPath);
                
                // Configure JsonSerializer options for case-insensitive deserialization
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };
                
                var testData = JsonSerializer.Deserialize<TestStrongholdData>(json, options);
                
                if (testData == null)
                {
                    CreateNewStronghold();
                    return;
                }

                // Create stronghold with test data
                _currentStronghold = new Stronghold
                {
                    Name = testData.StrongholdName,
                    Location = testData.StrongholdLocation,
                    Reputation = testData.Reputation
                };

                // Clear default resources and add standard starting resources
                _currentStronghold.Resources.Clear();
                _currentStronghold.Resources.Add(new Resource { Type = ResourceType.Gold, Amount = 500 });
                _currentStronghold.Resources.Add(new Resource { Type = ResourceType.Food, Amount = 100 });
                _currentStronghold.Resources.Add(new Resource { Type = ResourceType.Wood, Amount = 50 });
                _currentStronghold.Resources.Add(new Resource { Type = ResourceType.Stone, Amount = 30 });
                _currentStronghold.Resources.Add(new Resource { Type = ResourceType.Iron, Amount = 10 });
                _currentStronghold.Resources.Add(new Resource { Type = ResourceType.Luxury, Amount = 5 });

                // Add buildings from test data
                foreach (var buildingData in testData.Buildings)
                {
                    if (Enum.TryParse<BuildingStatus>(buildingData.ConstructionStatus, out var constructionStatus))
                    {
                        var building = new Building(buildingData.Type)
                        {
                            Name = buildingData.Name,
                            ConstructionStatus = constructionStatus,
                            Level = buildingData.Level > 0 ? buildingData.Level : 1
                        };
                        _currentStronghold.Buildings.Add(building);
                    }
                }

                // Add NPCs from test data
                foreach (var npcData in testData.NPCs)
                {
                    if (Enum.TryParse<NPCType>(npcData.Type, out var npcType))
                    {
                        var npc = new NPC(npcType);
                        if (!string.IsNullOrWhiteSpace(npcData.Name))
                            npc.Name = npcData.Name.Trim();
                        if (!string.IsNullOrWhiteSpace(npcData.Title))
                            npc.Title = npcData.Title.Trim();
                        npc.Hero = npcData.Hero;
                        npc.Spellcaster = npcData.Spellcaster;
                        npc.MagicAssist = npcData.Spellcaster && npcData.MagicAssist;
                        ApplyTestNpcSkills(npc, npcData.Skills);
                        TraitService.AssignRandomTraits(npc);
                        // Generate a procedural bio for test NPCs
                        npc.GenerateBio();
                        _currentStronghold.NPCs.Add(npc);
                    }
                }

                // Apply assignments from test data
                foreach (var assignment in testData.Assignments)
                {
                    {
                        var building = _currentStronghold.Buildings.FirstOrDefault(b => b.TypeName == assignment.BuildingType);
                        if (building != null)
                        {
                            var assignedCount = 0;
                            foreach (var npcTypeName in assignment.NpcTypes)
                            {
                                if (assignedCount >= assignment.MaxWorkers) break;
                                
                                if (Enum.TryParse<NPCType>(npcTypeName, out var npcType))
                                {
                                    var availableNpc = _currentStronghold.NPCs.FirstOrDefault(n => 
                                        n.Type == npcType && n.Assignment.Type == AssignmentType.Unassigned);
                                    
                                    if (availableNpc != null)
                                    {
                                        availableNpc.Assignment = new NPCAssignment
                                        {
                                            Type = AssignmentType.Building,
                                            TargetId = building.Id,
                                            TargetName = building.Name
                                        };
                                        building.AssignedWorkers.Add(availableNpc.Id);
                                        assignedCount++;
                                    }
                                }
                            }
                        }
                    }
                }

                // Always ensure a Keep exists (central building requirement)
                if (!_currentStronghold.Buildings.Any(b => b.TypeName == "Keep"))
                {
                    var keep = new Building("Keep")
                    {
                        Name = "Keep",
                        ConstructionStatus = BuildingStatus.Complete,
                        Level = 1
                    };
                    _currentStronghold.Buildings.Add(keep);
                }
                
                // Ensure the Keep has a Steward assigned
                EnsureKeepHasSteward();

                // Add initial journal entry
                _currentStronghold.Journal.Add(new JournalEntry(
                    _currentStronghold.CurrentWeek,
                    _currentStronghold.YearsSinceFoundation,
                    JournalEntryType.Event,
                    "Stronghold Founded",
                    $"The stronghold {_currentStronghold.Name} has been founded in {_currentStronghold.Location}."
                ));

                _currentStronghold.EnsureCombatDefaults();

                // Notify listeners that the game state has changed
                OnGameStateChanged();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading test stronghold data: {ex.Message}\n\nFalling back to default stronghold creation.", 
                    "Load Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                CreateNewStronghold();
            }
        }

        private static void ApplyTestNpcSkills(NPC npc, Dictionary<string, int> skills)
        {
            if (npc == null || skills == null || skills.Count == 0) return;
            npc.EnsureSkillsInitialized();
            foreach (var pair in skills)
            {
                var skill = npc.Skills.Find(s => s.Name == pair.Key);
                if (skill == null) continue;
                skill.Level = Math.Clamp(pair.Value, 0, 5);
                if (skill.Type == SkillType.Advanced && skill.Level > 0)
                    skill.IsLearned = true;
            }
            npc.UpdateUpkeepCosts();
        }

        /// <summary>
        /// Builds on the standard test stronghold: runs four silent turns so journal and
        /// weekly reports have history, then leaves construction, upgrade, trade, and
        /// project work in progress at week 4.
        /// </summary>
        private void ApplyPlaythroughWeek4Scenario()
        {
            if (_currentStronghold == null) return;

            BoostResource(ResourceType.Gold, 800);
            BoostResource(ResourceType.Food, 250);
            BoostResource(ResourceType.Wood, 120);
            BoostResource(ResourceType.Stone, 80);
            BoostResource(ResourceType.Iron, 40);
            BoostResource(ResourceType.Luxury, 15);
            _currentStronghold.CurrentMorale = 62;
            _currentStronghold.MoraleBaseline = 55;
            _currentStronghold.Reputation = Math.Max(_currentStronghold.Reputation, 15);

            AddPlaythroughJournal(
                JournalEntryType.Event,
                "Council convenes",
                "The steward calls a council. Expansion of the quarry and stronger watchtowers are approved for the coming weeks.");

            // Week 0 actions before the first turn
            StartWatchtowerUpgradeForPlaythrough();
            StartQuarryConstructionForPlaythrough(progressFraction: 0.15);
            StartSmithyCraftProjectForPlaythrough(timeRemaining: 3);
            HostTradeFairForPlaythrough();

            SimulateSilentTurn(); // → week 1
            AddPlaythroughJournal(
                JournalEntryType.Event,
                "Rationing review",
                "With the farm producing well, the steward keeps the garrison on full rations.");
            AddPlaythroughJournal(
                JournalEntryType.NPCAssigned,
                "Laborers to the quarry",
                "Two more laborers join the quarry crew to speed stone cutting.");

            SimulateSilentTurn(); // → week 2
            EstablishMillcreekRouteForPlaythrough();
            AddPlaythroughJournal(
                JournalEntryType.BuildingPlanned,
                "Stables planned",
                "Plans for a new stables are drawn up. Timber and coin are set aside for next season.");

            SimulateSilentTurn(); // → week 3
            var millcreek = _currentStronghold.TradeRoutes?.Find(r =>
                r.Name.Contains("Millcreek", StringComparison.OrdinalIgnoreCase));
            if (millcreek != null)
            {
                var demand = new TradeMarketEvent
                {
                    RouteId = millcreek.Id,
                    RouteName = millcreek.Name,
                    Kind = TradeMarketEventKind.DemandSpike,
                    ResourceType = ResourceType.Luxury,
                    WeeksRemaining = 4,
                    Notes = "Millcreek's market day is buzzing — luxury goods fetch a premium."
                };
                RecordTradeMarketEvent(demand, notify: false);
            }

            AddPlaythroughJournal(
                JournalEntryType.Event,
                "Morale lifts",
                "Word of the new trade opening and steady food stores lifts spirits across the stronghold.");
            _currentStronghold.CurrentMorale = Math.Clamp(_currentStronghold.CurrentMorale + 4, 0, 100);

            SimulateSilentTurn(); // → week 4

            // Leave a busy mid-game board regardless of how fast construction ticked
            SeedOngoingWorkAtWeek4();

            _suppressMoraleJournal = true;
            UpdateProductionAndConsumptionRates();
            _suppressMoraleJournal = false;
            // Avoid notifying during init — constructor already notifies after this returns via Load path.
            // LoadTestStrongholdData already called OnGameStateChanged before we ran; refresh once here.
            OnGameStateChanged();
        }

        private void SimulateSilentTurn()
        {
            BeginTurnJournalCapture();
            // No raids during silent playthrough — keep the board deterministic
            AdvanceWeek();
            AutoResolvePendingProjectsForPlaythrough();
        }

        private void AutoResolvePendingProjectsForPlaythrough()
        {
            foreach (var building in _currentStronghold.Buildings.ToList())
            {
                if (building.CurrentProject != null && building.CurrentProject.TimeRemaining <= 0)
                    FinishProject(building.Id, d20Total: 16, skipped: false, notify: false);
            }
        }

        private void SeedOngoingWorkAtWeek4()
        {
            // Quarry still under construction
            StartQuarryConstructionForPlaythrough(progressFraction: 0.45);

            // Watchtower mid-upgrade
            StartWatchtowerUpgradeForPlaythrough(progressFraction: 0.55);

            // Smithy craft job with time left
            StartSmithyCraftProjectForPlaythrough(timeRemaining: 2);

            // Merchant opening a second route
            StartEstablishTradeRouteForPlaythrough();

            // Stables planned / under construction with a peasant crew
            StartStablesConstructionForPlaythrough(progressFraction: 0.25);

            AddPlaythroughJournal(
                JournalEntryType.Event,
                "Busy week ahead",
                "The quarry, stables, watchtower upgrade, smithy commission, and a new trade embassy are all underway.");
        }

        private void StartWatchtowerUpgradeForPlaythrough(double progressFraction = 0.0)
        {
            var watchtower = _currentStronghold.Buildings.FirstOrDefault(b => b.TypeName == "Watchtower");
            if (watchtower == null) return;

            if (watchtower.ConstructionStatus == BuildingStatus.Upgrading)
            {
                SetConstructionProgress(watchtower, progressFraction);
                return;
            }

            if (watchtower.ConstructionStatus != BuildingStatus.Complete || watchtower.Level >= watchtower.GetMaxLevel())
                return;

            if (watchtower.GetTotalAssignedWorkers() == 0)
            {
                var militia = FindUnassignedNpc(NPCType.Militia) ?? FindAssignedNpc(NPCType.Militia);
                if (militia != null)
                    AssignNpcToBuildingWorkers(watchtower, militia);
            }

            if (watchtower.StartUpgrade(_currentStronghold.Resources))
            {
                SetConstructionProgress(watchtower, progressFraction);
                AddPlaythroughJournal(
                    JournalEntryType.Event,
                    $"Upgrading {watchtower.Name}",
                    $"Work begins to raise {watchtower.Name} to level {watchtower.Level + 1}.");
            }
        }

        private void StartQuarryConstructionForPlaythrough(double progressFraction)
        {
            var quarry = _currentStronghold.Buildings.FirstOrDefault(b => b.TypeName == "Quarry");
            if (quarry == null)
            {
                quarry = new Building("Quarry")
                {
                    Name = "Stone Quarry",
                    ConstructionStatus = BuildingStatus.UnderConstruction,
                    Level = 1
                };
                _currentStronghold.Buildings.Add(quarry);
                AddPlaythroughJournal(
                    JournalEntryType.BuildingPlanned,
                    "Stone Quarry construction planned",
                    "A quarry site is cleared on the eastern ridge. Laborers begin cutting the first blocks.");
            }

            EnsureBuildingUnderConstruction(quarry, progressFraction, NPCType.Laborer, 2);
        }

        private void StartStablesConstructionForPlaythrough(double progressFraction)
        {
            var stables = _currentStronghold.Buildings.FirstOrDefault(b => b.TypeName == "Stables");
            if (stables == null)
            {
                stables = new Building("Stables")
                {
                    Name = "South Stables",
                    ConstructionStatus = BuildingStatus.UnderConstruction,
                    Level = 1
                };
                _currentStronghold.Buildings.Add(stables);
                AddPlaythroughJournal(
                    JournalEntryType.BuildingStart,
                    "South Stables under construction",
                    "Frames go up for a new stables south of the keep.");
            }

            EnsureBuildingUnderConstruction(stables, progressFraction, NPCType.Peasant, 2);
        }

        private void EnsureBuildingUnderConstruction(Building building, double progressFraction, NPCType preferredCrew, int crewCount)
        {
            building.ConstructionStatus = BuildingStatus.UnderConstruction;
            if (building.RequiredConstructionPoints <= 0)
                building.RequiredConstructionPoints = 80;

            // Clear old crew assignments on this building, then assign fresh workers
            foreach (var id in building.DedicatedConstructionCrew.ToList())
            {
                var npc = _currentStronghold.NPCs.Find(n => n.Id == id);
                if (npc != null)
                {
                    npc.Assignment = new NPCAssignment
                    {
                        Type = AssignmentType.Unassigned,
                        TargetId = string.Empty,
                        TargetName = string.Empty
                    };
                }
            }
            building.DedicatedConstructionCrew.Clear();
            building.AssignedWorkers.Clear();

            var crew = new List<string>();
            for (int i = 0; i < crewCount; i++)
            {
                var worker = FindUnassignedNpc(preferredCrew)
                    ?? FindUnassignedNpc(NPCType.Laborer)
                    ?? FindUnassignedNpc(NPCType.Peasant);
                if (worker == null) break;
                worker.Assignment = new NPCAssignment
                {
                    Type = AssignmentType.Building,
                    TargetId = building.Id,
                    TargetName = building.Name + " (Construction Crew)"
                };
                crew.Add(worker.Id);
            }
            building.AssignConstructionCrew(crew);
            SetConstructionProgress(building, progressFraction);
            building.UpdateConstructionProgress(_currentStronghold.NPCs);
        }

        private void SetConstructionProgress(Building building, double progressFraction)
        {
            progressFraction = Math.Clamp(progressFraction, 0, 0.95);
            if (building.RequiredConstructionPoints <= 0)
                building.RequiredConstructionPoints = 50;
            building.CurrentConstructionPoints = (int)(building.RequiredConstructionPoints * progressFraction);
            building.ConstructionProgress = (int)(progressFraction * 100);
            building.UpdateConstructionProgress(_currentStronghold.NPCs);
        }

        private void StartSmithyCraftProjectForPlaythrough(int timeRemaining)
        {
            var smithy = _currentStronghold.Buildings.FirstOrDefault(b => b.TypeName == "Smithy");
            if (smithy == null || smithy.ConstructionStatus != BuildingStatus.Complete)
                return;

            if (smithy.CurrentProject != null)
                smithy.CurrentProject = null;

            var artisan = _currentStronghold.NPCs.FirstOrDefault(n =>
                n.Type == NPCType.Artisan && smithy.AssignedWorkers.Contains(n.Id))
                ?? FindUnassignedNpc(NPCType.Artisan);
            if (artisan == null) return;

            if (!smithy.AssignedWorkers.Contains(artisan.Id))
                AssignNpcToBuildingWorkers(smithy, artisan);

            var project = new Project
            {
                Name = "Craft Equipment",
                Description = "Produce specialized equipment for the stronghold.",
                Duration = 3,
                TimeRemaining = Math.Max(1, timeRemaining),
                MinWorkers = 1,
                OutcomeType = ProjectOutcomeType.Table,
                RollMode = ProjectRollMode.None,
                SetupPrompts = new List<string> { "Workers", "Commission" },
                BonusSkills = new List<string> { "Smithing", "Crafting" },
                Commission = "Militia spears and shields",
                InitialCost = new List<ResourceCost>
                {
                    new ResourceCost { ResourceType = ResourceType.Iron, Amount = 5 },
                    new ResourceCost { ResourceType = ResourceType.Wood, Amount = 3 }
                },
                HasTicked = timeRemaining < 3
            };
            project.AssignedWorkers.Add(artisan.Id);
            project.DC = DifficultyTier.Medium.GetDC();

            // Deduct costs only when freshly starting
            if (timeRemaining >= 3)
            {
                if (!smithy.StartProject(project, _currentStronghold.Resources))
                    return;
                AddPlaythroughJournal(
                    JournalEntryType.Event,
                    "Craft Equipment commissioned",
                    "The Town Smithy takes a commission for militia spears and shields.");
            }
            else
            {
                smithy.CurrentProject = project;
            }
        }

        private void HostTradeFairForPlaythrough()
        {
            var office = _currentStronghold.Buildings.FirstOrDefault(b => b.TypeName == "TradeOffice");
            if (office == null || office.ConstructionStatus != BuildingStatus.Complete)
                return;
            if (office.CurrentProject != null) return;

            var merchant = _currentStronghold.NPCs.FirstOrDefault(n =>
                n.Type == NPCType.Merchant && office.AssignedWorkers.Contains(n.Id))
                ?? FindUnassignedNpc(NPCType.Merchant);
            if (merchant == null) return;

            var project = new Project
            {
                Name = "Trade Fair",
                Description = "Host a week-long fair.",
                Duration = 1,
                TimeRemaining = 1,
                MinWorkers = 1,
                OutcomeType = ProjectOutcomeType.InApp,
                RollMode = ProjectRollMode.None,
                FairFocusResource = ResourceType.Food,
                SetupPrompts = new List<string> { "Workers", "Focus" },
                BonusSkills = new List<string> { "Trade", "Connections" },
                InitialCost = new List<ResourceCost>
                {
                    new ResourceCost { ResourceType = ResourceType.Gold, Amount = 10 },
                    new ResourceCost { ResourceType = ResourceType.Food, Amount = 5 }
                }
            };
            project.AssignedWorkers.Add(merchant.Id);

            if (office.StartProject(project, _currentStronghold.Resources))
            {
                AddPlaythroughJournal(
                    JournalEntryType.Event,
                    "Trade Fair begins",
                    "Banners go up outside the Trade Office. The fair focuses on food stalls this week.");
            }
        }

        private void EstablishMillcreekRouteForPlaythrough()
        {
            _currentStronghold.TradeRoutes ??= new List<TradeRoute>();
            if (_currentStronghold.TradeRoutes.Any(r =>
                r.Name.Contains("Millcreek", StringComparison.OrdinalIgnoreCase)))
                return;

            var dest = TradeDestinationService.GetInstance().GetById("millcreek")
                ?? TradeDestinationService.GetInstance().GetDestinations().FirstOrDefault();
            if (dest == null) return;

            var route = TradeService.CreateRoute(
                dest, ProjectResultTier.Success,
                _currentStronghold.CurrentWeek, _currentStronghold.YearsSinceFoundation);
            _currentStronghold.TradeRoutes.Add(route);

            AddPlaythroughJournal(
                JournalEntryType.TradeRouteEstablished,
                $"Trade route to {route.Name} established",
                $"Merchants return with signed terms. Distance: {route.DistanceWeeks} week(s). Demand: {string.Join(", ", route.CurrentDemand)}.");
        }

        private void StartEstablishTradeRouteForPlaythrough()
        {
            var office = _currentStronghold.Buildings.FirstOrDefault(b => b.TypeName == "TradeOffice");
            if (office == null || office.ConstructionStatus != BuildingStatus.Complete)
                return;

            office.CurrentProject = null;

            var merchant = _currentStronghold.NPCs.FirstOrDefault(n =>
                n.Type == NPCType.Merchant && office.AssignedWorkers.Contains(n.Id))
                ?? FindUnassignedNpc(NPCType.Merchant);
            if (merchant == null) return;

            var destinations = TradeDestinationService.GetInstance().GetDestinations();
            var dest = destinations.FirstOrDefault(d =>
                !string.Equals(d.Id, "millcreek", StringComparison.OrdinalIgnoreCase)
                && !TradeService.HasOpenRouteTo(_currentStronghold, d.Id))
                ?? destinations.FirstOrDefault(d => !TradeService.HasOpenRouteTo(_currentStronghold, d.Id));
            if (dest == null) return;

            var project = new Project
            {
                Name = "Establish Trade Route",
                Description = "Open a new destination for trade missions.",
                Duration = 2,
                TimeRemaining = 2,
                MinWorkers = 1,
                OutcomeType = ProjectOutcomeType.InApp,
                RollMode = ProjectRollMode.Required,
                Difficulty = DifficultyTier.Medium,
                DC = DifficultyTier.Medium.GetDC(),
                TradeDestinationId = dest.Id,
                SetupPrompts = new List<string> { "Workers", "Destination", "Difficulty" },
                BonusSkills = new List<string> { "Trade", "Connections" },
                InitialCost = new List<ResourceCost>
                {
                    new ResourceCost { ResourceType = ResourceType.Gold, Amount = 100 }
                },
                HasTicked = false
            };
            project.AssignedWorkers.Add(merchant.Id);

            if (office.StartProject(project, _currentStronghold.Resources))
            {
                AddPlaythroughJournal(
                    JournalEntryType.Event,
                    $"Embassy to {dest.Name}",
                    $"A merchant sets out to open trade with {dest.Name}. Terms will be settled when they return.");
            }
        }

        private void BoostResource(ResourceType type, int amount)
        {
            var resource = _currentStronghold.Resources.Find(r => r.Type == type);
            if (resource == null) return;
            resource.Amount += amount;
            if (type == ResourceType.Gold)
                _currentStronghold.Treasury = resource.Amount;
        }

        private void AddPlaythroughJournal(JournalEntryType type, string title, string description)
        {
            _currentStronghold.Journal.Add(new JournalEntry(
                _currentStronghold.CurrentWeek,
                _currentStronghold.YearsSinceFoundation,
                type,
                title,
                description));
        }

        private NPC? FindUnassignedNpc(NPCType type)
        {
            return _currentStronghold.NPCs.FirstOrDefault(n =>
                n.Type == type && n.Assignment.Type == AssignmentType.Unassigned && n.IsAlive);
        }

        private NPC? FindAssignedNpc(NPCType type)
        {
            return _currentStronghold.NPCs.FirstOrDefault(n =>
                n.Type == type && n.Assignment.Type == AssignmentType.Building && n.IsAlive);
        }

        private void AssignNpcToBuildingWorkers(Building building, NPC npc)
        {
            // Unassign from previous building if needed
            foreach (var b in _currentStronghold.Buildings)
            {
                b.AssignedWorkers.Remove(npc.Id);
                b.DedicatedConstructionCrew.Remove(npc.Id);
            }

            npc.Assignment = new NPCAssignment
            {
                Type = AssignmentType.Building,
                TargetId = building.Id,
                TargetName = building.Name
            };
            if (!building.AssignedWorkers.Contains(npc.Id))
                building.AssignedWorkers.Add(npc.Id);
        }

        // Data classes for test stronghold JSON structure
        public class TestStrongholdData
        {
            public string StrongholdName { get; set; } = "New Stronghold";
            public string StrongholdLocation { get; set; } = "Unknown";
            public int Reputation { get; set; } = 0;
            public List<TestBuildingData> Buildings { get; set; } = new List<TestBuildingData>();
            public List<TestNPCData> NPCs { get; set; } = new List<TestNPCData>();
            public List<TestAssignmentData> Assignments { get; set; } = new List<TestAssignmentData>();
        }

        public class TestBuildingData
        {
            public string Type { get; set; } = "";
            public string Name { get; set; } = "";
            public string ConstructionStatus { get; set; } = "Complete";
            public int Level { get; set; } = 1;
        }

        public class TestNPCData
        {
            public string Type { get; set; } = "";
            public string Name { get; set; } = "";
            public string Title { get; set; } = "";
            public bool Hero { get; set; }
            public bool Spellcaster { get; set; }
            public bool MagicAssist { get; set; }
            public Dictionary<string, int> Skills { get; set; } = new Dictionary<string, int>();
        }

        public class TestAssignmentData
        {
            public string BuildingType { get; set; } = "";
            public List<string> NpcTypes { get; set; } = new List<string>();
            public int MaxWorkers { get; set; } = 0;
        }
        
        // Add a new building to the stronghold
        public bool AddBuildingAndDeductCosts(Building building, bool ignoreCosts = false)
        {
            var command = new AddBuildingCommand(this, building, ignoreCosts);
            if (command.CanExecute())
            {
                _commandInvoker.ExecuteCommand(command);
                return true;
            }
            return false;
        }
        
        // Internal method called by AddBuildingCommand
        internal void ExecuteAddBuildingAndDeductCosts(Building building, bool ignoreCosts = false)
        {
            if (!ignoreCosts)
            {
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
            string title = string.IsNullOrWhiteSpace(building.Name) || building.Name == building.TypeName ?
                $"{building.TypeName} construction planned" :
                $"{building.Name} ({building.TypeName}) construction planned";
                
            _currentStronghold.Journal.Add(new JournalEntry(
                _currentStronghold.CurrentWeek,
                _currentStronghold.YearsSinceFoundation,
                JournalEntryType.BuildingPlanned,
                title,
                ignoreCosts
                    ? "A new building has been added. Construction costs were waived."
                    : "A new building has been planned for construction. Resources have been allocated."
            ));
            
            OnGameStateChanged();
        }

        // Add a new NPC to the stronghold
        public void AddNPC(NPC npc)
        {
            var command = new AddNPCCommand(this, npc);
            _commandInvoker.ExecuteCommand(command);
        }
        
        // Internal method called by AddNPCCommand
        internal void ExecuteAddNPC(NPC npc)
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
        
        // Ensure Keep always has a Steward assigned
        public void EnsureKeepHasSteward()
        {
            var keep = _currentStronghold.Buildings.FirstOrDefault(b => b.TypeName == "Keep");
            if (keep == null) return;
            
            // Check if Keep already has a Steward assigned
            var hasSteward = _currentStronghold.NPCs.Any(npc => 
                npc.Assignment.Type == AssignmentType.Building && 
                npc.Assignment.TargetId == keep.Id &&
                npc.Title == "Steward");
            
            if (!hasSteward)
            {
                // Find an available NPC to assign as Steward, or create one if none available
                var availableNpc = _currentStronghold.NPCs.FirstOrDefault(npc => 
                    npc.Assignment.Type == AssignmentType.Unassigned);
                
                if (availableNpc == null)
                {
                    // Create a new NPC to serve as Steward
                    availableNpc = new NPC(NPCType.Administrator)
                    {
                        Name = "Steward",
                        Title = "Steward"
                    };
                    TraitService.AssignRandomTraits(availableNpc);
                    availableNpc.GenerateBio();
                    _currentStronghold.NPCs.Add(availableNpc);
                }
                
                // Assign as Steward to the Keep
                availableNpc.Assignment = new NPCAssignment
                {
                    Type = AssignmentType.Building,
                    TargetId = keep.Id,
                    TargetName = keep.Name
                };
                availableNpc.Title = "Steward";
                
                // Ensure the Keep has at least one worker slot assigned
                if (!keep.AssignedWorkers.Contains(availableNpc.Id))
                {
                    keep.AssignedWorkers.Add(availableNpc.Id);
                }
                
                // Add journal entry
                _currentStronghold.Journal.Add(new JournalEntry(
                    _currentStronghold.CurrentWeek,
                    _currentStronghold.YearsSinceFoundation,
                    JournalEntryType.Event,
                    "Steward Assigned",
                    $"{availableNpc.Name} has been assigned as Steward of the Keep."
                ));
            }
        }
        
        // Assign workers to a building
        public void AssignWorkersToBuilding(string buildingId, List<string> npcIds)
        {
            var command = new AssignWorkersCommand(this, buildingId, npcIds);
            _commandInvoker.ExecuteCommand(command);
        }
        
        // Internal method called by AssignWorkersCommand
        internal void ExecuteAssignWorkersToBuilding(string buildingId, List<string> npcIds)
        {
            var building = _currentStronghold.Buildings.Find(b => b.Id == buildingId);
            if (building == null)
            {
                return;
            }
            
            // First, unassign any regular workers currently assigned to this building (but not construction crew)
            // Special case: Don't unassign Stewards from the Keep
            foreach (var npc in _currentStronghold.NPCs)
            {
                if (npc.Assignment.Type == AssignmentType.Building && npc.Assignment.TargetId == buildingId && !building.DedicatedConstructionCrew.Contains(npc.Id))
                {
                    // Don't unassign Stewards from the Keep
                    if (building.TypeName == "Keep" && npc.Title == "Steward")
                    {
                        continue;
                    }
                    
                    npc.Assignment = new NPCAssignment
                    {
                        Type = AssignmentType.Unassigned,
                        TargetId = string.Empty,
                        TargetName = string.Empty
                    };
                }
            }
            
            // Clear the building's assigned workers list, but preserve Stewards for the Keep
            if (building.TypeName == "Keep")
            {
                // Keep only Stewards in the assigned workers list
                var stewardIds = _currentStronghold.NPCs
                    .Where(npc => npc.Assignment.Type == AssignmentType.Building && 
                                 npc.Assignment.TargetId == buildingId && 
                                 npc.Title == "Steward")
                    .Select(npc => npc.Id)
                    .ToList();
                building.AssignedWorkers = stewardIds;
            }
            else
            {
                building.AssignedWorkers.Clear();
            }
            
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
        
        // Assign construction crew to a building
        public void AssignConstructionCrewToBuilding(string buildingId, List<string> npcIds)
        {
            var command = new AssignConstructionCrewCommand(this, buildingId, npcIds);
            _commandInvoker.ExecuteCommand(command);
        }

        /// <summary>
        /// Lay off workers to reduce gold payroll. Skips stewards and NPCs away on missions.
        /// </summary>
        public int UnassignWorkersForPayroll(IEnumerable<string> npcIds, bool notify = true)
        {
            var ids = npcIds?.Distinct().ToList() ?? new List<string>();
            if (ids.Count == 0) return 0;

            var laidOff = new List<string>();
            foreach (var npcId in ids)
            {
                var npc = _currentStronghold.NPCs.Find(n => n.Id == npcId);
                if (npc == null || !npc.IsAlive) continue;
                if (CombatService.IsAway(npc, _currentStronghold)) continue;
                if (string.Equals(npc.Title, "Steward", StringComparison.OrdinalIgnoreCase)
                    && npc.Assignment.Type == AssignmentType.Building)
                {
                    var keep = _currentStronghold.Buildings.Find(b => b.Id == npc.Assignment.TargetId);
                    if (keep != null && keep.TypeName == "Keep")
                        continue;
                }

                foreach (var building in _currentStronghold.Buildings)
                {
                    building.AssignedWorkers ??= new List<string>();
                    building.DedicatedConstructionCrew ??= new List<string>();
                    building.AssignedWorkers.Remove(npcId);
                    building.DedicatedConstructionCrew.Remove(npcId);
                }

                npc.Assignment = new NPCAssignment
                {
                    Type = AssignmentType.Unassigned,
                    TargetId = string.Empty,
                    TargetName = string.Empty
                };
                laidOff.Add(npc.Name);
            }

            if (laidOff.Count > 0)
            {
                _currentStronghold.Journal.Add(new JournalEntry(
                    _currentStronghold.CurrentWeek,
                    _currentStronghold.YearsSinceFoundation,
                    JournalEntryType.Event,
                    "Payroll cuts",
                    $"Unassigned to afford upkeep: {string.Join(", ", laidOff)}."));
            }

            if (notify && laidOff.Count > 0)
                OnGameStateChanged();
            return laidOff.Count;
        }
        
        // Internal method called by AssignConstructionCrewCommand
        internal void ExecuteAssignConstructionCrewToBuilding(string buildingId, List<string> npcIds)
        {
            var building = _currentStronghold.Buildings.Find(b => b.Id == buildingId);
            if (building == null) return;
            
            // Clear existing construction crew assignments from NPCs
            foreach (var npc in _currentStronghold.NPCs)
            {
                if (building.DedicatedConstructionCrew.Contains(npc.Id))
                {
                    npc.Assignment = new NPCAssignment
                    {
                        Type = AssignmentType.Unassigned,
                        TargetId = string.Empty,
                        TargetName = string.Empty
                    };
                }
            }
            
            // Assign new construction crew
            building.AssignConstructionCrew(npcIds);
            
            // Update NPC assignments
            foreach (var npcId in building.DedicatedConstructionCrew)
            {
                var npc = _currentStronghold.NPCs.Find(n => n.Id == npcId);
                if (npc != null)
                {
                    npc.Assignment = new NPCAssignment
                    {
                        Type = AssignmentType.Building,
                        TargetId = buildingId,
                        TargetName = building.Name + " (Construction Crew)"
                    };
                }
            }
            
            // Update construction progress
            building.UpdateConstructionProgress(_currentStronghold.NPCs);
            
            // Add journal entry if crew was assigned
            if (building.DedicatedConstructionCrew.Count > 0)
            {
                _currentStronghold.Journal.Add(new JournalEntry(
                    _currentStronghold.CurrentWeek,
                    _currentStronghold.YearsSinceFoundation,
                    JournalEntryType.Event,
                    $"Construction Crew Assigned to {building.Name}",
                    $"{building.DedicatedConstructionCrew.Count} workers have been assigned to the dedicated construction crew for {building.Name}."
                ));
            }
            
            OnGameStateChanged();
        }
        
        // Clear construction crew from a building (automatically called when construction completes)
        public void ClearConstructionCrewFromBuilding(string buildingId)
        {
            var building = _currentStronghold.Buildings.Find(b => b.Id == buildingId);
            if (building == null) return;
            
            // Capture the construction crew list BEFORE clearing it
            var constructionCrewIds = new List<string>(building.DedicatedConstructionCrew);
            
            // Unassign construction crew NPCs using the captured list
            foreach (var npcId in constructionCrewIds)
            {
                var npc = _currentStronghold.NPCs.Find(n => n.Id == npcId);
                if (npc != null)
                {
                    npc.Assignment = new NPCAssignment
                    {
                        Type = AssignmentType.Unassigned,
                        TargetId = string.Empty,
                        TargetName = string.Empty
                    };
                }
            }
            
            building.ClearConstructionCrew();
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
            string title = string.IsNullOrWhiteSpace(building.Name) || building.Name == building.TypeName ?
                $"{building.TypeName} construction canceled" :
                $"{building.Name} ({building.TypeName}) construction canceled";
                
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
        
        // Update production and consumption calculations without applying changes
        private void UpdateProductionAndConsumptionRates()
        {
            var operable = UpkeepService.GetOperableBuildingIds(_currentStronghold);

            // Update building production and upkeep for all buildings
            foreach (var building in _currentStronghold.Buildings)
            {
                building.ActualUpkeep.Clear();
                building.ActualProduction.Clear();
                
                if (building.IsFunctional())
                {
                    bool staffed = building.AssignedWorkers != null && building.AssignedWorkers.Count > 0;
                    bool canOperate = staffed && operable.Contains(building.Id);

                    if (canOperate)
                    {
                        var assignedNPCs = building.AssignedWorkers
                            .Select(workerId => _currentStronghold.NPCs.Find(n => n.Id == workerId))
                            .Where(npc => npc != null)
                            .ToList();

                        var moraleStatus = GetMoraleStatus();
                        building.UpdateProduction(assignedNPCs, moraleStatus);
                        
                        if (!string.IsNullOrEmpty(building.LastMoraleJournalMessage))
                        {
                            if (!_suppressMoraleJournal)
                            {
                                _currentStronghold.Journal.Add(new JournalEntry(
                                    _currentStronghold.CurrentWeek,
                                    _currentStronghold.YearsSinceFoundation,
                                    JournalEntryType.Event,
                                    "Morale Production Effect",
                                    building.LastMoraleJournalMessage
                                ));
                            }

                            building.LastMoraleJournalMessage = null;
                        }

                        building.ActualUpkeep.AddRange(building.CalculateUpkeep(_currentStronghold.NPCs));
                    }
                    // Staffed but lacking material upkeep: leave production/upkeep empty (shuttered).
                }
                else
                {
                    // Construction / repair / upgrade salaries still apply.
                    building.ActualUpkeep.AddRange(building.CalculateUpkeep(_currentStronghold.NPCs));
                }
            }

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
                else
                {
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

            // Calculate and add NPC food consumption
            CalculateNPCFoodConsumption();
        }

        // Calculate NPC food consumption and add to resource tracking
        private void CalculateNPCFoodConsumption()
        {
            var foodResource = _currentStronghold.Resources.Find(r => r.Type == ResourceType.Food);
            if (foodResource != null)
            {
                int totalFoodConsumption = 0;
                foreach (var npc in _currentStronghold.NPCs)
                {
                    var foodUpkeep = npc.UpkeepCost.Find(c => c.ResourceType == ResourceType.Food);
                    if (foodUpkeep != null)
                    {
                        // Calculate actual food consumption based on rationing level
                        int actualConsumption = GetNPCActualFoodConsumption(npc, foodUpkeep.Amount);
                        totalFoodConsumption += actualConsumption;
                    }
                }
                
                if (totalFoodConsumption > 0)
                {
                    foodResource.WeeklyConsumption += totalFoodConsumption;
                    foodResource.Sources.Add(new ResourceSource
                    {
                        SourceType = ResourceSourceType.Manual,
                        SourceId = "NPCs",
                        SourceName = "Population",
                        Amount = totalFoodConsumption,
                        IsProduction = false
                    });
                }
            }
        }

        // Calculate actual food consumption for an NPC based on their hunger/rationing state
        private int GetNPCActualFoodConsumption(NPC npc, int baseFoodNeed)
        {
            return npc.RationLevel switch
            {
                RationLevel.Full => baseFoodNeed,      // Full rations = full consumption
                RationLevel.Half => (int)Math.Ceiling(baseFoodNeed / 2.0),  // Half rations = half consumption (rounded up)
                RationLevel.None => 0,                     // No rations = no consumption
                _ => baseFoodNeed
            };
        }

        // Process hunger progression based on rationing levels
        private void ProcessHungerProgression()
        {
            // Create a copy of the NPCs list to avoid "Collection was modified" exception
            // when NPCs abandon the stronghold during iteration
            var npcsToProcess = _currentStronghold.NPCs.ToList();
            
            foreach (var npc in npcsToProcess)
            {
                ProcessNPCHungerProgression(npc);
            }
        }

        private void ProcessNPCHungerProgression(NPC npc)
        {
            // Process hunger progression based on current ration level
            switch (npc.RationLevel)
            {
                case RationLevel.Full:
                    // Full rations: NPC becomes well-fed and resets starvation progress
                    if (npc.HungerState != HungerStatus.WellFed)
                    {
                        npc.HungerState = HungerStatus.WellFed;
                        
                        _currentStronghold.Journal.Add(new JournalEntry(
                            _currentStronghold.CurrentWeek,
                            _currentStronghold.YearsSinceFoundation,
                            JournalEntryType.Event,
                            "Food Situation Improved",
                            $"{npc.Name} is no longer hungry and has recovered."
                        ));
                    }
                    
                    // Always reset starvation progress when NPC has full rations
                    npc.StarvationProgress = 0;
                    break;
                    
                case RationLevel.Half:
                    // Half rations: NPC becomes/stays hungry, starvation progress +1
                    if (npc.HungerState == HungerStatus.WellFed)
                    {
                        npc.HungerState = HungerStatus.Hungry;
                        _currentStronghold.Journal.Add(new JournalEntry(
                            _currentStronghold.CurrentWeek,
                            _currentStronghold.YearsSinceFoundation,
                            JournalEntryType.Event,
                            "Hunger Begins",
                            $"{npc.Name} is now hungry due to reduced rations."
                        ));
                    }
                    
                    if (npc.HungerState == HungerStatus.Hungry)
                    {
                        npc.StarvationProgress += 1; // Slow progression on half rations
                    }
                    break;
                    
                case RationLevel.None:
                    // No rations: NPC becomes/stays hungry, starvation progress +2
                    if (npc.HungerState == HungerStatus.WellFed)
                    {
                        npc.HungerState = HungerStatus.Hungry;
                        _currentStronghold.Journal.Add(new JournalEntry(
                            _currentStronghold.CurrentWeek,
                            _currentStronghold.YearsSinceFoundation,
                            JournalEntryType.Event,
                            "Hunger Begins",
                            $"{npc.Name} is now hungry - no rations provided."
                        ));
                    }
                    
                    if (npc.HungerState == HungerStatus.Hungry)
                    {
                        npc.StarvationProgress += 2; // Fast progression on no rations
                        
                        // Check if NPC should become starving (StarvationProgress >= 4 and no rations)
                        if (npc.StarvationProgress >= 4)
                        {
                            npc.HungerState = HungerStatus.Starving;
                            
                            _currentStronghold.Journal.Add(new JournalEntry(
                                _currentStronghold.CurrentWeek,
                                _currentStronghold.YearsSinceFoundation,
                                JournalEntryType.Event,
                                "⚠️ Starvation Crisis",
                                $"{npc.Name} is now starving after starvation progress reached {npc.StarvationProgress}. They may abandon the stronghold!"
                            ));
                        }
                    }
                    break;
            }
            
            // Handle starving NPCs (check for abandonment)
            if (npc.HungerState == HungerStatus.Starving)
            {
                Random random = new Random();
                int baseAbandonmentChance = 10; // 10% base chance per week
                
                // TODO: Factor in morale system when implemented
                // For now, use base chance
                
                if (random.Next(100) < baseAbandonmentChance)
                {
                    // NPC abandons the stronghold
                    AbandonStronghold(npc);
                }
            }
        }

        private void AbandonStronghold(NPC npc)
        {
            foreach (var building in _currentStronghold.Buildings)
            {
                building.AssignedWorkers.Remove(npc.Id);
                building.DedicatedConstructionCrew.Remove(npc.Id);
                if (building.CurrentProject?.AssignedWorkers.Contains(npc.Id) ?? false)
                    building.CurrentProject.AssignedWorkers.Remove(npc.Id);
            }

            string name = npc.Name;
            _currentStronghold.NPCs.Remove(npc);
            _turnDepartures.Add(name);
            OnNPCAbandonment(name);
        }

        /// <summary>
        /// Updates the bankrupt flag from structural insolvency and, while bankrupt,
        /// causes one eligible NPC to abandon (in addition to hunger abandonments).
        /// </summary>
        private void ProcessBankruptcy()
        {
            bool insolvent = UpkeepService.IsStructurallyBankrupt(_currentStronghold);
            bool wasBankrupt = _currentStronghold.IsBankrupt;

            if (insolvent && !wasBankrupt)
            {
                _currentStronghold.IsBankrupt = true;
                int unavoidable = UpkeepService.GetUnavoidableGoldUpkeep(_currentStronghold);
                int available = UpkeepService.GetGoldAvailable(_currentStronghold);
                var entry = new JournalEntry(
                    _currentStronghold.CurrentWeek,
                    _currentStronghold.YearsSinceFoundation,
                    JournalEntryType.Event,
                    "⚠️ Bankruptcy",
                    $"The stronghold can no longer cover unavoidable upkeep " +
                    $"({unavoidable} gold/week) with available gold ({available}: coffers + production). " +
                    $"While bankrupt, one inhabitant abandons each week.");
                entry.Importance = ImportanceLevel.High;
                _currentStronghold.Journal.Add(entry);
            }
            else if (!insolvent && wasBankrupt)
            {
                _currentStronghold.IsBankrupt = false;
                var entry = new JournalEntry(
                    _currentStronghold.CurrentWeek,
                    _currentStronghold.YearsSinceFoundation,
                    JournalEntryType.Event,
                    "Bankruptcy Ended",
                    "Available gold again covers unavoidable Keep upkeep. The bankruptcy is lifted.");
                entry.Importance = ImportanceLevel.High;
                _currentStronghold.Journal.Add(entry);
            }

            if (!_currentStronghold.IsBankrupt)
                return;

            var candidate = PickBankruptcyAbandonmentCandidate();
            if (candidate == null)
                return;

            AbandonStronghold(candidate);
        }

        /// <summary>
        /// Clears bankruptcy immediately if coffers/production now cover unavoidable
        /// upkeep (e.g. after a DM gold injection). Does not enter bankruptcy or abandon.
        /// </summary>
        public bool TryClearBankruptcyFromCoffers()
        {
            if (_currentStronghold == null || !_currentStronghold.IsBankrupt)
                return false;
            if (UpkeepService.IsStructurallyBankrupt(_currentStronghold))
                return false;

            _currentStronghold.IsBankrupt = false;
            var entry = new JournalEntry(
                _currentStronghold.CurrentWeek,
                _currentStronghold.YearsSinceFoundation,
                JournalEntryType.Event,
                "Bankruptcy Ended",
                "Available gold again covers unavoidable Keep upkeep. The bankruptcy is lifted.");
            entry.Importance = ImportanceLevel.High;
            _currentStronghold.Journal.Add(entry);
            OnGameStateChanged();
            return true;
        }

        /// <summary>DM tool: append a freeform journal entry for the current week.</summary>
        public JournalEntry AddCustomJournalEntry(
            string title,
            string description,
            ImportanceLevel importance = ImportanceLevel.Medium)
        {
            if (_currentStronghold == null)
                throw new InvalidOperationException("No stronghold loaded.");

            var entry = new JournalEntry(
                _currentStronghold.CurrentWeek,
                _currentStronghold.YearsSinceFoundation,
                JournalEntryType.Event,
                string.IsNullOrWhiteSpace(title) ? "Note" : title.Trim(),
                description?.Trim() ?? "");
            entry.Importance = importance;
            _currentStronghold.Journal.Add(entry);
            OnGameStateChanged();
            return entry;
        }

        private NPC? PickBankruptcyAbandonmentCandidate()
        {
            var eligible = _currentStronghold.NPCs
                .Where(n => n != null && n.IsAlive)
                .Where(n => !n.Hero)
                .Where(n => !CombatService.IsAway(n, _currentStronghold))
                .Where(n => !IsKeepSteward(n))
                .OrderByDescending(n =>
                    n.Assignment == null || n.Assignment.Type == AssignmentType.Unassigned ? 1 : 0)
                .ThenBy(n => UpkeepService.WorkerSalary(n))
                .ThenBy(n => n.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return eligible.Count == 0 ? null : eligible[0];
        }

        private bool IsKeepSteward(NPC npc)
        {
            if (!string.Equals(npc.Title, "Steward", StringComparison.OrdinalIgnoreCase))
                return false;
            var keep = _currentStronghold.Buildings.Find(b => b.TypeName == "Keep");
            if (keep == null) return false;
            return keep.AssignedWorkers != null && keep.AssignedWorkers.Contains(npc.Id);
        }

        public void KillNpc(NPC npc)
        {
            if (npc == null || _currentStronghold == null) return;
            CombatService.UnassignNpc(_currentStronghold, npc);
            npc.IsAlive = false;
            npc.States.Clear();
            string name = npc.Name;
            _currentStronghold.NPCs.Remove(npc);
            _turnDepartures.Add(name);
            OnNPCDeath(name);
        }

        private void ProcessHealthStates()
        {
            foreach (var npc in _currentStronghold.NPCs.ToList())
            {
                if (!npc.IsAlive || npc.UpdateHealthState())
                    KillNpc(npc);
            }
        }

        public void HandleRaidCancelledProjects(List<Project> cancelled)
        {
            if (cancelled == null || _currentStronghold == null) return;
            foreach (var project in cancelled)
                ClearTradeOccupancy(project);
            TradeService.ReconcileOccupancy(_currentStronghold);
        }

        public void RecordRaid(RaidBattle battle)
        {
            if (_currentStronghold == null || battle == null) return;
            var party = battle.Party;
            string title = party.Surprise
                ? $"Surprise raid — {party.FactionName}"
                : $"Raid — {party.FactionName}";
            var entry = new JournalEntry(
                _currentStronghold.CurrentWeek,
                _currentStronghold.YearsSinceFoundation,
                JournalEntryType.Raid,
                title,
                battle.FullReport);
            entry.Importance = ImportanceLevel.High;
            _currentStronghold.Journal.Add(entry);
            OnRaid();
            OnGameStateChanged();
        }

        public void RecordRaid(RaidingParty party, DefenseSnapshot snapshot)
        {
            if (party == null) return;
            var battle = new RaidBattle { Party = party };
            battle.Log.Add(CombatService.BuildRaidReport(party, snapshot ?? CombatService.Calculate(_currentStronghold)));
            RecordRaid(battle);
        }

        public void OnGameStateChanged()
        {
            // Update production/consumption rates whenever game state changes
            UpdateProductionAndConsumptionRates();
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
        public bool CanAffordBuilding(string buildingTypeName)
        {
            var building = new Building(buildingTypeName);
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

        // Process weekly morale changes
        private void ProcessWeeklyMorale()
        {
            // 1. Update baseline based on current structural conditions
            UpdateMoraleBaseline();
            
            // 2. Calculate drift components
            int baseDrift = CalculateBaseDrift();
            int eventDrift = GetEventModifiers();
            int conditionDrift = GetConditionModifiers();
            
            // 3. Total drift = sum of all modifiers
            int totalDrift = baseDrift + eventDrift + conditionDrift;
            
            // 4. Apply drift to current morale
            int previousMorale = _currentStronghold.CurrentMorale;
            _currentStronghold.CurrentMorale += totalDrift;
            _currentStronghold.CurrentMorale = Math.Clamp(_currentStronghold.CurrentMorale, 0, 100);
            
            // 5. Process temporary effects (reduce duration)
            ProcessTemporaryMoraleEffects();
            
            // 6. Log morale change if significant
            if (Math.Abs(totalDrift) >= 5)
            {
                string changeDirection = totalDrift > 0 ? "improved" : "declined";
                _currentStronghold.Journal.Add(new JournalEntry(
                    _currentStronghold.CurrentWeek,
                    _currentStronghold.YearsSinceFoundation,
                    JournalEntryType.Event,
                    "Morale Change",
                    $"Stronghold morale has {changeDirection} to {_currentStronghold.CurrentMorale}."
                ));
            }
        }

        // Update morale baseline based on structural conditions
        private void UpdateMoraleBaseline()
        {
            int newBaseline = 50; // Default neutral
            
            // Starving NPCs temporarily lower baseline
            int starvingCount = _currentStronghold.NPCs.Count(n => n.HungerState == HungerStatus.Starving);
            newBaseline -= starvingCount * 2;
            
            // Morale buildings permanently raise baseline
            var moraleBuildings = _currentStronghold.Buildings.Where(b => b.MoraleBonus > 0);
            newBaseline += moraleBuildings.Sum(b => b.MoraleBonus);
            
            // Apply temporary effects
            foreach (var effect in _currentStronghold.TemporaryMoraleEffects)
            {
                newBaseline += effect.MoraleBonus;
            }
            
            _currentStronghold.MoraleBaseline = Math.Clamp(newBaseline, 0, 100);
        }

        // Calculate base drift toward baseline
        private int CalculateBaseDrift()
        {
            if (_currentStronghold.CurrentMorale > _currentStronghold.MoraleBaseline)
                return -Math.Min(3, _currentStronghold.CurrentMorale - _currentStronghold.MoraleBaseline); // Drift down
            else if (_currentStronghold.CurrentMorale < _currentStronghold.MoraleBaseline)
                return Math.Min(3, _currentStronghold.MoraleBaseline - _currentStronghold.CurrentMorale); // Drift up
            else
                return 0; // At baseline
        }

        // Get event-based morale modifiers
        private int GetEventModifiers()
        {
            int drift = 0;
            
            // TODO: Add mission results, NPC deaths, abandonments, feast events
            // This will be implemented when we integrate with the event system
            
            return drift;
        }

        // Get condition-based morale modifiers
        private int GetConditionModifiers()
        {
            int drift = 0;
            
            // Starving NPCs reduce morale
            int starvingCount = _currentStronghold.NPCs.Count(n => n.HungerState == HungerStatus.Starving);
            drift -= starvingCount * 1;
            
            // TODO: Add active morale projects, building conditions, etc.
            
            return drift;
        }

        // Process temporary morale effects (reduce duration)
        private void ProcessTemporaryMoraleEffects()
        {
            var effectsToRemove = new List<TemporaryMoraleEffect>();
            
            foreach (var effect in _currentStronghold.TemporaryMoraleEffects)
            {
                effect.DurationWeeks--;
                if (effect.DurationWeeks <= 0)
                {
                    effectsToRemove.Add(effect);
                }
            }
            
            foreach (var effect in effectsToRemove)
            {
                _currentStronghold.TemporaryMoraleEffects.Remove(effect);
            }
        }

        // Get current morale status
        public MoraleStatus GetMoraleStatus()
        {
            return _currentStronghold.CurrentMorale switch
            {
                >= 81 => MoraleStatus.Excellent,
                >= 61 => MoraleStatus.Good,
                >= 41 => MoraleStatus.Fair,
                >= 21 => MoraleStatus.Poor,
                _ => MoraleStatus.Critical
            };
        }

        // Add temporary morale effect
        public void AddTemporaryMoraleEffect(string name, int bonus, int durationWeeks, string description)
        {
            var effect = new TemporaryMoraleEffect
            {
                Name = name,
                MoraleBonus = bonus,
                DurationWeeks = durationWeeks,
                Description = description
            };
            
            _currentStronghold.TemporaryMoraleEffects.Add(effect);
        }

        // Event-based morale changes
        public void OnSuccessfulMission(string missionName)
        {
            ChangeMorale(10);
            _currentStronghold.Journal.Add(new JournalEntry(
                _currentStronghold.CurrentWeek,
                _currentStronghold.YearsSinceFoundation,
                JournalEntryType.Event,
                "Mission Success",
                $"Successful completion of {missionName} has lifted stronghold morale."
            ));
        }

        public void OnFailedMission(string missionName)
        {
            ChangeMorale(-5);
            _currentStronghold.Journal.Add(new JournalEntry(
                _currentStronghold.CurrentWeek,
                _currentStronghold.YearsSinceFoundation,
                JournalEntryType.Event,
                "Mission Failure",
                $"The failure of {missionName} has lowered stronghold morale."
            ));
        }

        public void OnRaid()
        {
            ChangeMorale(-5);
        }

        public void OnNPCDeath(string npcName)
        {
            ChangeMorale(-5);
            _currentStronghold.Journal.Add(new JournalEntry(
                _currentStronghold.CurrentWeek,
                _currentStronghold.YearsSinceFoundation,
                JournalEntryType.Event,
                "NPC Death",
                $"The death of {npcName} has lowered the stronghold's morale."
            ));
        }

        public void OnNPCAbandonment(string npcName)
        {
            ChangeMorale(-4);
            _currentStronghold.Journal.Add(new JournalEntry(
                _currentStronghold.CurrentWeek,
                _currentStronghold.YearsSinceFoundation,
                JournalEntryType.Event,
                "NPC Abandonment",
                $"{npcName} has abandoned the stronghold, lowering morale."
            ));
        }

        public void OnFeastEvent()
        {
            ChangeMorale(10);
            _currentStronghold.Journal.Add(new JournalEntry(
                _currentStronghold.CurrentWeek,
                _currentStronghold.YearsSinceFoundation,
                JournalEntryType.Event,
                "Feast Event",
                "A feast has boosted stronghold morale."
            ));
        }

        private void ChangeMorale(int delta)
        {
            _currentStronghold.CurrentMorale = Math.Clamp(_currentStronghold.CurrentMorale + delta, 0, 100);
        }
    }
} 