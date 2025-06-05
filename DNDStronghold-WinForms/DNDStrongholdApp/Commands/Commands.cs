using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Forms;
using DNDStrongholdApp.Models;
using DNDStrongholdApp.Services;

namespace DNDStrongholdApp.Commands
{
    // Base command interface
    public interface ICommand
    {
        void Execute();
        void Undo();
        bool CanExecute();
    }

    // Base command with result
    public interface ICommand<TResult>
    {
        TResult Execute();
        bool CanExecute();
    }

    // Command for loading JSON data
    public class LoadBuildingDataCommand : ICommand<BuildingData>
    {
        private readonly string[] _possiblePaths = new[]
        {
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "BuildingData.json"),
            Path.Combine(Directory.GetCurrentDirectory(), "Data", "BuildingData.json"),
            Path.Combine(Directory.GetCurrentDirectory(), "..", "Data", "BuildingData.json"),
            Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "Data", "BuildingData.json")
        };

        public BuildingData Execute()
        {
            string jsonPath = _possiblePaths.FirstOrDefault(File.Exists);
            if (string.IsNullOrEmpty(jsonPath))
                return new BuildingData();

            string json = File.ReadAllText(jsonPath);
            return JsonSerializer.Deserialize<BuildingData>(json) ?? new BuildingData();
        }

        public bool CanExecute() => true;
    }

    // Command for loading test stronghold data
    public class LoadTestStrongholdDataCommand : ICommand<GameStateService.TestStrongholdData>
    {
        public GameStateService.TestStrongholdData Execute()
        {
            string jsonPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "TestStrongholdData.json");
            if (File.Exists(jsonPath))
            {
                string json = File.ReadAllText(jsonPath);
                return JsonSerializer.Deserialize<GameStateService.TestStrongholdData>(json) ?? new GameStateService.TestStrongholdData();
            }
            return new GameStateService.TestStrongholdData();
        }

        public bool CanExecute() => true;
    }

    // Command for worker assignment operations
    public class AssignWorkersCommand : ICommand
    {
        private readonly GameStateService _gameStateService;
        private readonly string _buildingId;
        private readonly List<string> _workerIds;
        private List<string> _previousWorkerIds;

        public AssignWorkersCommand(GameStateService gameStateService, string buildingId, List<string> workerIds)
        {
            _gameStateService = gameStateService;
            _buildingId = buildingId;
            _workerIds = workerIds;
        }

        public void Execute()
        {
            var building = _gameStateService.GetCurrentStronghold().Buildings.Find(b => b.Id == _buildingId);
            if (building != null)
            {
                _previousWorkerIds = new List<string>(building.AssignedWorkers);
                _gameStateService.ExecuteAssignWorkersToBuilding(_buildingId, _workerIds);
            }
        }

        public void Undo()
        {
            if (_previousWorkerIds != null)
            {
                _gameStateService.ExecuteAssignWorkersToBuilding(_buildingId, _previousWorkerIds);
            }
        }

        public bool CanExecute()
        {
            var building = _gameStateService.GetCurrentStronghold().Buildings.Find(b => b.Id == _buildingId);
            return building != null && _workerIds.Count <= building.WorkerSlots;
        }
    }

    // Command for construction crew assignment
    public class AssignConstructionCrewCommand : ICommand
    {
        private readonly GameStateService _gameStateService;
        private readonly string _buildingId;
        private readonly List<string> _crewIds;
        private List<string> _previousCrewIds;

        public AssignConstructionCrewCommand(GameStateService gameStateService, string buildingId, List<string> crewIds)
        {
            _gameStateService = gameStateService;
            _buildingId = buildingId;
            _crewIds = crewIds;
        }

        public void Execute()
        {
            var building = _gameStateService.GetCurrentStronghold().Buildings.Find(b => b.Id == _buildingId);
            if (building != null)
            {
                _previousCrewIds = new List<string>(building.DedicatedConstructionCrew);
                _gameStateService.ExecuteAssignConstructionCrewToBuilding(_buildingId, _crewIds);
            }
        }

        public void Undo()
        {
            if (_previousCrewIds != null)
            {
                _gameStateService.ExecuteAssignConstructionCrewToBuilding(_buildingId, _previousCrewIds);
            }
        }

        public bool CanExecute()
        {
            return _crewIds.Count <= 3; // Max 3 construction crew members
        }
    }

    // Command for resource operations
    public class ModifyResourceCommand : ICommand
    {
        private readonly GameStateService _gameStateService;
        private readonly ResourceType _resourceType;
        private readonly int _amount;
        private int _previousAmount;

        public ModifyResourceCommand(GameStateService gameStateService, ResourceType resourceType, int amount)
        {
            _gameStateService = gameStateService;
            _resourceType = resourceType;
            _amount = amount;
        }

        public void Execute()
        {
            var stronghold = _gameStateService.GetCurrentStronghold();
            var resource = stronghold.Resources.Find(r => r.Type == _resourceType);
            if (resource != null)
            {
                _previousAmount = resource.Amount;
                resource.Amount += _amount;
                
                // Update treasury if it's gold
                if (_resourceType == ResourceType.Gold)
                {
                    stronghold.Treasury = resource.Amount;
                }
            }
        }

        public void Undo()
        {
            var stronghold = _gameStateService.GetCurrentStronghold();
            var resource = stronghold.Resources.Find(r => r.Type == _resourceType);
            if (resource != null)
            {
                resource.Amount = _previousAmount;
                
                // Update treasury if it's gold
                if (_resourceType == ResourceType.Gold)
                {
                    stronghold.Treasury = resource.Amount;
                }
            }
        }

        public bool CanExecute()
        {
            var stronghold = _gameStateService.GetCurrentStronghold();
            var resource = stronghold.Resources.Find(r => r.Type == _resourceType);
            return resource != null && (resource.Amount + _amount >= 0);
        }
    }

    // Command for checking resource costs
    public class CheckResourceCostsCommand : ICommand<bool>
    {
        private readonly GameStateService _gameStateService;
        private readonly List<ResourceCost> _costs;

        public CheckResourceCostsCommand(GameStateService gameStateService, List<ResourceCost> costs)
        {
            _gameStateService = gameStateService;
            _costs = costs;
        }

        public bool Execute()
        {
            var stronghold = _gameStateService.GetCurrentStronghold();
            foreach (var cost in _costs)
            {
                var resource = stronghold.Resources.Find(r => r.Type == cost.ResourceType);
                if (resource == null || resource.Amount < cost.Amount)
                    return false;
            }
            return true;
        }

        public bool CanExecute() => true;
    }

    // Command for deducting resource costs
    public class DeductResourceCostsCommand : ICommand
    {
        private readonly GameStateService _gameStateService;
        private readonly List<ResourceCost> _costs;
        private readonly Dictionary<ResourceType, int> _previousAmounts = new();

        public DeductResourceCostsCommand(GameStateService gameStateService, List<ResourceCost> costs)
        {
            _gameStateService = gameStateService;
            _costs = costs;
        }

        public void Execute()
        {
            var stronghold = _gameStateService.GetCurrentStronghold();
            foreach (var cost in _costs)
            {
                var resource = stronghold.Resources.Find(r => r.Type == cost.ResourceType);
                if (resource != null)
                {
                    _previousAmounts[cost.ResourceType] = resource.Amount;
                    resource.Amount -= cost.Amount;
                    
                    // Update treasury if it's gold
                    if (cost.ResourceType == ResourceType.Gold)
                    {
                        stronghold.Treasury = resource.Amount;
                    }
                }
            }
        }

        public void Undo()
        {
            var stronghold = _gameStateService.GetCurrentStronghold();
            foreach (var previousAmount in _previousAmounts)
            {
                var resource = stronghold.Resources.Find(r => r.Type == previousAmount.Key);
                if (resource != null)
                {
                    resource.Amount = previousAmount.Value;
                    
                    // Update treasury if it's gold
                    if (previousAmount.Key == ResourceType.Gold)
                    {
                        stronghold.Treasury = resource.Amount;
                    }
                }
            }
        }

        public bool CanExecute()
        {
            return new CheckResourceCostsCommand(_gameStateService, _costs).Execute();
        }
    }

    // Command for adding buildings
    public class AddBuildingCommand : ICommand
    {
        private readonly GameStateService _gameStateService;
        private readonly Building _building;
        private string _buildingId;

        public AddBuildingCommand(GameStateService gameStateService, Building building)
        {
            _gameStateService = gameStateService;
            _building = building;
        }

        public void Execute()
        {
            _buildingId = _building.Id;
            _gameStateService.ExecuteAddBuildingAndDeductCosts(_building);
        }

        public void Undo()
        {
            if (!string.IsNullOrEmpty(_buildingId))
            {
                var stronghold = _gameStateService.GetCurrentStronghold();
                var building = stronghold.Buildings.Find(b => b.Id == _buildingId);
                if (building != null)
                {
                    stronghold.Buildings.Remove(building);
                    // Restore resources
                    foreach (var cost in building.ConstructionCost)
                    {
                        var resource = stronghold.Resources.Find(r => r.Type == cost.ResourceType);
                        if (resource != null)
                        {
                            resource.Amount += cost.Amount;
                            if (cost.ResourceType == ResourceType.Gold)
                            {
                                stronghold.Treasury = resource.Amount;
                            }
                        }
                    }
                }
            }
        }

        public bool CanExecute()
        {
            return new CheckResourceCostsCommand(_gameStateService, _building.ConstructionCost).Execute();
        }
    }

    // Command for adding NPCs
    public class AddNPCCommand : ICommand
    {
        private readonly GameStateService _gameStateService;
        private readonly NPC _npc;
        private string _npcId;

        public AddNPCCommand(GameStateService gameStateService, NPC npc)
        {
            _gameStateService = gameStateService;
            _npc = npc;
        }

        public void Execute()
        {
            _npcId = _npc.Id;
            _gameStateService.ExecuteAddNPC(_npc);
        }

        public void Undo()
        {
            if (!string.IsNullOrEmpty(_npcId))
            {
                var stronghold = _gameStateService.GetCurrentStronghold();
                var npc = stronghold.NPCs.Find(n => n.Id == _npcId);
                if (npc != null)
                {
                    stronghold.NPCs.Remove(npc);
                }
            }
        }

        public bool CanExecute() => true;
    }

    // Command for ListView operations
    public class PopulateListViewCommand : ICommand
    {
        private readonly ListView _listView;
        private readonly IEnumerable<object> _items;
        private readonly Func<object, ListViewItem> _itemConverter;

        public PopulateListViewCommand(ListView listView, IEnumerable<object> items, Func<object, ListViewItem> itemConverter)
        {
            _listView = listView;
            _items = items;
            _itemConverter = itemConverter;
        }

        public void Execute()
        {
            _listView.BeginUpdate();
            _listView.Items.Clear();
            
            foreach (var item in _items)
            {
                var listViewItem = _itemConverter(item);
                _listView.Items.Add(listViewItem);
            }
            
            _listView.EndUpdate();
        }

        public void Undo()
        {
            _listView.Items.Clear();
        }

        public bool CanExecute() => _listView != null && _items != null && _itemConverter != null;
    }

    // Command for sorting ListViews
    public class SortListViewCommand : ICommand
    {
        private readonly ListView _listView;
        private readonly int _column;
        private readonly SortOrder _sortOrder;
        private readonly System.Collections.IComparer _comparer;

        public SortListViewCommand(ListView listView, int column, SortOrder sortOrder, System.Collections.IComparer comparer)
        {
            _listView = listView;
            _column = column;
            _sortOrder = sortOrder;
            _comparer = comparer;
        }

        public void Execute()
        {
            _listView.ListViewItemSorter = _comparer;
            _listView.Sort();
        }

        public void Undo()
        {
            _listView.ListViewItemSorter = null;
        }

        public bool CanExecute() => _listView != null && _comparer != null;
    }

    // Command for filtering NPCs
    public class FilterNPCsCommand : ICommand<List<NPC>>
    {
        private readonly List<NPC> _allNPCs;
        private readonly Func<NPC, bool> _filterPredicate;

        public FilterNPCsCommand(List<NPC> allNPCs, Func<NPC, bool> filterPredicate)
        {
            _allNPCs = allNPCs;
            _filterPredicate = filterPredicate;
        }

        public List<NPC> Execute()
        {
            return _allNPCs.Where(_filterPredicate).ToList();
        }

        public bool CanExecute() => _allNPCs != null && _filterPredicate != null;
    }

    // Command for sorting NPCs
    public class SortNPCsCommand : ICommand<List<NPC>>
    {
        private readonly List<NPC> _npcs;
        private readonly Comparison<NPC> _comparison;

        public SortNPCsCommand(List<NPC> npcs, Comparison<NPC> comparison)
        {
            _npcs = npcs;
            _comparison = comparison;
        }

        public List<NPC> Execute()
        {
            var sorted = new List<NPC>(_npcs);
            sorted.Sort(_comparison);
            return sorted;
        }

        public bool CanExecute() => _npcs != null && _comparison != null;
    }

    // Command for getting available projects
    public class GetAvailableProjectsCommand : ICommand<List<Project>>
    {
        private readonly Building _building;
        private readonly LoadBuildingDataCommand _loadDataCommand;

        public GetAvailableProjectsCommand(Building building)
        {
            _building = building;
            _loadDataCommand = new LoadBuildingDataCommand();
        }

        public List<Project> Execute()
        {
            var availableProjects = new List<Project>();
            
            try
            {
                var buildingData = _loadDataCommand.Execute();
                var buildingInfo = buildingData.buildings.Find(b => b.type == _building.Type.ToString());
                
                if (buildingInfo != null)
                {
                    var availableProjectInfos = buildingInfo.availableProjects
                        .Where(p => p.minLevel <= _building.Level)
                        .ToList();

                    foreach (var projectInfo in availableProjectInfos)
                    {
                        var project = new Project
                        {
                            Name = projectInfo.projectName,
                            Description = GetProjectDescription(projectInfo.projectName),
                            Duration = GetProjectDuration(projectInfo.projectName),
                            TimeRemaining = GetProjectDuration(projectInfo.projectName),
                            InitialCost = GetProjectCosts(projectInfo.projectName),
                            AssignedWorkers = new List<string>()
                        };
                        availableProjects.Add(project);
                    }
                }
            }
            catch (Exception)
            {
                // Return empty list on error
            }
            
            return availableProjects;
        }

        public bool CanExecute() => _building != null;

        private string GetProjectDescription(string projectName)
        {
            return projectName switch
            {
                "Patrol" => "Send workers to patrol the area around the stronghold, providing security and gathering information about nearby threats.",
                "Reconnaissance" => "Conduct detailed scouting missions to gather intelligence about distant locations and potential opportunities.",
                "Craft Equipment" => "Produce specialized equipment that can be used by the stronghold or traded for resources.",
                "Craft Alchemical Item" => "Create potions, elixirs, and other alchemical items for various purposes.",
                _ => "A special project that provides unique benefits to the stronghold."
            };
        }

        private int GetProjectDuration(string projectName)
        {
            return projectName switch
            {
                "Patrol" => 2,
                "Reconnaissance" => 4,
                "Craft Equipment" => 3,
                "Craft Alchemical Item" => 2,
                _ => 3
            };
        }

        private List<ResourceCost> GetProjectCosts(string projectName)
        {
            return projectName switch
            {
                "Patrol" => new List<ResourceCost> { new ResourceCost { ResourceType = ResourceType.Food, Amount = 5 } },
                "Reconnaissance" => new List<ResourceCost> { new ResourceCost { ResourceType = ResourceType.Food, Amount = 10 }, new ResourceCost { ResourceType = ResourceType.Gold, Amount = 20 } },
                "Craft Equipment" => new List<ResourceCost> { new ResourceCost { ResourceType = ResourceType.Iron, Amount = 5 }, new ResourceCost { ResourceType = ResourceType.Wood, Amount = 3 } },
                "Craft Alchemical Item" => new List<ResourceCost> { new ResourceCost { ResourceType = ResourceType.Gold, Amount = 30 } },
                _ => new List<ResourceCost>()
            };
        }
    }

    // Command for file operations
    public class SaveGameCommand : ICommand
    {
        private readonly GameStateService _gameStateService;
        private readonly string _filePath;

        public SaveGameCommand(GameStateService gameStateService, string filePath)
        {
            _gameStateService = gameStateService;
            _filePath = filePath;
        }

        public void Execute()
        {
            _gameStateService.ExecuteSaveGame(_filePath);
        }

        public void Undo()
        {
            // Cannot undo save operation
        }

        public bool CanExecute() => !string.IsNullOrEmpty(_filePath);
    }

    // Command for load operations
    public class LoadGameCommand : ICommand
    {
        private readonly GameStateService _gameStateService;
        private readonly string _filePath;

        public LoadGameCommand(GameStateService gameStateService, string filePath)
        {
            _gameStateService = gameStateService;
            _filePath = filePath;
        }

        public void Execute()
        {
            _gameStateService.ExecuteLoadGame(_filePath);
        }

        public void Undo()
        {
            // Cannot undo load operation
        }

        public bool CanExecute() => File.Exists(_filePath);
    }

    // Command for advancing time
    public class AdvanceWeekCommand : ICommand
    {
        private readonly GameStateService _gameStateService;

        public AdvanceWeekCommand(GameStateService gameStateService)
        {
            _gameStateService = gameStateService;
        }

        public void Execute()
        {
            _gameStateService.ExecuteAdvanceWeek();
        }

        public void Undo()
        {
            // Cannot undo week advancement
        }

        public bool CanExecute() => true;
    }

    // Command invoker
    public class CommandInvoker
    {
        private readonly Stack<ICommand> _commandHistory = new();
        private readonly Stack<ICommand> _undoHistory = new();

        public void ExecuteCommand(ICommand command)
        {
            if (command.CanExecute())
            {
                command.Execute();
                _commandHistory.Push(command);
                _undoHistory.Clear(); // Clear redo history when new command is executed
            }
        }

        public TResult ExecuteCommand<TResult>(ICommand<TResult> command)
        {
            if (command.CanExecute())
            {
                return command.Execute();
            }
            return default(TResult);
        }

        public void Undo()
        {
            if (_commandHistory.Count > 0)
            {
                var command = _commandHistory.Pop();
                command.Undo();
                _undoHistory.Push(command);
            }
        }

        public void Redo()
        {
            if (_undoHistory.Count > 0)
            {
                var command = _undoHistory.Pop();
                if (command.CanExecute())
                {
                    command.Execute();
                    _commandHistory.Push(command);
                }
            }
        }

        public bool CanUndo => _commandHistory.Count > 0;
        public bool CanRedo => _undoHistory.Count > 0;
    }
} 