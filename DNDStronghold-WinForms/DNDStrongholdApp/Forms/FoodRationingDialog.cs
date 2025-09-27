using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using DNDStrongholdApp.Models;
using DNDStrongholdApp.Services;

namespace DNDStrongholdApp.Forms
{
    public partial class FoodRationingDialog : Form
    {
        private Stronghold _stronghold;
        private GameStateService _gameStateService;
        private bool _isEmergencyMode;
        private Dictionary<string, RationLevel> _npcRationingChanges = new Dictionary<string, RationLevel>();
        
        // UI Controls
        private ListView _npcListView;
        private Label _currentConsumptionLabel;
        private Label _projectedConsumptionLabel;
        private Label _shortageLabel;
        private ComboBox _bulkRationCombo;
        private Button _applyToSelectedButton;
        private Button _emergencyRationingButton;
        private Button _applyButton;
        private Button _cancelButton;
        private Button _resetButton;
        
        // Sorting state
        private int _lastSortedColumn = -1;
        private SortOrder _sortOrder = SortOrder.None;

        public FoodRationingDialog(Stronghold stronghold, GameStateService gameService, bool emergencyMode = false)
        {
            _stronghold = stronghold;
            _gameStateService = gameService;
            _isEmergencyMode = emergencyMode;
            
            InitializeComponent();
            InitializeDialog();
        }

        private void InitializeDialog()
        {
            // Set dialog title based on mode
            this.Text = _isEmergencyMode ? "⚠️ Emergency Food Rationing" : "Manage Food Rationing";
            
            // Set dialog size and properties
            this.Size = new Size(950, 700);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.MinimumSize = new Size(900, 600);
            
            CreateDialogLayout();
            PopulateNPCList();
            UpdateFoodStatusDisplay();
        }

        private void CreateDialogLayout()
        {
            // Main layout container
            var mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 4,
                ColumnCount = 1,
                Padding = new Padding(10)
            };
            
            // Set row styles: Summary, NPC List, Controls, Buttons
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 100)); // Summary panel
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));  // NPC list (grows)
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));  // Bulk controls
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));  // Action buttons
            
            // 1. Summary Panel
            var summaryPanel = CreateSummaryPanel();
            mainLayout.Controls.Add(summaryPanel, 0, 0);
            
            // 2. NPC List
            var npcListPanel = CreateNPCListPanel();
            mainLayout.Controls.Add(npcListPanel, 0, 1);
            
            // 3. Bulk Controls
            var bulkControlsPanel = CreateBulkControlsPanel();
            mainLayout.Controls.Add(bulkControlsPanel, 0, 2);
            
            // 4. Action Buttons
            var buttonPanel = CreateButtonPanel();
            mainLayout.Controls.Add(buttonPanel, 0, 3);
            
            this.Controls.Add(mainLayout);
        }

        private GroupBox CreateSummaryPanel()
        {
            var panel = new GroupBox
            {
                Text = "Food Status Summary",
                Dock = DockStyle.Fill,
                Padding = new Padding(10)
            };
            
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 1,
                ColumnCount = 3
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
            
            _currentConsumptionLabel = new Label
            {
                Text = "Current: 0 food/week",
                Font = new Font(SystemFonts.DefaultFont, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter,
                Dock = DockStyle.Fill
            };
            
            _projectedConsumptionLabel = new Label
            {
                Text = "Projected: 0 food/week",
                Font = new Font(SystemFonts.DefaultFont, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter,
                Dock = DockStyle.Fill
            };
            
            _shortageLabel = new Label
            {
                Text = "No shortage",
                Font = new Font(SystemFonts.DefaultFont, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter,
                Dock = DockStyle.Fill,
                ForeColor = Color.Green
            };
            
            layout.Controls.Add(_currentConsumptionLabel, 0, 0);
            layout.Controls.Add(_projectedConsumptionLabel, 1, 0);
            layout.Controls.Add(_shortageLabel, 2, 0);
            
            panel.Controls.Add(layout);
            return panel;
        }

        private GroupBox CreateNPCListPanel()
        {
            var panel = new GroupBox
            {
                Text = "NPC Rationing (use Ctrl/Shift for multiple selection)",
                Dock = DockStyle.Fill,
                Padding = new Padding(10)
            };
            
            _npcListView = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                MultiSelect = true,
                HideSelection = false,
                Sorting = SortOrder.None
            };
            
            // Add columns
            _npcListView.Columns.Add("Name", 150);
            _npcListView.Columns.Add("Type", 100);
            _npcListView.Columns.Add("Hunger Status", 120);
            _npcListView.Columns.Add("Food Consumption", 130);
            _npcListView.Columns.Add("Assignment", 180);
            _npcListView.Columns.Add("Ration Level", 120);
            
            // Add column click handler for sorting
            _npcListView.ColumnClick += NPCListView_ColumnClick;
            
            // Handle ration level changes
            _npcListView.DoubleClick += NPCListView_DoubleClick;
            
            panel.Controls.Add(_npcListView);
            return panel;
        }

        private Panel CreateBulkControlsPanel()
        {
            var panel = new Panel
            {
                Dock = DockStyle.Fill
            };
            
            var layout = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                Padding = new Padding(0, 10, 0, 0)
            };
            
            var label = new Label
            {
                Text = "Apply to selected NPCs:",
                AutoSize = true,
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(0, 7, 10, 0)
            };
            
            _bulkRationCombo = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 100,
                Margin = new Padding(0, 5, 10, 0)
            };
            _bulkRationCombo.Items.AddRange(new[] { "Full", "Half", "None" });
            _bulkRationCombo.SelectedIndex = 0;
            
            _applyToSelectedButton = new Button
            {
                Text = "Apply",
                Width = 80,
                Height = 30,
                Margin = new Padding(0, 0, 20, 0)
            };
            _applyToSelectedButton.Click += ApplyToSelectedButton_Click;
            
            _emergencyRationingButton = new Button
            {
                Text = "Automatic Redistribution",
                Width = 250,
                Height = 30,
                BackColor = Color.Orange,
                Margin = new Padding(0, 0, 10, 0)
            };
            _emergencyRationingButton.Click += EmergencyRationingButton_Click;
            
            _resetButton = new Button
            {
                Text = "Reset All",
                Width = 80,
                Height = 30
            };
            _resetButton.Click += ResetButton_Click;
            
            layout.Controls.Add(label);
            layout.Controls.Add(_bulkRationCombo);
            layout.Controls.Add(_applyToSelectedButton);
            layout.Controls.Add(_emergencyRationingButton);
            layout.Controls.Add(_resetButton);
            
            panel.Controls.Add(layout);
            return panel;
        }

        private Panel CreateButtonPanel()
        {
            var panel = new Panel
            {
                Dock = DockStyle.Fill
            };
            
            var layout = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(0, 10, 0, 0)
            };
            
            _cancelButton = new Button
            {
                Text = "Cancel",
                Width = 80,
                Height = 35,
                DialogResult = DialogResult.Cancel,
                Margin = new Padding(0, 0, 10, 0)
            };
            
            _applyButton = new Button
            {
                Text = "Apply Changes",
                Width = 120,
                Height = 35,
                BackColor = Color.LightGreen,
                Font = new Font(SystemFonts.DefaultFont, FontStyle.Bold)
            };
            _applyButton.Click += ApplyButton_Click;
            
            layout.Controls.Add(_cancelButton);
            layout.Controls.Add(_applyButton);
            
            panel.Controls.Add(layout);
            return panel;
        }

        private void PopulateNPCList()
        {
            _npcListView.Items.Clear();
            
            foreach (var npc in _stronghold.NPCs.OrderBy(n => n.Name))
            {
                var item = new ListViewItem(npc.Name);
                item.SubItems.Add(npc.Type.ToString());
                
                // Hunger status
                string hungerStatus = npc.HungerState.ToString();
                if (npc.StarvationProgress > 0)
                {
                    hungerStatus += $" ({npc.StarvationProgress})";
                }
                item.SubItems.Add(hungerStatus);
                
                // Food consumption
                int baseFoodNeed = GetNPCBaseFoodNeed(npc);
                int actualConsumption = GetNPCActualFoodConsumption(npc, baseFoodNeed);
                item.SubItems.Add($"{actualConsumption}/{baseFoodNeed}");
                
                // Assignment
                string assignment = npc.Assignment.Type == AssignmentType.Unassigned 
                    ? "Unassigned" 
                    : $"{npc.Assignment.Type}: {npc.Assignment.TargetName}";
                item.SubItems.Add(assignment);
                
                // Current ration level (from pending changes or hunger state)
                RationLevel currentRation = GetCurrentRationLevel(npc);
                item.SubItems.Add(currentRation.ToString());
                
                // Color coding based on hunger status
                if (npc.HungerState == HungerStatus.Starving)
                    item.ForeColor = Color.Red;
                else if (npc.HungerState == HungerStatus.Hungry)
                    item.ForeColor = Color.Orange;
                else
                    item.ForeColor = Color.Black;
                
                item.Tag = npc.Id;
                _npcListView.Items.Add(item);
            }
            
            // Auto-resize columns
            foreach (ColumnHeader column in _npcListView.Columns)
            {
                column.Width = -2; // Auto-size to content
            }
        }

        private RationLevel GetCurrentRationLevel(NPC npc)
        {
            // Check if there's a pending change
            if (_npcRationingChanges.ContainsKey(npc.Id))
            {
                return _npcRationingChanges[npc.Id];
            }
            
            // Otherwise, use the actual ration level from the NPC
            return npc.RationLevel;
        }

        private int GetNPCBaseFoodNeed(NPC npc)
        {
            var foodUpkeep = npc.UpkeepCost.Find(c => c.ResourceType == ResourceType.Food);
            return foodUpkeep?.Amount ?? 2; // Default to 2 if not found
        }


        private void UpdateFoodStatusDisplay()
        {
            var foodResource = _stronghold.Resources.Find(r => r.Type == ResourceType.Food);
            if (foodResource == null) return;
            
            // Calculate current consumption
            int currentConsumption = 0;
            foreach (var npc in _stronghold.NPCs)
            {
                var foodUpkeep = npc.UpkeepCost.Find(c => c.ResourceType == ResourceType.Food);
                if (foodUpkeep != null)
                {
                    currentConsumption += GetNPCActualFoodConsumption(npc, foodUpkeep.Amount);
                }
            }
            
            // Calculate projected consumption with pending changes
            int projectedConsumption = CalculateProjectedConsumption();
            
            // Update labels
            _currentConsumptionLabel.Text = $"Current: {currentConsumption} food/week";
            _projectedConsumptionLabel.Text = $"Projected: {projectedConsumption} food/week";
            
            // Calculate shortage
            int availableFood = foodResource.Amount + foodResource.WeeklyProduction;
            if (projectedConsumption > availableFood)
            {
                int shortage = projectedConsumption - availableFood;
                _shortageLabel.Text = $"⚠️ Shortage: {shortage} food";
                _shortageLabel.ForeColor = Color.Red;
            }
            else
            {
                _shortageLabel.Text = "✓ No shortage";
                _shortageLabel.ForeColor = Color.Green;
            }
        }

        private int CalculateProjectedConsumption()
        {
            int total = 0;
            foreach (var npc in _stronghold.NPCs)
            {
                var foodUpkeep = npc.UpkeepCost.Find(c => c.ResourceType == ResourceType.Food);
                if (foodUpkeep != null)
                {
                    // Use the same calculation as the main game system
                    int actualConsumption = GetNPCActualFoodConsumption(npc, foodUpkeep.Amount);
                    total += actualConsumption;
                }
            }
            return total;
        }

        // Calculate actual food consumption for an NPC based on their hunger/rationing state
        // This matches the calculation in GameStateService
        private int GetNPCActualFoodConsumption(NPC npc, int baseFoodNeed)
        {
            RationLevel rationLevel = GetCurrentRationLevel(npc);
            return rationLevel switch
            {
                RationLevel.Full => baseFoodNeed,      // Full rations = full consumption
                RationLevel.Half => (int)Math.Ceiling(baseFoodNeed / 2.0),  // Half rations = half consumption (rounded up)
                RationLevel.None => 0,                     // No rations = no consumption
                _ => baseFoodNeed
            };
        }

        #region Event Handlers

        private void NPCListView_ColumnClick(object sender, ColumnClickEventArgs e)
        {
            // Toggle sort order if clicking the same column
            if (e.Column == _lastSortedColumn)
            {
                _sortOrder = _sortOrder == SortOrder.Ascending ? SortOrder.Descending : SortOrder.Ascending;
            }
            else
            {
                _sortOrder = SortOrder.Ascending;
                _lastSortedColumn = e.Column;
            }
            
            _npcListView.ListViewItemSorter = new NPCListViewSorter(e.Column, _sortOrder);
            _npcListView.Sort();
        }

        private void NPCListView_DoubleClick(object sender, EventArgs e)
        {
            if (_npcListView.SelectedItems.Count == 1)
            {
                var selectedItem = _npcListView.SelectedItems[0];
                string npcId = selectedItem.Tag.ToString();
                CycleRationLevel(npcId, selectedItem);
            }
        }

        private void CycleRationLevel(string npcId, ListViewItem item)
        {
            RationLevel currentLevel = GetCurrentRationLevel(_stronghold.NPCs.Find(n => n.Id == npcId));
            RationLevel newLevel = currentLevel switch
            {
                RationLevel.Full => RationLevel.Half,
                RationLevel.Half => RationLevel.None,
                RationLevel.None => RationLevel.Full,
                _ => RationLevel.Full
            };
            
            _npcRationingChanges[npcId] = newLevel;
            item.SubItems[5].Text = newLevel.ToString();
            UpdateFoodStatusDisplay();
        }

        private void ApplyToSelectedButton_Click(object sender, EventArgs e)
        {
            if (_npcListView.SelectedItems.Count == 0)
            {
                MessageBox.Show("Please select one or more NPCs first.", "No Selection", 
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            
            RationLevel selectedRation = (RationLevel)_bulkRationCombo.SelectedIndex;
            
            foreach (ListViewItem item in _npcListView.SelectedItems)
            {
                string npcId = item.Tag.ToString();
                _npcRationingChanges[npcId] = selectedRation;
                item.SubItems[5].Text = selectedRation.ToString();
            }
            
            UpdateFoodStatusDisplay();
            PopulateNPCList(); // Refresh to update colors and consumption
        }

        private void EmergencyRationingButton_Click(object sender, EventArgs e)
        {
            var result = MessageBox.Show(
                "Apply automatic redistribution?\n\n" +
                "This will intelligently redistribute available food using this priority:\n\n" +
                "1. Keep food producers at FULL rations (maximize food production)\n" +
                "2. Prevent starvation: Improve ALL Starving NPCs to Hungry (None → Half)\n" +
                "   • Within starving NPCs: Essential → Non-Essential → Unassigned\n" +
                "3. Relieve hunger: Improve ALL Hungry NPCs to WellFed (Half → Full)\n" +
                "   • Within hungry NPCs: Essential → Non-Essential → Unassigned\n" +
                "4. If shortage remains, cut by priority (least important first)\n\n" +
                "The system can both improve and cut rations based on available food supply.",
                "Automatic Redistribution",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);
                
            if (result == DialogResult.Yes)
            {
                ApplyEmergencyRationing();
            }
        }

        private void ResetButton_Click(object sender, EventArgs e)
        {
            var result = MessageBox.Show(
                "Reset all rationing changes?",
                "Reset Changes",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);
                
            if (result == DialogResult.Yes)
            {
                _npcRationingChanges.Clear();
                PopulateNPCList();
                UpdateFoodStatusDisplay();
            }
        }

        private void ApplyButton_Click(object sender, EventArgs e)
        {
            // Apply all pending rationing changes to NPCs
            foreach (var change in _npcRationingChanges)
            {
                var npc = _stronghold.NPCs.Find(n => n.Id == change.Key);
                if (npc != null)
                {
                    ApplyRationingToNPC(npc, change.Value);
                }
            }
            
            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        #endregion

        #region Helper Methods

        private void ApplyEmergencyRationing()
        {
            // Clear existing changes
            _npcRationingChanges.Clear();
            
            // Get available food
            var foodResource = _stronghold.Resources.Find(r => r.Type == ResourceType.Food);
            int availableFood = foodResource.Amount + foodResource.WeeklyProduction;
            
            // Step 1: Keep food producers at full rations (maximize food production)
            var foodProducers = GetFoodProducingNPCs();
            foreach (var npc in foodProducers)
            {
                _npcRationingChanges[npc.Id] = RationLevel.Full;
            }
            
            // Step 2: Try to improve NPCs using starvation-first logic, then priority within each hunger state
            var essentialWorkers = GetEssentialBuildingWorkers().OrderBy(GetStarvationRiskScore).ToList();
            var nonEssentialWorkers = GetNonEssentialBuildingWorkers().OrderBy(GetStarvationRiskScore).ToList();
            var unassignedNPCs = _stronghold.NPCs.Where(n => n.Assignment.Type == AssignmentType.Unassigned).OrderBy(GetStarvationRiskScore).ToList();
            
            // Define priority groups for within each hunger state (most important first)
            var priorityGroups = new List<(List<NPC> npcs, string groupName)>
            {
                (essentialWorkers, "Essential Workers"),
                (nonEssentialWorkers, "Non-Essential Workers"),
                (unassignedNPCs, "Unassigned NPCs")
            };
            
            // First priority: Try to improve ALL Starving NPCs to Hungry (None → Half)
            // Process by priority within starving NPCs
            foreach (var (npcs, groupName) in priorityGroups)
            {
                foreach (var npc in npcs.Where(n => n.HungerState == HungerStatus.Starving))
                {
                    _npcRationingChanges[npc.Id] = RationLevel.Half;
                    if (CalculateProjectedConsumption() > availableFood)
                    {
                        // Can't afford this improvement, revert to None
                        _npcRationingChanges[npc.Id] = RationLevel.None;
                    }
                }
            }
            
            // Second priority: Try to improve ALL Hungry NPCs to WellFed (Half → Full)
            // Process by priority within hungry NPCs
            foreach (var (npcs, groupName) in priorityGroups)
            {
                foreach (var npc in npcs.Where(n => n.HungerState == HungerStatus.Hungry))
                {
                    _npcRationingChanges[npc.Id] = RationLevel.Full;
                    if (CalculateProjectedConsumption() > availableFood)
                    {
                        // Can't afford this improvement, revert to Half
                        _npcRationingChanges[npc.Id] = RationLevel.Half;
                    }
                }
            }
            
            // Step 3: If we still have a shortage, start cutting by priority
            int currentConsumption = CalculateProjectedConsumption();
            if (currentConsumption > availableFood)
            {
                // Get NPC groups and sort by starvation risk (least at risk first for cutting)
                var cuttingUnassignedNPCs = _stronghold.NPCs.Where(n => n.Assignment.Type == AssignmentType.Unassigned).OrderByDescending(GetStarvationRiskScore).ToList();
                var cuttingNonEssentialWorkers = GetNonEssentialBuildingWorkers().OrderByDescending(GetStarvationRiskScore).ToList();
                var cuttingEssentialWorkers = GetEssentialBuildingWorkers().OrderByDescending(GetStarvationRiskScore).ToList();
                
                // Define priority groups for cutting (least at risk first)
                var cuttingGroups = new List<(List<NPC> npcs, RationLevel rationLevel, string groupName)>
                {
                    (cuttingUnassignedNPCs, RationLevel.Half, "Unassigned NPCs to Half"),
                    (cuttingNonEssentialWorkers, RationLevel.Half, "Non-Essential Workers to Half"),
                    (cuttingEssentialWorkers, RationLevel.Half, "Essential Workers to Half"),
                    (cuttingUnassignedNPCs, RationLevel.None, "Unassigned NPCs to None"),
                    (cuttingNonEssentialWorkers, RationLevel.None, "Non-Essential Workers to None"),
                    (cuttingEssentialWorkers, RationLevel.None, "Essential Workers to None")
                };
                
                // Process each cutting group
                foreach (var (npcs, rationLevel, groupName) in cuttingGroups)
                {
                    // Process each NPC individually within the group
                    foreach (var npc in npcs)
                    {
                        // Apply ration level to this NPC
                        _npcRationingChanges[npc.Id] = rationLevel;
                        
                        // Check if we've reached the goal
                        currentConsumption = CalculateProjectedConsumption();
                        if (currentConsumption <= availableFood)
                        {
                            // Goal achieved! Stop processing
                            PopulateNPCList();
                            UpdateFoodStatusDisplay();
                            return;
                        }
                    }
                }
            }
            
            // Apply changes to UI
            PopulateNPCList();
            UpdateFoodStatusDisplay();
        }

        private int GetStarvationRiskScore(NPC npc)
        {
            // Lower score = higher priority (less at risk)
            // Higher score = lower priority (more at risk)
            
            // Already starving NPCs get highest priority (lowest score)
            if (npc.HungerState == HungerStatus.Starving)
                return 0;
            
            // NPCs with high StarvationProgress get higher priority (lower score)
            // StarvationProgress of 4+ means they're about to starve
            if (npc.StarvationProgress >= 4)
                return 1;
            
            // NPCs with moderate StarvationProgress get medium priority
            if (npc.StarvationProgress >= 2)
                return 2;
            
            // NPCs with low StarvationProgress get lower priority
            if (npc.StarvationProgress >= 1)
                return 3;
            
            // Well-fed NPCs get lowest priority (highest score)
            return 4;
        }

        private List<NPC> GetFoodProducingNPCs()
        {
            var foodProducers = new List<NPC>();
            foreach (var building in _stronghold.Buildings)
            {
                if (building.IsFunctional())
                {
                    // Update the building's production to ensure we have current data
                    var assignedNPCs = building.AssignedWorkers
                        .Select(id => _stronghold.NPCs.Find(n => n.Id == id))
                        .Where(npc => npc != null)
                        .ToList();
                    building.UpdateProduction(assignedNPCs);
                    
                    if (DoesBuildingProduceFood(building))
                    {
                        foodProducers.AddRange(assignedNPCs);
                    }
                }
            }
            return foodProducers.Distinct().ToList();
        }

        private bool DoesBuildingProduceFood(Building building)
        {
            // Check if the building has the potential to produce food (ignoring current hunger states)
            // This prevents the vicious cycle where starving NPCs can't get food because they produce 0 food
            if (!building.IsFunctional())
                return false;
                
            // We need to check the building's potential to produce food, not its current hunger-affected output
            // Since we can't access LoadBuildingData() directly, we'll use a different approach:
            // Check if the building has any food production in its current ActualProduction
            // but also check if it's a known food-producing building type
            var hasFoodProduction = building.ActualProduction.Any(p => p.ResourceType == ResourceType.Food);
            
            // If it currently has food production, it's definitely a food producer
            if (hasFoodProduction)
                return true;
                
            // If it doesn't have current production, check if it's a known food-producing building type
            // This handles the case where starving workers make production = 0
            var foodProducingTypes = new[] { "Farm", "Orchard", "Vineyard", "Dairy", "Butcher", "Bakery", "Mill" };
            return foodProducingTypes.Any(type => building.TypeName.Contains(type, StringComparison.OrdinalIgnoreCase));
        }

        private List<NPC> GetEssentialBuildingWorkers()
        {
            // Essential buildings: buildings marked as essential by the user (excluding food producers)
            var essentialWorkers = new List<NPC>();
            
            foreach (var building in _stronghold.Buildings)
            {
                // Include workers from essential buildings (both functional and under construction)
                if (building.IsEssential && !DoesBuildingProduceFood(building))
                {
                    var workers = building.AssignedWorkers
                        .Select(id => _stronghold.NPCs.Find(n => n.Id == id))
                        .Where(npc => npc != null);
                    essentialWorkers.AddRange(workers);
                }
                
                // Add dedicated construction crew as essential workers
                var constructionWorkers = building.DedicatedConstructionCrew
                    .Select(id => _stronghold.NPCs.Find(n => n.Id == id))
                    .Where(npc => npc != null);
                essentialWorkers.AddRange(constructionWorkers);
            }
            return essentialWorkers.Distinct().ToList();
        }

        private List<NPC> GetNonEssentialBuildingWorkers()
        {
            // Non-essential buildings: buildings NOT marked as essential by the user (excluding food producers)
            var nonEssentialWorkers = new List<NPC>();
            
            foreach (var building in _stronghold.Buildings)
            {
                // Include workers from non-essential buildings (both functional and under construction)
                if (!building.IsEssential && !DoesBuildingProduceFood(building))
                {
                    var workers = building.AssignedWorkers
                        .Select(id => _stronghold.NPCs.Find(n => n.Id == id))
                        .Where(npc => npc != null);
                    nonEssentialWorkers.AddRange(workers);
                }
            }
            return nonEssentialWorkers.Distinct().ToList();
        }

        private void ApplyRationingToNPC(NPC npc, RationLevel rationLevel)
        {
            // Set the ration level first
            npc.RationLevel = rationLevel;
            
            // Apply immediate hunger state changes based on rationing
            switch (rationLevel)
            {
                case RationLevel.Full:
                    npc.HungerState = HungerStatus.WellFed;
                    // Don't reset StarvationProgress here - only reset during next turn logic
                    break;
                case RationLevel.Half:
                    npc.HungerState = HungerStatus.Hungry;
                    // Don't reset StarvationProgress - voluntary rationing doesn't reset progress
                    break;
                case RationLevel.None:
                    // If NPC has high starvation progress, they become starving immediately
                    if (npc.StarvationProgress >= 4)
                    {
                        npc.HungerState = HungerStatus.Starving;
                    }
                    else
                    {
                        npc.HungerState = HungerStatus.Hungry; // Immediately hungry, not starving
                    }
                    // Don't reset StarvationProgress - will progress faster with no rations
                    break;
            }
        }

        #endregion
    }


    // Custom sorter for the NPC ListView
    public class NPCListViewSorter : System.Collections.IComparer
    {
        private int _column;
        private SortOrder _sortOrder;

        public NPCListViewSorter(int column, SortOrder sortOrder)
        {
            _column = column;
            _sortOrder = sortOrder;
        }

        public int Compare(object x, object y)
        {
            ListViewItem itemX = (ListViewItem)x;
            ListViewItem itemY = (ListViewItem)y;
            
            int compareResult = 0;
            
            switch (_column)
            {
                case 0: // Name
                case 1: // Type
                case 2: // Hunger Status
                case 4: // Assignment
                case 5: // Ration Level
                    compareResult = string.Compare(itemX.SubItems[_column].Text, itemY.SubItems[_column].Text, StringComparison.OrdinalIgnoreCase);
                    break;
                case 3: // Food Consumption (e.g., "1/2")
                    var consumptionX = itemX.SubItems[_column].Text.Split('/');
                    var consumptionY = itemY.SubItems[_column].Text.Split('/');
                    if (consumptionX.Length == 2 && consumptionY.Length == 2)
                    {
                        int actualX = int.Parse(consumptionX[0]);
                        int actualY = int.Parse(consumptionY[0]);
                        compareResult = actualX.CompareTo(actualY);
                    }
                    break;
            }
            
            return _sortOrder == SortOrder.Ascending ? compareResult : -compareResult;
        }
    }
}
