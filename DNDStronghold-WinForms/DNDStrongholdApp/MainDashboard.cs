using System;
using System.Drawing;
using System.Windows.Forms;
using DNDStrongholdApp.Models;
using DNDStrongholdApp.Services;
using System.Linq;
using DNDStrongholdApp.Forms;
using System.IO;
using System.Text.Json;

namespace DNDStrongholdApp;

public partial class MainDashboard : Form
{
    // Reference to the game state service
    private GameStateService _gameStateService;
    
    // Reference to the current stronghold
    private Stronghold _stronghold => _gameStateService?.GetCurrentStronghold() ?? throw new InvalidOperationException("GameStateService not initialized");
    
    // UI controls that need to be updated
    private StatusStrip _statusStrip;
    private ToolStripStatusLabel _weekLabel;
    private ToolStripStatusLabel _yearLabel;
    private ToolStripStatusLabel _seasonLabel;
    private ToolStripStatusLabel _goldLabel;
    private TabControl _tabControl;
    private Button _nextTurnButton;

    // Track last created building/NPC for selection after refresh
    private string _lastCreatedNpcId = null;

    // Add these fields at class level
    private string _selectedBuildingId;
    private int _lastSortedColumn = -1;
    private SortOrder _lastSortOrder = SortOrder.None;
    
    // Add CurrentProject property to track building projects
    private Building CurrentProject => _stronghold?.Buildings.FirstOrDefault(b => 
        b.ConstructionStatus == BuildingStatus.UnderConstruction || 
        b.ConstructionStatus == BuildingStatus.Repairing || 
        b.ConstructionStatus == BuildingStatus.Upgrading);

    public MainDashboard()
    {
        try
        {
            if (Program.DebugMode)
                MessageBox.Show("Starting MainDashboard initialization...", "Debug");
            InitializeComponent();
            
            if (Program.DebugMode)
                MessageBox.Show("Getting GameStateService instance...", "Debug");
            // Get reference to game state service first
            _gameStateService = GameStateService.GetInstance();
            _gameStateService.GameStateChanged += GameStateService_GameStateChanged;
            
            if (Program.DebugMode)
                MessageBox.Show("Initializing dashboard...", "Debug");
            InitializeDashboard();
            if (Program.DebugMode)
                MessageBox.Show("MainDashboard initialization complete.", "Debug");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error in MainDashboard initialization: {ex.Message}\n\nStack Trace:\n{ex.StackTrace}", 
                "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            throw;
        }
    }

    private void InitializeDashboard()
    {
        // Set window title
        this.Text = "D&D Stronghold Management";
        
        // Set window size (20% larger)
        this.Width = (int)(1024 * 1.2); // 1228
        this.Height = (int)(768 * 1.2); // 921
        
        // Center the form on screen
        this.StartPosition = FormStartPosition.CenterScreen;

        // Create main menu
        CreateMainMenu();

        // Initialize UI components in the correct order for proper layering
        CreateStatusBar();
        CreateNextTurnButton();
        CreateTabControl();
        
        // Ensure proper Z-order of controls
        this.Controls.SetChildIndex(_tabControl, 0); // Tab control at the back
        this.Controls.SetChildIndex(_nextTurnButton, 1); // Next turn button above tab control
        this.Controls.SetChildIndex(_statusStrip, 2); // Status strip on top
    }

    private void CreateMainMenu()
    {
        MenuStrip menuStrip = new MenuStrip();
        menuStrip.Dock = DockStyle.Top;

        // File menu
        ToolStripMenuItem fileMenu = new ToolStripMenuItem("File");
        
        ToolStripMenuItem newStrongholdItem = new ToolStripMenuItem("New Stronghold");
        newStrongholdItem.Click += NewStrongholdItem_Click;
        
        ToolStripMenuItem saveGameItem = new ToolStripMenuItem("Save State");
        saveGameItem.Click += SaveButton_Click;
        
        ToolStripMenuItem loadGameItem = new ToolStripMenuItem("Load State");
        loadGameItem.Click += LoadButton_Click;
        
        ToolStripMenuItem exitItem = new ToolStripMenuItem("Exit");
        exitItem.Click += (s, e) => this.Close();

        fileMenu.DropDownItems.Add(newStrongholdItem);
        fileMenu.DropDownItems.Add(new ToolStripSeparator());
        fileMenu.DropDownItems.Add(saveGameItem);
        fileMenu.DropDownItems.Add(loadGameItem);
        fileMenu.DropDownItems.Add(new ToolStripSeparator());
        fileMenu.DropDownItems.Add(exitItem);

        // Tools menu
        ToolStripMenuItem toolsMenu = new ToolStripMenuItem("Tools");
        
        ToolStripMenuItem buildingDataEditorItem = new ToolStripMenuItem("Building Data Editor");
        buildingDataEditorItem.Click += (s, e) =>
        {
            using (var editor = new Forms.BuildingDataEditor())
            {
                editor.ShowDialog();
            }
        };
        
        toolsMenu.DropDownItems.Add(buildingDataEditorItem);

        // Add DM Mode toggle
        ToolStripMenuItem dmModeItem = new ToolStripMenuItem("DM Mode");
        dmModeItem.CheckOnClick = true;
        dmModeItem.Checked = _gameStateService.DMMode;
        dmModeItem.Click += (s, e) =>
        {
            _gameStateService.DMMode = dmModeItem.Checked;
        };
        toolsMenu.DropDownItems.Add(new ToolStripSeparator());
        toolsMenu.DropDownItems.Add(dmModeItem);

        menuStrip.Items.Add(fileMenu);
        menuStrip.Items.Add(toolsMenu);
        
        this.Controls.Add(menuStrip);
        this.MainMenuStrip = menuStrip;
    }

    private void CreateNextTurnButton()
    {
        _nextTurnButton = new Button();
        _nextTurnButton.Text = "Next Turn";
        _nextTurnButton.Font = new Font(_nextTurnButton.Font.FontFamily, 12, FontStyle.Bold);
        _nextTurnButton.BackColor = Color.FromArgb(200, 230, 200); // Light green color
        _nextTurnButton.FlatStyle = FlatStyle.Flat;
        _nextTurnButton.Dock = DockStyle.Bottom;
        _nextTurnButton.Height = 40;
        _nextTurnButton.Click += NextTurnButton_Click;
        
        this.Controls.Add(_nextTurnButton);
    }

    private void CreateTabControl()
    {
        // Create tab control
        _tabControl = new TabControl();
        _tabControl.Dock = DockStyle.Fill;
        _tabControl.Visible = true; // Ensure tab control is visible
        
        // Create tabs
        TabPage dashboardTab = new TabPage("Dashboard");
        TabPage buildingsTab = new TabPage("Buildings");
        TabPage npcsTab = new TabPage("NPCs");
        TabPage resourcesTab = new TabPage("Resources");
        TabPage journalTab = new TabPage("Journal");
        TabPage missionsTab = new TabPage("Missions");
        
        // Add tabs to tab control
        _tabControl.TabPages.Add(dashboardTab);
        _tabControl.TabPages.Add(buildingsTab);
        _tabControl.TabPages.Add(npcsTab);
        _tabControl.TabPages.Add(resourcesTab);
        _tabControl.TabPages.Add(journalTab);
        _tabControl.TabPages.Add(missionsTab);
        
        // Add tab control to form
        this.Controls.Add(_tabControl);
        
        // Initialize tab contents
        InitializeDashboardTab(dashboardTab);
        InitializeBuildingsTab(buildingsTab);
        InitializeNPCsTab(npcsTab);
        InitializeResourcesTab(resourcesTab);
        InitializeJournalTab(journalTab);
        InitializeMissionsTab(missionsTab);
    }

    private void CreateStatusBar()
    {
        // Create status strip
        _statusStrip = new StatusStrip();
        _statusStrip.Dock = DockStyle.Bottom;
        
        // Add status labels
        _weekLabel = new ToolStripStatusLabel($"Week: {_stronghold.CurrentWeek}");
        _yearLabel = new ToolStripStatusLabel($"Year: {_stronghold.YearsSinceFoundation}");
        _seasonLabel = new ToolStripStatusLabel($"Season: {_stronghold.CurrentSeason}");
        _goldLabel = new ToolStripStatusLabel($"Gold: {_stronghold.Treasury}");
        
        // Add labels to status strip
        _statusStrip.Items.Add(_weekLabel);
        _statusStrip.Items.Add(new ToolStripSeparator());
        _statusStrip.Items.Add(_yearLabel);
        _statusStrip.Items.Add(new ToolStripSeparator());
        _statusStrip.Items.Add(_seasonLabel);
        _statusStrip.Items.Add(new ToolStripSeparator());
        _statusStrip.Items.Add(_goldLabel);
        
        // Add status strip to form
        this.Controls.Add(_statusStrip);
    }

    #region Tab Initialization Methods

    private void InitializeDashboardTab(TabPage tab)
    {
        // Main layout: 2 columns, 1 row
        TableLayoutPanel mainLayout = new TableLayoutPanel();
        mainLayout.Dock = DockStyle.Fill;
        mainLayout.ColumnCount = 2;
        mainLayout.RowCount = 1;
        mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        
        // Left column: vertical stack (Stronghold Info, Buildings, Recent Events)
        TableLayoutPanel leftLayout = new TableLayoutPanel();
        leftLayout.Dock = DockStyle.Fill;
        leftLayout.ColumnCount = 1;
        leftLayout.RowCount = 3;
        leftLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 22F)); // Stronghold Info (was 18F)
        leftLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 53F)); // Buildings (was 57F)
        leftLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 25F)); // Recent Events
        
        GroupBox strongholdInfoPanel = CreateStrongholdInfoPanel();
        GroupBox buildingSummaryPanel = CreateBuildingSummaryPanel();
        GroupBox recentEventsPanel = CreateRecentEventsPanel();
        leftLayout.Controls.Add(strongholdInfoPanel, 0, 0);
        leftLayout.Controls.Add(buildingSummaryPanel, 0, 1);
        leftLayout.Controls.Add(recentEventsPanel, 0, 2);
        
        // Right column: vertical stack (Resources, NPCs, Controls)
        TableLayoutPanel rightLayout = new TableLayoutPanel();
        rightLayout.Dock = DockStyle.Fill;
        rightLayout.ColumnCount = 1;
        rightLayout.RowCount = 3;
        rightLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 38F)); // Resources (tall)
        rightLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 47F)); // NPCs (medium)
        rightLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 15F)); // Controls (small)
        
        GroupBox resourceSummaryPanel = CreateResourceSummaryPanel();
        GroupBox npcSummaryPanel = CreateNPCSummaryPanel();
        GroupBox controlsPanel = CreateControlsPanel();
        rightLayout.Controls.Add(resourceSummaryPanel, 0, 0);
        rightLayout.Controls.Add(npcSummaryPanel, 0, 1);
        rightLayout.Controls.Add(controlsPanel, 0, 2);
        
        // Add left and right layouts to main layout
        mainLayout.Controls.Add(leftLayout, 0, 0);
        mainLayout.Controls.Add(rightLayout, 1, 0);
        
        // Add main layout to tab
        tab.Controls.Add(mainLayout);
    }

    private GroupBox CreateStrongholdInfoPanel()
    {
        GroupBox groupBox = new GroupBox();
        groupBox.Text = "Stronghold Information";
        groupBox.Dock = DockStyle.Fill;
        groupBox.Margin = new Padding(5);
        
        TableLayoutPanel layout = new TableLayoutPanel();
        layout.Dock = DockStyle.Fill;
        layout.ColumnCount = 2;
        layout.RowCount = 3;
        
        // Add labels
        layout.Controls.Add(new Label { Text = "Name:", TextAlign = ContentAlignment.MiddleRight }, 0, 0);
        layout.Controls.Add(new Label { Text = _stronghold.Name, TextAlign = ContentAlignment.MiddleLeft }, 1, 0);
        
        layout.Controls.Add(new Label { Text = "Location:", TextAlign = ContentAlignment.MiddleRight }, 0, 1);
        layout.Controls.Add(new Label { Text = _stronghold.Location, TextAlign = ContentAlignment.MiddleLeft }, 1, 1);
        
        layout.Controls.Add(new Label { Text = "Level:", TextAlign = ContentAlignment.MiddleRight }, 0, 2);
        layout.Controls.Add(new Label { Text = _stronghold.Level.ToString(), TextAlign = ContentAlignment.MiddleLeft }, 1, 2);
        
        groupBox.Controls.Add(layout);
        return groupBox;
    }

    private GroupBox CreateResourceSummaryPanel()
    {
        GroupBox groupBox = new GroupBox();
        groupBox.Text = "Resources";
        groupBox.Dock = DockStyle.Fill;
        groupBox.Margin = new Padding(5);
        
        ListView listView = new ListView();
        listView.Dock = DockStyle.Fill;
        listView.View = View.Details;
        listView.FullRowSelect = true;
        
        // Add columns
        listView.Columns.Add("Resource", 100);
        listView.Columns.Add("Amount", 70);
        listView.Columns.Add("Weekly Change", 100);
        
        // Dynamic column widths (40%, 25%, 35%)
        void ResizeResourceColumns(object s, EventArgs e)
        {
            int totalWidth = listView.ClientSize.Width;
            listView.Columns[0].Width = (int)(totalWidth * 0.4);
            listView.Columns[1].Width = (int)(totalWidth * 0.25);
            listView.Columns[2].Width = (int)(totalWidth * 0.35);
        }
        listView.Resize += ResizeResourceColumns;
        // Initial sizing
        ResizeResourceColumns(null, null);
        
        // Add items for each resource
        foreach (var resource in _stronghold.Resources)
        {
            ListViewItem item = new ListViewItem(resource.Type.ToString());
            item.SubItems.Add(resource.Amount.ToString());
            item.SubItems.Add(resource.NetWeeklyChange.ToString());
            listView.Items.Add(item);
        }
        
        groupBox.Controls.Add(listView);
        return groupBox;
    }

    private GroupBox CreateBuildingSummaryPanel()
    {
        GroupBox groupBox = new GroupBox();
        groupBox.Text = "Buildings";
        groupBox.Dock = DockStyle.Fill;
        groupBox.Margin = new Padding(5);
        
        ListView listView = new ListView();
        listView.Dock = DockStyle.Fill;
        listView.View = View.Details;
        listView.FullRowSelect = true;
        
        // Add columns
        listView.Columns.Add("Name", 150);
        listView.Columns.Add("Type", 100);
        listView.Columns.Add("Status", 150);
        
        // Add items for each building
        foreach (var building in _stronghold.Buildings)
        {
            string statusText = building.ConstructionStatus.ToString();
            if (building.ConstructionStatus == BuildingStatus.UnderConstruction ||
                building.ConstructionStatus == BuildingStatus.Repairing ||
                building.ConstructionStatus == BuildingStatus.Upgrading)
            {
                if (building.AssignedWorkers.Count == 0)
                {
                    statusText += " (No Workers)";
                }
                else
                {
                    statusText += $" ({building.ConstructionProgress}%)";
                }
            }
            ListViewItem item = new ListViewItem(building.Name);
            item.SubItems.Add(building.Type.ToString());
            item.SubItems.Add(statusText);
            
            // Set color based on building status
            if (building.ConstructionStatus == BuildingStatus.Damaged)
            {
                item.ForeColor = Color.Red;
            }
            else if (building.ConstructionStatus == BuildingStatus.UnderConstruction)
            {
                item.ForeColor = Color.Blue;
            }
            else if (building.ConstructionStatus == BuildingStatus.Repairing)
            {
                item.ForeColor = Color.Orange;
            }
            
            listView.Items.Add(item);
        }
        
        groupBox.Controls.Add(listView);
        return groupBox;
    }

    private GroupBox CreateNPCSummaryPanel()
    {
        GroupBox groupBox = new GroupBox();
        groupBox.Text = "NPCs";
        groupBox.Dock = DockStyle.Fill;
        groupBox.Margin = new Padding(5);
        
        ListView listView = new ListView();
        listView.Dock = DockStyle.Fill;
        listView.View = View.Details;
        listView.FullRowSelect = true;
        
        // Add columns
        listView.Columns.Add("Name", 150);
        listView.Columns.Add("Type", 100);
        listView.Columns.Add("Assignment", 150);
        listView.Columns.Add("Status", 100);
        
        // Dynamic column widths (40%, 20%, 25%, 15%)
        void ResizeNPCColumns(object s, EventArgs e)
        {
            int totalWidth = listView.ClientSize.Width;
            listView.Columns[0].Width = (int)(totalWidth * 0.4);
            listView.Columns[1].Width = (int)(totalWidth * 0.2);
            listView.Columns[2].Width = (int)(totalWidth * 0.25);
            listView.Columns[3].Width = (int)(totalWidth * 0.15);
        }
        listView.Resize += ResizeNPCColumns;
        ResizeNPCColumns(null, null);
        
        // Add items for each NPC
        foreach (var npc in _stronghold.NPCs)
        {
            ListViewItem item = new ListViewItem(npc.Name);
            item.SubItems.Add(npc.Type.ToString());
            item.SubItems.Add(npc.Assignment.Type == AssignmentType.Unassigned ? "Unassigned" : npc.Assignment.TargetName);
            // Status column: show health states or 'Healthy'
            string status = npc.States != null && npc.States.Any()
                ? string.Join(", ", npc.States.Select(s => s.Type.ToString()))
                : "Healthy";
            item.SubItems.Add(status);
            item.Tag = npc.Id;
            listView.Items.Add(item);
        }
        // Double-click to open in NPCs tab
        listView.DoubleClick += (s, e) => {
            if (listView.SelectedItems.Count > 0)
            {
                string npcId = (string)listView.SelectedItems[0].Tag;
                ShowNPCInTab(npcId);
            }
        };
        
        groupBox.Controls.Add(listView);
        return groupBox;
    }

    private GroupBox CreateRecentEventsPanel()
    {
        GroupBox groupBox = new GroupBox();
        groupBox.Text = "Recent Events";
        groupBox.Dock = DockStyle.Fill;
        groupBox.Margin = new Padding(5);
        
        ListView listView = new ListView();
        listView.Dock = DockStyle.Fill;
        listView.View = View.Details;
        listView.FullRowSelect = true;
        
        // Add columns
        listView.Columns.Add("Date", 100);
        listView.Columns.Add("Event", 300);
        
        // Dynamic column widths (30%, 70%)
        void ResizeEventColumns(object s, EventArgs e)
        {
            int totalWidth = listView.ClientSize.Width;
            listView.Columns[0].Width = (int)(totalWidth * 0.3);
            listView.Columns[1].Width = (int)(totalWidth * 0.7);
        }
        listView.Resize += ResizeEventColumns;
        ResizeEventColumns(null, null);
        
        // Add items for each journal entry (most recent first)
        foreach (var entry in _stronghold.Journal)
        {
            ListViewItem item = new ListViewItem(entry.Date);
            item.SubItems.Add(entry.Title);
            listView.Items.Add(item);
        }
        
        groupBox.Controls.Add(listView);
        return groupBox;
    }

    private GroupBox CreateControlsPanel()
    {
        GroupBox groupBox = new GroupBox();
        groupBox.Text = "Controls";
        groupBox.Dock = DockStyle.Fill;
        groupBox.Margin = new Padding(5);
        
        TableLayoutPanel layout = new TableLayoutPanel();
        layout.Dock = DockStyle.Fill;
        layout.ColumnCount = 2;
        layout.RowCount = 1;
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        
        // Add Save and Load buttons side by side, narrower
        Button saveButton = new Button();
        saveButton.Text = "Save State";
        saveButton.Dock = DockStyle.None;
        saveButton.Width = 100;
        saveButton.Height = 32;
        saveButton.Margin = new Padding(10, 10, 5, 10);
        saveButton.Click += SaveButton_Click;
        
        Button loadButton = new Button();
        loadButton.Text = "Load State";
        loadButton.Dock = DockStyle.None;
        loadButton.Width = 100;
        loadButton.Height = 32;
        loadButton.Margin = new Padding(5, 10, 10, 10);
        loadButton.Click += LoadButton_Click;
        
        layout.Controls.Add(saveButton, 0, 0);
        layout.Controls.Add(loadButton, 1, 0);
        
        groupBox.Controls.Add(layout);
        return groupBox;
    }

    private void InitializeResourcesTab(TabPage tab)
    {
        // Create layout panel
        TableLayoutPanel layout = new TableLayoutPanel();
        layout.Dock = DockStyle.Fill;
        layout.ColumnCount = 2;
        layout.RowCount = 2;
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 65F)); // Main list 65%
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35F)); // Details 35%
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 70F));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 30F));

        // Create resources list view
        ListView resourcesListView = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            MultiSelect = false,
            GridLines = true,
            Tag = "ResourcesListView"
        };
        resourcesListView.Columns.AddRange(new[]
        {
            new ColumnHeader { Text = "Resource", Width = 150 },
            new ColumnHeader { Text = "Amount", Width = 100 },
            new ColumnHeader { Text = "Net Change", Width = 100 }
        });
        // Dynamic column widths (45%, 25%, 30%)
        void ResizeResourceColumns(object s, EventArgs e)
        {
            int totalWidth = resourcesListView.ClientSize.Width;
            resourcesListView.Columns[0].Width = (int)(totalWidth * 0.45);
            resourcesListView.Columns[1].Width = (int)(totalWidth * 0.25);
            resourcesListView.Columns[2].Width = (int)(totalWidth * 0.30);
        }
        resourcesListView.Resize += ResizeResourceColumns;
        ResizeResourceColumns(null, null);
        resourcesListView.SelectedIndexChanged += ResourcesListView_SelectedIndexChanged;

        // Create resource details panel
        Panel detailsPanel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(10)
        };

        // Create resource details controls
        GroupBox detailsGroupBox = new GroupBox
        {
            Text = "Resource Details",
            Dock = DockStyle.Fill,
            Padding = new Padding(10)
        };

        // 2 columns: label (right), value (left)
        TableLayoutPanel detailsLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 9,
            Padding = new Padding(0),
            Margin = new Padding(0)
        };
        detailsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38F));
        detailsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 62F));
        detailsLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Name
        detailsLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Amount
        detailsLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Net Change
        detailsLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Weekly Production
        detailsLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 40F)); // Production Sources
        detailsLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Weekly Consumption
        detailsLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 40F)); // Consumption Sources
        detailsLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 0F)); // Spacer
        detailsLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Adjust Button

        var boldFont = new Font(SystemFonts.DefaultFont, FontStyle.Bold);
        Label nameLabel = new Label { Text = "Name:", TextAlign = ContentAlignment.MiddleRight, AutoSize = true, Margin = new Padding(0, 2, 0, 0), Font = boldFont };
        Label amountLabel = new Label { Text = "Current Amount:", TextAlign = ContentAlignment.MiddleRight, AutoSize = true, Margin = new Padding(0, 2, 0, 0), Font = boldFont };
        Label netChangeLabel = new Label { Text = "Net Weekly Change:", TextAlign = ContentAlignment.MiddleRight, AutoSize = true, Margin = new Padding(0, 2, 0, 0), Font = boldFont };
        Label productionLabel = new Label { Text = "Weekly Production:", TextAlign = ContentAlignment.MiddleRight, AutoSize = true, Margin = new Padding(0, 2, 0, 0), Font = boldFont };
        Label consumptionLabel = new Label { Text = "Weekly Consumption:", TextAlign = ContentAlignment.MiddleRight, AutoSize = true, Margin = new Padding(0, 2, 0, 0), Font = boldFont };
        // Value labels remain unchanged
        Label nameValueLabel = new Label { Text = "", Tag = "ResourceName", AutoSize = true, Margin = new Padding(0, 2, 0, 0) };
        Label amountValueLabel = new Label { Text = "", Tag = "ResourceAmount", AutoSize = true, Margin = new Padding(0, 2, 0, 0) };
        Label netChangeValueLabel = new Label { Text = "", Tag = "ResourceNetChange", AutoSize = true, Margin = new Padding(0, 2, 0, 0) };
        Label productionValueLabel = new Label { Text = "", Tag = "ResourceProduction", AutoSize = true, Margin = new Padding(0, 2, 0, 0) };
        Label consumptionValueLabel = new Label { Text = "", Tag = "ResourceConsumption", AutoSize = true, Margin = new Padding(0, 2, 0, 0) };

        // Production sources list view
        ListView productionSourcesListView = new ListView
        {
            View = View.Details,
            FullRowSelect = true,
            MultiSelect = false,
            GridLines = true,
            Dock = DockStyle.Fill,
            Tag = "ProductionSourcesListView",
            Margin = new Padding(0, 2, 0, 2)
        };
        productionSourcesListView.Columns.AddRange(new[]
        {
            new ColumnHeader { Text = "Source", Width = 150 },
            new ColumnHeader { Text = "Type", Width = 100 },
            new ColumnHeader { Text = "Amount", Width = 100 }
        });
        void ResizeProductionSourcesColumns(object s, EventArgs e)
        {
            int totalWidth = productionSourcesListView.ClientSize.Width;
            productionSourcesListView.Columns[0].Width = (int)(totalWidth * 0.5);
            productionSourcesListView.Columns[1].Width = (int)(totalWidth * 0.25);
            productionSourcesListView.Columns[2].Width = (int)(totalWidth * 0.25);
        }
        productionSourcesListView.Resize += ResizeProductionSourcesColumns;
        ResizeProductionSourcesColumns(null, null);

        // Consumption sources list view
        ListView consumptionSourcesListView = new ListView
        {
            View = View.Details,
            FullRowSelect = true,
            MultiSelect = false,
            GridLines = true,
            Dock = DockStyle.Fill,
            Tag = "ConsumptionSourcesListView",
            Margin = new Padding(0, 2, 0, 2)
        };
        consumptionSourcesListView.Columns.AddRange(new[]
        {
            new ColumnHeader { Text = "Source", Width = 150 },
            new ColumnHeader { Text = "Type", Width = 100 },
            new ColumnHeader { Text = "Amount", Width = 100 }
        });
        void ResizeConsumptionSourcesColumns(object s, EventArgs e)
        {
            int totalWidth = consumptionSourcesListView.ClientSize.Width;
            consumptionSourcesListView.Columns[0].Width = (int)(totalWidth * 0.5);
            consumptionSourcesListView.Columns[1].Width = (int)(totalWidth * 0.25);
            consumptionSourcesListView.Columns[2].Width = (int)(totalWidth * 0.25);
        }
        consumptionSourcesListView.Resize += ResizeConsumptionSourcesColumns;
        ResizeConsumptionSourcesColumns(null, null);

        Button adjustButton = new Button
        {
            Text = "Adjust Amount",
            Width = 120,
            Height = 38,
            Tag = "AdjustResourceButton",
            Margin = new Padding(0, 8, 0, 0)
        };
        adjustButton.Click += AdjustResourceButton_Click;

        // Add controls to details layout (label, value)
        detailsLayout.Controls.Add(nameLabel, 0, 0);
        detailsLayout.Controls.Add(nameValueLabel, 1, 0);
        detailsLayout.Controls.Add(amountLabel, 0, 1);
        detailsLayout.Controls.Add(amountValueLabel, 1, 1);
        detailsLayout.Controls.Add(netChangeLabel, 0, 2);
        detailsLayout.Controls.Add(netChangeValueLabel, 1, 2);
        detailsLayout.Controls.Add(productionLabel, 0, 3);
        detailsLayout.Controls.Add(productionValueLabel, 1, 3);
        detailsLayout.Controls.Add(productionSourcesListView, 0, 4);
        detailsLayout.SetColumnSpan(productionSourcesListView, 2);
        detailsLayout.Controls.Add(consumptionLabel, 0, 5);
        detailsLayout.Controls.Add(consumptionValueLabel, 1, 5);
        detailsLayout.Controls.Add(consumptionSourcesListView, 0, 6);
        detailsLayout.SetColumnSpan(consumptionSourcesListView, 2);
        detailsLayout.Controls.Add(adjustButton, 1, 8);

        detailsGroupBox.Controls.Add(detailsLayout);
        detailsPanel.Controls.Add(detailsGroupBox);

        // Add controls to main layout
        layout.Controls.Add(resourcesListView, 0, 0);
        layout.Controls.Add(detailsPanel, 1, 0);

        tab.Controls.Add(layout);

        // Populate the resources list directly
        resourcesListView.Items.Clear();
        foreach (var resource in _stronghold.Resources)
        {
            var item = new ListViewItem(resource.Type.ToString());
            item.SubItems.Add(resource.Amount.ToString());
            item.SubItems.Add($"{(resource.NetWeeklyChange >= 0 ? "+" : "")}{resource.NetWeeklyChange}");
            item.Tag = resource;
            resourcesListView.Items.Add(item);
        }
    }

    private void InitializeNPCsTab(TabPage tab)
    {
        // Create main layout
        TableLayoutPanel layout = new TableLayoutPanel();
        layout.Dock = DockStyle.Fill;
        layout.ColumnCount = 2;
        layout.RowCount = 1;
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55F)); // List 55%
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45F)); // Details 45%
        
        // NPCs list view
        ListView npcsListView = new ListView();
        npcsListView.Dock = DockStyle.Fill;
        npcsListView.View = View.Details;
        npcsListView.FullRowSelect = true;
        npcsListView.MultiSelect = false;
        npcsListView.Tag = "NPCsListView";
        
        // Add columns
        npcsListView.Columns.Add("Name", 120);
        npcsListView.Columns.Add("Type", 80);
        npcsListView.Columns.Add("Level", 50);
        npcsListView.Columns.Add("Assignment", 120);
        
        // Dynamic column widths (35%, 20%, 15%, 30%)
        void ResizeNPCsTabColumns(object s, EventArgs e)
        {
            int totalWidth = npcsListView.ClientSize.Width;
            npcsListView.Columns[0].Width = (int)(totalWidth * 0.35);
            npcsListView.Columns[1].Width = (int)(totalWidth * 0.20);
            npcsListView.Columns[2].Width = (int)(totalWidth * 0.15);
            npcsListView.Columns[3].Width = (int)(totalWidth * 0.30);
        }
        npcsListView.Resize += ResizeNPCsTabColumns;
        ResizeNPCsTabColumns(null, null);
        
        // Add items for each NPC
        foreach (var npc in _stronghold.NPCs)
        {
            ListViewItem item = new ListViewItem(npc.Name);
            item.SubItems.Add(npc.Type.ToString());
            item.SubItems.Add(npc.Level.ToString());
            item.SubItems.Add(npc.Assignment.Type == AssignmentType.Unassigned ? "Unassigned" : npc.Assignment.TargetName);
            item.Tag = npc.Id;
            npcsListView.Items.Add(item);
        }
        
        npcsListView.SelectedIndexChanged += NPCsListView_SelectedIndexChanged;
        
        // Details panel
        GroupBox detailsGroupBox = new GroupBox();
        detailsGroupBox.Text = "NPC Details";
        detailsGroupBox.Dock = DockStyle.Fill;
        
        TableLayoutPanel detailsLayout = new TableLayoutPanel();
        detailsLayout.Dock = DockStyle.Fill;
        detailsLayout.ColumnCount = 2;
        detailsLayout.RowCount = 5;
        detailsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35F));
        detailsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 65F));
        
        // Labels for details
        Label nameLabel = new Label { Text = "Name:", TextAlign = ContentAlignment.MiddleRight };
        Label nameValueLabel = new Label { Text = "", Tag = "NPCName" };
        Label typeLabel = new Label { Text = "Type:", TextAlign = ContentAlignment.MiddleRight };
        Label typeValueLabel = new Label { Text = "", Tag = "NPCType" };
        Label levelLabel = new Label { Text = "Level:", TextAlign = ContentAlignment.MiddleRight };
        Label levelValueLabel = new Label { Text = "", Tag = "NPCLevel" };
        Label expLabel = new Label { Text = "Experience:", TextAlign = ContentAlignment.MiddleRight };
        Label expValueLabel = new Label { Text = "", Tag = "NPCExperience" };
        Label happinessLabel = new Label { Text = "Morale:", TextAlign = ContentAlignment.MiddleRight };
        Label happinessValueLabel = new Label { Text = "", Tag = "NPCHappiness" };
        Label skillsLabel = new Label { Text = "Skills:", TextAlign = ContentAlignment.MiddleRight };
        Label skillsValueLabel = new Label { Text = "", Tag = "NPCSkills" };
        Label assignmentLabel = new Label { Text = "Assignment:", TextAlign = ContentAlignment.MiddleRight };
        Label assignmentValueLabel = new Label { Text = "", Tag = "NPCAssignment" };
        
        // Add to details layout
        detailsLayout.Controls.Add(nameLabel, 0, 0);
        detailsLayout.Controls.Add(nameValueLabel, 1, 0);
        detailsLayout.Controls.Add(typeLabel, 0, 1);
        detailsLayout.Controls.Add(typeValueLabel, 1, 1);
        detailsLayout.Controls.Add(levelLabel, 0, 2);
        detailsLayout.Controls.Add(levelValueLabel, 1, 2);
        detailsLayout.Controls.Add(expLabel, 0, 3);
        detailsLayout.Controls.Add(expValueLabel, 1, 3);
        detailsLayout.Controls.Add(happinessLabel, 0, 4);
        detailsLayout.Controls.Add(happinessValueLabel, 1, 4);
        detailsLayout.Controls.Add(skillsLabel, 0, 5);
        detailsLayout.Controls.Add(skillsValueLabel, 1, 5);
        detailsLayout.Controls.Add(assignmentLabel, 0, 6);
        detailsLayout.Controls.Add(assignmentValueLabel, 1, 6);
        
        detailsGroupBox.Controls.Add(detailsLayout);
        
        // Add controls to layout
        layout.Controls.Add(npcsListView, 0, 0);
        layout.Controls.Add(detailsGroupBox, 1, 0);
        
        tab.Controls.Add(layout);
    }

    private void InitializeJournalTab(TabPage tab)
    {
        // To be implemented
        Label placeholder = new Label();
        placeholder.Text = "Journal tab content will be implemented here";
        placeholder.Dock = DockStyle.Fill;
        placeholder.TextAlign = ContentAlignment.MiddleCenter;
        tab.Controls.Add(placeholder);
    }

    private void InitializeMissionsTab(TabPage tab)
    {
        // To be implemented
        Label placeholder = new Label();
        placeholder.Text = "Missions tab content will be implemented here";
        placeholder.Dock = DockStyle.Fill;
        placeholder.TextAlign = ContentAlignment.MiddleCenter;
        tab.Controls.Add(placeholder);
    }

    private void InitializeBuildingsTab(TabPage tab)
    {
        // Create main layout with fixed 65/35 split
        TableLayoutPanel mainLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(10),
            BackColor = SystemColors.Control
        };
        mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58.5F));
        mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 41.5F));

        // Left side panel (contains ListView and Add button)
        TableLayoutPanel leftPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = new Padding(0)
        };
        leftPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F)); // ListView takes all available space
        leftPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F)); // Fixed height for button

        // Create and configure ListView
        ListView buildingsListView = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
            MultiSelect = false,
            Tag = "BuildingsListView"
        };
        buildingsListView.SelectedIndexChanged += BuildingsListView_SelectedIndexChanged;
        buildingsListView.ColumnClick += BuildingsListView_ColumnClick;

        // Add columns
        buildingsListView.Columns.Add("Name", 150);
        buildingsListView.Columns.Add("Type", 100);
        buildingsListView.Columns.Add("Workers", 80);
        buildingsListView.Columns.Add("State", 210);
        buildingsListView.Columns.Add("Condition", 80);
        buildingsListView.Columns.Add("Level", 60);

        // Add items for each building
        foreach (var building in _stronghold.Buildings)
        {
            string stateText = building.ConstructionStatus.ToString();
            if (building.ConstructionStatus == BuildingStatus.UnderConstruction ||
                building.ConstructionStatus == BuildingStatus.Repairing ||
                building.ConstructionStatus == BuildingStatus.Upgrading)
            {
                if (building.AssignedWorkers.Count == 0)
                {
                    stateText += " (No Workers)";
                }
                else
                {
                    stateText += $" ({building.ConstructionProgress}%, {building.ConstructionTimeRemaining}w)";
                }
            }

            ListViewItem item = new ListViewItem(building.Name);
            item.SubItems.Add(building.Type.ToString());
            item.SubItems.Add($"{building.AssignedWorkers.Count}/{building.WorkerSlots}");
            item.SubItems.Add(stateText);
            item.SubItems.Add($"{building.Condition}%");
            item.SubItems.Add(building.Level.ToString());
            item.Tag = building.Id;

            // Set color based on building status
            switch (building.ConstructionStatus)
            {
                case BuildingStatus.Damaged:
                    item.ForeColor = Color.Red;
                    break;
                case BuildingStatus.UnderConstruction:
                    item.ForeColor = Color.Blue;
                    break;
                case BuildingStatus.Repairing:
                    item.ForeColor = Color.Orange;
                    break;
            }

            buildingsListView.Items.Add(item);
        }

        // Create Add New Building button
        Button addBuildingButton = new Button
        {
            Text = "Add New Building",
            Height = 30,
            Width = 120,
            Margin = new Padding(0, 8, 0, 0),
            Anchor = AnchorStyles.Left
        };
        addBuildingButton.Click += AddBuildingButton_Click;

        // Add controls to left panel
        leftPanel.Controls.Add(buildingsListView, 0, 0);
        leftPanel.Controls.Add(addBuildingButton, 0, 1);

        // Right side panel (placeholder for now)
        Panel rightPanel = new Panel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(10, 0, 0, 0),
            BackColor = SystemColors.Control,
            AutoScroll = true
        };

        // Create a container panel for sections
        TableLayoutPanel sectionsPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            RowCount = 2,
            Margin = new Padding(0)
        };

        // Basic Information GroupBox
        GroupBox basicInfoGroup = new GroupBox
        {
            Text = "Basic Information",
            Dock = DockStyle.Top,
            Height = 200,
            Padding = new Padding(10),
            Margin = new Padding(0, 0, 0, 10)
        };

        TableLayoutPanel basicInfoLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 5,
            Padding = new Padding(5)
        };

        // Set column styles
        basicInfoLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30F));  // Labels
        basicInfoLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));  // Values
        basicInfoLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20F));  // Buttons

        // Add rows with equal height
        for (int i = 0; i < 5; i++)
        {
            basicInfoLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 20F));
        }

        // Name row
        Label nameLabel = new Label { Text = "Name:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
        TextBox nameValue = new TextBox { Dock = DockStyle.Fill, ReadOnly = true, Tag = "BuildingNameValue" };
        Button renameButton = new Button { Text = "Rename", Dock = DockStyle.Fill, Tag = "RenameButton" };
        renameButton.Click += RenameBuilding_Click;

        // Type row
        Label typeLabel = new Label { Text = "Type:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
        Label typeValue = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Tag = "BuildingTypeValue" };

        // Level row
        Label levelLabel = new Label { Text = "Level:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
        Label levelValue = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Tag = "BuildingLevelValue" };
        Button upgradeButton = new Button { Text = "Upgrade", Dock = DockStyle.Fill, Tag = "UpgradeButton" };
        upgradeButton.Click += UpgradeBuilding_Click;

        // Status row
        Label statusLabel = new Label { Text = "Status:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
        Label statusValue = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Tag = "BuildingStatusValue" };
        Button cancelButton = new Button { Text = "Cancel", Dock = DockStyle.Fill, Tag = "CancelButton", Visible = false };
        cancelButton.Click += CancelBuilding_Click;

        // Condition row
        Label conditionLabel = new Label { Text = "Condition:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
        Label conditionValue = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Tag = "BuildingConditionValue" };
        Button repairButton = new Button { Text = "Repair", Dock = DockStyle.Fill, Tag = "RepairButton" };
        repairButton.Click += RepairBuilding_Click;

        // Add controls to layout
        basicInfoLayout.Controls.Add(nameLabel, 0, 0);
        basicInfoLayout.Controls.Add(nameValue, 1, 0);
        basicInfoLayout.Controls.Add(renameButton, 2, 0);

        basicInfoLayout.Controls.Add(typeLabel, 0, 1);
        basicInfoLayout.Controls.Add(typeValue, 1, 1);

        basicInfoLayout.Controls.Add(levelLabel, 0, 2);
        basicInfoLayout.Controls.Add(levelValue, 1, 2);
        basicInfoLayout.Controls.Add(upgradeButton, 2, 2);

        basicInfoLayout.Controls.Add(statusLabel, 0, 3);
        basicInfoLayout.Controls.Add(statusValue, 1, 3);
        basicInfoLayout.Controls.Add(cancelButton, 2, 3);

        basicInfoLayout.Controls.Add(conditionLabel, 0, 4);
        basicInfoLayout.Controls.Add(conditionValue, 1, 4);
        basicInfoLayout.Controls.Add(repairButton, 2, 4);

        basicInfoGroup.Controls.Add(basicInfoLayout);
        sectionsPanel.Controls.Add(basicInfoGroup, 0, 0);

        // Production Section
        GroupBox productionGroup = new GroupBox
        {
            Text = "Production",
            Dock = DockStyle.Top,
            Height = 250, // Increased from 150 to show more rows
            Padding = new Padding(10),
            Margin = new Padding(0, 0, 0, 10)
        };

        // Create a header panel for the title and collapse button
        Panel headerPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 35, // Increased from 30 to fully show the button
            Padding = new Padding(0)
        };

        // Add production summary label
        Label productionSummaryLabel = new Label
        {
            AutoSize = false,
            Height = 24,
            Width = headerPanel.Width - 40, // Leave space for the button
            TextAlign = ContentAlignment.MiddleLeft,
            Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right,
            Tag = "ProductionSummaryLabel"
        };
        headerPanel.Resize += (s, e) => productionSummaryLabel.Width = headerPanel.Width - 40;

        // Add collapse/expand button
        Button collapseButton = new Button
        {
            Text = "▼",
            Width = 24,
            Height = 24,
            FlatStyle = FlatStyle.Flat,
            Margin = new Padding(0),
            Tag = "ProductionCollapseButton",
            Anchor = AnchorStyles.Right | AnchorStyles.Top
        };
        collapseButton.Location = new Point(headerPanel.Width - collapseButton.Width - 10, 5); // Added vertical offset
        headerPanel.Resize += (s, e) => collapseButton.Location = new Point(headerPanel.Width - collapseButton.Width - 10, 5);

        // Create collapsible content panel
        Panel contentPanel = new Panel
        {
            Dock = DockStyle.Fill,
            Height = 200 // Increased to show more rows
        };

        // Create production list view
        ListView productionListView = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
            Tag = "ProductionListView"
        };

        // Add columns in new order with new widths
        productionListView.Columns.Add("Resource", 120);
        productionListView.Columns.Add("Workers", 120);
        productionListView.Columns.Add("Total", 70);
        productionListView.Columns.Add("Base", 60);
        productionListView.Columns.Add("Bonus", 250);

        // Dynamic column widths (20%, 20%, 10%, 10%, 40%)
        void ResizeProductionColumns(object s, EventArgs e)
        {
            int totalWidth = productionListView.ClientSize.Width;
            productionListView.Columns[0].Width = (int)(totalWidth * 0.17);
            productionListView.Columns[1].Width = (int)(totalWidth * 0.25);
            productionListView.Columns[2].Width = (int)(totalWidth * 0.11);
            productionListView.Columns[3].Width = (int)(totalWidth * 0.10);
            productionListView.Columns[4].Width = (int)(totalWidth * 0.70);
        }
        productionListView.Resize += ResizeProductionColumns;
        ResizeProductionColumns(null, null);

        // Add double-click handler for resource and worker navigation
        productionListView.DoubleClick += (s, e) =>
        {
            var listView = (ListView)s;
            if (listView.SelectedItems.Count == 0) return;

            var selectedItem = listView.SelectedItems[0];
            
            // Skip empty rows (used for spacing)
            if (selectedItem.SubItems.Cast<ListViewItem.ListViewSubItem>()
                .All(subItem => string.IsNullOrWhiteSpace(subItem.Text)))
            {
                return;
            }

            try
            {
                // If this is a resource header row (has all columns filled and first column is a valid resource type)
                if (!string.IsNullOrWhiteSpace(selectedItem.SubItems[0].Text) && 
                    !string.IsNullOrWhiteSpace(selectedItem.SubItems[1].Text) &&
                    !string.IsNullOrWhiteSpace(selectedItem.SubItems[2].Text) &&
                    Enum.TryParse<ResourceType>(selectedItem.SubItems[0].Text, out var resourceType))
                {
                    ShowResourceInTab(resourceType);
                }
                // If this is a worker row (second column has worker name)
                else if (selectedItem.SubItems.Count > 1 && 
                       !string.IsNullOrWhiteSpace(selectedItem.SubItems[1].Text))
                {
                    var workerName = selectedItem.SubItems[1].Text.Trim();
                    
                    // Get the building and its assigned workers to ensure we get the right NPC
                    var building = _stronghold.Buildings.Find(b => b.Id == _selectedBuildingId);
                    if (building != null)
                    {
                        // Find all NPCs with this name that are assigned to this building
                        var matchingNPCs = _stronghold.NPCs
                            .Where(n => n.Name == workerName && 
                                   building.AssignedWorkers.Contains(n.Id))
                            .ToList();

                        if (matchingNPCs.Count == 1)
                        {
                            // If exactly one match, show that NPC
                            ShowNPCInTab(matchingNPCs[0].Id);
                        }
                        else if (matchingNPCs.Count > 1)
                        {
                            // If multiple matches, show a selection dialog
                            using (var dialog = new SelectNPCDialog(matchingNPCs))
                            {
                                if (dialog.ShowDialog() == DialogResult.OK && dialog.SelectedNPC != null)
                                {
                                    ShowNPCInTab(dialog.SelectedNPC.Id);
                                }
                            }
                        }
                        else
                        {
                            MessageBox.Show(
                                "Could not find the worker in the stronghold's records.",
                                "Worker Not Found",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Warning);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"An error occurred while trying to show the details: {ex.Message}",
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        };

        // Start collapsed
        contentPanel.Visible = false;
        productionGroup.Height = 70;
        collapseButton.Text = "▶";

        collapseButton.Click += (s, e) =>
        {
            Button btn = (Button)s;
            if (contentPanel.Visible)
            {
                contentPanel.Visible = false;
                productionGroup.Height = 70;
                btn.Text = "▶";
            }
            else
            {
                contentPanel.Visible = true;
                productionGroup.Height = 250;
                btn.Text = "▼";
            }
        };

        // Add controls to panels
        contentPanel.Controls.Add(productionListView);
        headerPanel.Controls.Add(productionSummaryLabel);
        headerPanel.Controls.Add(collapseButton);
        productionGroup.Controls.Add(contentPanel);
        productionGroup.Controls.Add(headerPanel);

        // Add groups to sections panel in order
        sectionsPanel.Controls.Add(basicInfoGroup, 0, 0);
        sectionsPanel.Controls.Add(productionGroup, 0, 1);

        // Add sections panel to right panel
        rightPanel.Controls.Add(sectionsPanel);

        // Add panels to main layout
        mainLayout.Controls.Add(leftPanel, 0, 0);
        mainLayout.Controls.Add(rightPanel, 1, 0);

        // Upkeep Section
        GroupBox upkeepGroup = new GroupBox
        {
            Text = "Upkeep",
            Dock = DockStyle.Top,
            Height = 250,
            Padding = new Padding(10),
            Margin = new Padding(0, 0, 0, 10)
        };

        // Create a header panel for the title and collapse button
        Panel upkeepHeaderPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 35,
            Padding = new Padding(0)
        };

        // Add upkeep summary label
        Label upkeepSummaryLabel = new Label
        {
            AutoSize = false,
            Height = 24,
            Width = upkeepHeaderPanel.Width - 40,
            TextAlign = ContentAlignment.MiddleLeft,
            Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right,
            Tag = "UpkeepSummaryLabel"
        };
        upkeepHeaderPanel.Resize += (s, e) => upkeepSummaryLabel.Width = upkeepHeaderPanel.Width - 40;

        // Add collapse/expand button
        Button upkeepCollapseButton = new Button
        {
            Text = "▶",
            Width = 24,
            Height = 24,
            FlatStyle = FlatStyle.Flat,
            Margin = new Padding(0),
            Tag = "UpkeepCollapseButton",
            Anchor = AnchorStyles.Right | AnchorStyles.Top
        };
        upkeepCollapseButton.Location = new Point(upkeepHeaderPanel.Width - upkeepCollapseButton.Width - 10, 5);
        upkeepHeaderPanel.Resize += (s, e) => upkeepCollapseButton.Location = new Point(upkeepHeaderPanel.Width - upkeepCollapseButton.Width - 10, 5);

        // Create collapsible content panel
        Panel upkeepContentPanel = new Panel
        {
            Dock = DockStyle.Fill,
            Height = 200
        };

        // Create upkeep list view
        ListView upkeepListView = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
            Tag = "UpkeepListView"
        };

        // Add columns with correct names and initial widths
        upkeepListView.Columns.Add("Resource", 120);
        upkeepListView.Columns.Add("Source", 120);
        upkeepListView.Columns.Add("Amt", 70);
        upkeepListView.Columns.Add("Info", 250);

        // Dynamic column widths
        void ResizeUpkeepColumns(object s, EventArgs e)
        {
            int totalWidth = upkeepListView.ClientSize.Width;
            upkeepListView.Columns[0].Width = (int)(totalWidth * 0.12);
            upkeepListView.Columns[1].Width = (int)(totalWidth * 0.25);
            upkeepListView.Columns[2].Width = (int)(totalWidth * 0.10);
            upkeepListView.Columns[3].Width = (int)(totalWidth * 0.70);
        }
        upkeepListView.Resize += ResizeUpkeepColumns;
        ResizeUpkeepColumns(null, null);

        // Add double-click handler for resource navigation
        upkeepListView.DoubleClick += (s, e) =>
        {
            var listView = (ListView)s;
            if (listView.SelectedItems.Count == 0) return;

            var selectedItem = listView.SelectedItems[0];
            
            // Skip empty rows (used for spacing)
            if (selectedItem.SubItems.Cast<ListViewItem.ListViewSubItem>()
                .All(subItem => string.IsNullOrWhiteSpace(subItem.Text)))
            {
                return;
            }

            try
            {
                // If this is a resource header row (has all columns filled and first column is a valid resource type)
                if (!string.IsNullOrWhiteSpace(selectedItem.SubItems[0].Text) && 
                    !string.IsNullOrWhiteSpace(selectedItem.SubItems[1].Text) &&
                    !string.IsNullOrWhiteSpace(selectedItem.SubItems[2].Text) &&
                    Enum.TryParse<ResourceType>(selectedItem.SubItems[0].Text, out var resourceType))
                {
                    ShowResourceInTab(resourceType);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"An error occurred while trying to show the resource details: {ex.Message}",
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        };

        // Start collapsed
        upkeepContentPanel.Visible = false;
        upkeepGroup.Height = 70;

        upkeepCollapseButton.Click += (s, e) =>
        {
            Button btn = (Button)s;
            if (upkeepContentPanel.Visible)
            {
                upkeepContentPanel.Visible = false;
                upkeepGroup.Height = 70;
                btn.Text = "▶";
            }
            else
            {
                upkeepContentPanel.Visible = true;
                upkeepGroup.Height = 250;
                btn.Text = "▼";
            }
        };

        // Add controls to panels
        upkeepContentPanel.Controls.Add(upkeepListView);
        upkeepHeaderPanel.Controls.Add(upkeepSummaryLabel);
        upkeepHeaderPanel.Controls.Add(upkeepCollapseButton);
        upkeepGroup.Controls.Add(upkeepContentPanel);
        upkeepGroup.Controls.Add(upkeepHeaderPanel);

        // Add groups to sections panel in order
        sectionsPanel.Controls.Add(basicInfoGroup, 0, 0);
        sectionsPanel.Controls.Add(productionGroup, 0, 1);
        sectionsPanel.Controls.Add(upkeepGroup, 0, 2);

        tab.Controls.Add(mainLayout);
    }

    #endregion

    #region Event Handlers

    private void NewStrongholdItem_Click(object sender, EventArgs e)
    {
        // Show stronghold setup dialog
        using (var setupDialog = new StrongholdSetupDialog())
        {
            if (setupDialog.ShowDialog() == DialogResult.OK)
            {
                // Create new stronghold with the specified settings
                _gameStateService.CreateNewStronghold(
                    setupDialog.StrongholdName,
                    setupDialog.StrongholdLocation,
                    setupDialog.Buildings,
                    setupDialog.NPCs,
                    setupDialog.StartingResources
                );
            }
        }
    }

    private void NextTurnButton_Click(object sender, EventArgs e)
    {
        // Advance the game by one turn
        _gameStateService.AdvanceWeek();
        
        // UI will be updated via the GameStateChanged event
    }

    private void SaveButton_Click(object sender, EventArgs e)
    {
        // Show save file dialog
        SaveFileDialog saveFileDialog = new SaveFileDialog();
        saveFileDialog.Filter = "Stronghold Save Files (*.stronghold)|*.stronghold|All Files (*.*)|*.*";
        saveFileDialog.Title = "Save Stronghold";
        saveFileDialog.DefaultExt = "stronghold";
        
        if (saveFileDialog.ShowDialog() == DialogResult.OK)
        {
            _gameStateService.SaveGame(saveFileDialog.FileName);
        }
    }

    private void LoadButton_Click(object sender, EventArgs e)
    {
        // Show open file dialog
        OpenFileDialog openFileDialog = new OpenFileDialog();
        openFileDialog.Filter = "Stronghold Save Files (*.stronghold)|*.stronghold|All Files (*.*)|*.*";
        openFileDialog.Title = "Load Stronghold";
        
        if (openFileDialog.ShowDialog() == DialogResult.OK)
        {
            _gameStateService.LoadGame(openFileDialog.FileName);
        }
    }

    private void GameStateService_GameStateChanged(object sender, EventArgs e)
    {
        // Update UI when game state changes
        UpdateStatusBar();
        RefreshAllTabs();
    }

    private void UpdateStatusBar()
    {
        _weekLabel.Text = $"Week: {_stronghold.CurrentWeek}";
        _yearLabel.Text = $"Year: {_stronghold.YearsSinceFoundation}";
        _seasonLabel.Text = $"Season: {_stronghold.CurrentSeason}";
        _goldLabel.Text = $"Gold: {_stronghold.Treasury}";
    }

    private void RefreshAllTabs()
    {
        RefreshDashboardTab();
        RefreshBuildingsTab();
        RefreshNPCsTab();
        RefreshResourcesTab();
        RefreshJournalTab();
        RefreshMissionsTab();
    }

    private void RefreshDashboardTab()
    {
        // Update Stronghold Info Panel
        var strongholdInfoPanel = FindControl<GroupBox>(_tabControl.TabPages[0], "StrongholdInfoPanel");
        if (strongholdInfoPanel != null)
        {
            var nameLabel = FindControl<Label>(strongholdInfoPanel, "StrongholdName");
            var locationLabel = FindControl<Label>(strongholdInfoPanel, "StrongholdLocation");
            var levelLabel = FindControl<Label>(strongholdInfoPanel, "StrongholdLevel");
            
            if (nameLabel != null) nameLabel.Text = _stronghold.Name;
            if (locationLabel != null) locationLabel.Text = _stronghold.Location;
            if (levelLabel != null) levelLabel.Text = _stronghold.Level.ToString();
        }

        // Update Building Summary Panel
        var buildingSummaryPanel = FindControl<GroupBox>(_tabControl.TabPages[0], "BuildingSummaryPanel");
        if (buildingSummaryPanel != null)
        {
            var listView = FindControl<ListView>(buildingSummaryPanel, "BuildingSummaryList");
            if (listView != null)
            {
                listView.Items.Clear();
                foreach (var building in _stronghold.Buildings)
                {
                    string statusText = building.ConstructionStatus.ToString();
                    if (building.ConstructionStatus == BuildingStatus.UnderConstruction ||
                        building.ConstructionStatus == BuildingStatus.Repairing ||
                        building.ConstructionStatus == BuildingStatus.Upgrading)
                    {
                        statusText += $" ({building.ConstructionProgress}%)";
                    }
                    ListViewItem item = new ListViewItem(building.Name);
                    item.SubItems.Add(building.Type.ToString());
                    item.SubItems.Add(statusText);
                    
                    if (building.ConstructionStatus == BuildingStatus.Damaged)
                        item.ForeColor = Color.Red;
                    else if (building.ConstructionStatus == BuildingStatus.UnderConstruction)
                        item.ForeColor = Color.Blue;
                    else if (building.ConstructionStatus == BuildingStatus.Repairing)
                        item.ForeColor = Color.Orange;
                    
                    listView.Items.Add(item);
                }
            }
        }

        // Update Resource Summary Panel
        var resourceSummaryPanel = FindControl<GroupBox>(_tabControl.TabPages[0], "ResourceSummaryPanel");
        if (resourceSummaryPanel != null)
        {
            var listView = FindControl<ListView>(resourceSummaryPanel, "ResourceSummaryList");
            if (listView != null)
            {
                listView.Items.Clear();
                foreach (var resource in _stronghold.Resources)
                {
                    ListViewItem item = new ListViewItem(resource.Type.ToString());
                    item.SubItems.Add(resource.Amount.ToString());
                    item.SubItems.Add(resource.NetWeeklyChange.ToString());
                    listView.Items.Add(item);
                }
            }
        }

        // Update NPC Summary Panel
        var npcSummaryPanel = FindControl<GroupBox>(_tabControl.TabPages[0], "NPCSummaryPanel");
        if (npcSummaryPanel != null)
        {
            var listView = FindControl<ListView>(npcSummaryPanel, "NPCSummaryList");
            if (listView != null)
            {
                listView.Items.Clear();
                foreach (var npc in _stronghold.NPCs)
                {
                    ListViewItem item = new ListViewItem(npc.Name);
                    item.SubItems.Add(npc.Type.ToString());
                    item.SubItems.Add(npc.Assignment.Type == AssignmentType.Unassigned ? "Unassigned" : npc.Assignment.TargetName);
                    string status = npc.States != null && npc.States.Any()
                        ? string.Join(", ", npc.States.Select(s => s.Type.ToString()))
                        : "Healthy";
                    item.SubItems.Add(status);
                    item.Tag = npc.Id;
                    listView.Items.Add(item);
                }
            }
        }

        // Update Recent Events Panel
        var recentEventsPanel = FindControl<GroupBox>(_tabControl.TabPages[0], "RecentEventsPanel");
        if (recentEventsPanel != null)
        {
            var listView = FindControl<ListView>(recentEventsPanel, "RecentEventsList");
            if (listView != null)
            {
                listView.Items.Clear();
                foreach (var entry in _stronghold.Journal)
                {
                    ListViewItem item = new ListViewItem(entry.Date);
                    item.SubItems.Add(entry.Title);
                    listView.Items.Add(item);
                }
            }
        }
    }

    private void RefreshBuildingsTab()
    {
        var buildingsListView = FindControl<ListView>(_tabControl.TabPages[1], "BuildingsListView");
        if (buildingsListView == null) return;

        buildingsListView.Items.Clear();

        foreach (var building in _stronghold.Buildings)
        {
            string stateText = building.ConstructionStatus.ToString();
            if (building.ConstructionStatus == BuildingStatus.UnderConstruction ||
                building.ConstructionStatus == BuildingStatus.Repairing ||
                building.ConstructionStatus == BuildingStatus.Upgrading)
            {
                if (building.AssignedWorkers.Count == 0)
                {
                    stateText += " (No Workers)";
                }
                else
                {
                    stateText += $" ({building.ConstructionProgress}%, {building.ConstructionTimeRemaining}w)";
                }
            }

            ListViewItem item = new ListViewItem(building.Name);
            item.SubItems.Add(building.Type.ToString());
            item.SubItems.Add($"{building.AssignedWorkers.Count}/{building.WorkerSlots}");
            item.SubItems.Add(stateText);
            item.SubItems.Add($"{building.Condition}%");
            item.SubItems.Add(building.Level.ToString());
            item.Tag = building.Id;

            // Set color based on building status
            switch (building.ConstructionStatus)
            {
                case BuildingStatus.Damaged:
                    item.ForeColor = Color.Red;
                    break;
                case BuildingStatus.UnderConstruction:
                    item.ForeColor = Color.Blue;
                    break;
                case BuildingStatus.Repairing:
                    item.ForeColor = Color.Orange;
                    break;
            }

            buildingsListView.Items.Add(item);
        }

        // Maintain sorting if a column was previously sorted
        if (_lastSortedColumn != -1)
        {
            buildingsListView.ListViewItemSorter = new BuildingsListViewSorter(_lastSortedColumn, _lastSortOrder);
        }
    }

    private void RefreshNPCsTab()
    {
        // Store selected NPC ID (if any)
        string selectedNpcId = null;
        var npcsListView = FindControl<ListView>(_tabControl.TabPages[2], "NPCsListView");
        if (npcsListView != null && npcsListView.SelectedItems.Count > 0)
        {
            selectedNpcId = npcsListView.SelectedItems[0].Tag as string;
        }

        // Refresh the ListView
        if (npcsListView != null)
        {
            npcsListView.Items.Clear();
            foreach (var npc in _stronghold.NPCs)
            {
                ListViewItem item = new ListViewItem(npc.Name);
                item.SubItems.Add(npc.Type.ToString());
                item.SubItems.Add(npc.Level.ToString());
                item.SubItems.Add(npc.Assignment.Type == AssignmentType.Unassigned ? "Unassigned" : npc.Assignment.TargetName);
                item.Tag = npc.Id;
                npcsListView.Items.Add(item);
            }

            // Restore selected NPC if it still exists
            if (selectedNpcId != null)
            {
                foreach (ListViewItem item in npcsListView.Items)
                {
                    if ((string)item.Tag == selectedNpcId)
                    {
                        item.Selected = true;
                        item.Focused = true;
                        npcsListView.Select();
                        npcsListView.EnsureVisible(item.Index);
                        break;
                    }
                }
            }
            // Or select newly created NPC if applicable
            else if (_lastCreatedNpcId != null)
            {
                foreach (ListViewItem item in npcsListView.Items)
                {
                    if ((string)item.Tag == _lastCreatedNpcId)
                    {
                        item.Selected = true;
                        item.Focused = true;
                        npcsListView.Select();
                        npcsListView.EnsureVisible(item.Index);
                        break;
                    }
                }
                _lastCreatedNpcId = null;
            }
        }
    }

    private void RefreshResourcesTab()
    {
        var resourcesListView = FindControl<ListView>(_tabControl.TabPages[3], "ResourcesListView");
        if (resourcesListView == null) return;

        // Store selected resource
        Resource selectedResource = null;
        if (resourcesListView.SelectedItems.Count > 0)
        {
            selectedResource = (Resource)resourcesListView.SelectedItems[0].Tag;
        }

        // Refresh list
        resourcesListView.Items.Clear();
        foreach (var resource in _stronghold.Resources)
        {
            var item = new ListViewItem(resource.Type.ToString());
            item.SubItems.Add(resource.Amount.ToString());
            item.SubItems.Add($"{(resource.NetWeeklyChange >= 0 ? "+" : "")}{resource.NetWeeklyChange}");
            item.Tag = resource;
            resourcesListView.Items.Add(item);

            // Restore selection if this was the previously selected resource
            if (selectedResource != null && resource.Type == selectedResource.Type)
            {
                item.Selected = true;
                resourcesListView.EnsureVisible(item.Index);
            }
        }
    }

    private void RefreshJournalTab()
    {
        var journalListView = FindControl<ListView>(_tabControl.TabPages[4], "JournalListView");
        if (journalListView == null) return;

        journalListView.Items.Clear();
        foreach (var entry in _stronghold.Journal)
        {
            ListViewItem item = new ListViewItem(entry.Date);
            item.SubItems.Add(entry.Title);
            item.SubItems.Add(entry.Description);
            item.Tag = entry;
            journalListView.Items.Add(item);
        }
    }

    private void RefreshMissionsTab()
    {
        var missionsListView = FindControl<ListView>(_tabControl.TabPages[5], "MissionsListView");
        if (missionsListView == null) return;

        missionsListView.Items.Clear();
        foreach (var mission in _stronghold.ActiveMissions)
        {
            ListViewItem item = new ListViewItem(mission.Name);
            item.SubItems.Add(mission.Status.ToString());
            item.SubItems.Add($"{mission.WeeksRemaining} weeks");
            item.Tag = mission;
            missionsListView.Items.Add(item);
        }
    }

    #region NPCs Tab Event Handlers

    private void NPCsListView_SelectedIndexChanged(object sender, EventArgs e)
    {
        ListView listView = (ListView)sender;
        if (listView.SelectedItems.Count > 0)
        {
            string npcId = (string)listView.SelectedItems[0].Tag;
            NPC selectedNpc = _stronghold.NPCs.Find(n => n.Id == npcId);
            if (selectedNpc != null)
            {
                UpdateNPCDetails(selectedNpc);
            }
        }
    }

    private void UpdateNPCDetails(NPC npc)
    {
        if (npc == null) return;

        var detailsGroupBox = new GroupBox
        {
            Text = "NPC Details",
            Dock = DockStyle.Fill
        };

        var detailsLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 5,
            Padding = new Padding(10)
        };

        // Name
        Label nameLabel = new Label { Text = "Name:", TextAlign = ContentAlignment.MiddleRight };
        Label nameValueLabel = new Label { Text = npc.Name };
        detailsLayout.Controls.Add(nameLabel, 0, 0);
        detailsLayout.Controls.Add(nameValueLabel, 1, 0);

        // Type
        Label typeLabel = new Label { Text = "Type:", TextAlign = ContentAlignment.MiddleRight };
        Label typeValueLabel = new Label { Text = npc.Type.ToString() };
        detailsLayout.Controls.Add(typeLabel, 0, 1);
        detailsLayout.Controls.Add(typeValueLabel, 1, 1);

        // Skills
        Label skillsLabel = new Label { Text = "Skills:", TextAlign = ContentAlignment.MiddleRight };
        Label skillsValueLabel = new Label { Text = string.Join(", ", npc.Skills.Select(s => $"{s.Name} (Lvl {s.Level})")) };
        detailsLayout.Controls.Add(skillsLabel, 0, 2);
        detailsLayout.Controls.Add(skillsValueLabel, 1, 2);

        // Assignment
        Label assignmentLabel = new Label { Text = "Assignment:", TextAlign = ContentAlignment.MiddleRight };
        Label assignmentValueLabel = new Label { Text = npc.Assignment.Type == AssignmentType.Unassigned ? "None" : $"{npc.Assignment.Type}: {npc.Assignment.TargetName}" };
        detailsLayout.Controls.Add(assignmentLabel, 0, 3);
        detailsLayout.Controls.Add(assignmentValueLabel, 1, 3);

        // Health States
        Label statesLabel = new Label { Text = "Status:", TextAlign = ContentAlignment.MiddleRight };
        Label statesValueLabel = new Label { Text = npc.States.Any() ? string.Join(", ", npc.States.Select(s => s.Type.ToString())) : "Healthy" };
        detailsLayout.Controls.Add(statesLabel, 0, 4);
        detailsLayout.Controls.Add(statesValueLabel, 1, 4);

        // Clear and add the new layout
        detailsGroupBox.Controls.Clear();
        detailsGroupBox.Controls.Add(detailsLayout);
    }

    // Helper to switch to NPCs tab and select an NPC
    private void ShowNPCInTab(string npcId)
    {
        // Find the NPCs tab
        for (int i = 0; i < _tabControl.TabPages.Count; i++)
        {
            if (_tabControl.TabPages[i].Text == "NPCs")
            {
                _tabControl.SelectedIndex = i;
                // Find the NPCs list view
                ListView npcsListView = FindControl<ListView>(_tabControl.TabPages[i], "NPCsListView");
                if (npcsListView != null)
                {
                    foreach (ListViewItem item in npcsListView.Items)
                    {
                        if ((string)item.Tag == npcId)
                        {
                            item.Selected = true;
                            item.Focused = true;
                            npcsListView.Select();
                            npcsListView.EnsureVisible(item.Index);
                            break;
                        }
                    }
                }
                break;
            }
        }
    }

    // For now, add to NewStrongholdItem_Click for initial NPCs, and expose a method for future use
    public void NotifyNpcCreated(string npcId)
    {
        _lastCreatedNpcId = npcId;
    }

    private void ResourcesListView_SelectedIndexChanged(object sender, EventArgs e)
    {
        ListView listView = (ListView)sender;
        if (listView.SelectedItems.Count == 0) return;

        Resource selectedResource = (Resource)listView.SelectedItems[0].Tag;
        if (selectedResource == null) return;

        // Update resource details
        Label nameValueLabel = FindControl<Label>(listView.Parent, "ResourceName");
        Label amountValueLabel = FindControl<Label>(listView.Parent, "ResourceAmount");
        Label productionValueLabel = FindControl<Label>(listView.Parent, "ResourceProduction");
        Label consumptionValueLabel = FindControl<Label>(listView.Parent, "ResourceConsumption");
        Label netChangeValueLabel = FindControl<Label>(listView.Parent, "ResourceNetChange");
        ListView productionSourcesListView = FindControl<ListView>(listView.Parent, "ProductionSourcesListView");
        ListView consumptionSourcesListView = FindControl<ListView>(listView.Parent, "ConsumptionSourcesListView");

        if (nameValueLabel != null) nameValueLabel.Text = selectedResource.Type.ToString();
        if (amountValueLabel != null) amountValueLabel.Text = selectedResource.Amount.ToString();
        if (productionValueLabel != null) productionValueLabel.Text = $"+{selectedResource.WeeklyProduction} per week";
        if (consumptionValueLabel != null) consumptionValueLabel.Text = $"-{selectedResource.WeeklyConsumption} per week";
        if (netChangeValueLabel != null)
        {
            int netChange = selectedResource.NetWeeklyChange;
            netChangeValueLabel.Text = $"{(netChange >= 0 ? "+" : "")}{netChange} per week";
            netChangeValueLabel.ForeColor = netChange >= 0 ? Color.Green : Color.Red;
        }

        // Update production sources list
        if (productionSourcesListView != null)
        {
            productionSourcesListView.Items.Clear();
            var prodSources = selectedResource.Sources.Where(s => s.IsProduction).ToList();
            if (prodSources.Count == 0)
            {
                var placeholder = new ListViewItem("No production sources");
                placeholder.SubItems.Add("");
                placeholder.SubItems.Add("");
                placeholder.ForeColor = Color.Gray;
                productionSourcesListView.Items.Add(placeholder);
            }
            else
            {
                foreach (var source in prodSources)
                {
                    var item = new ListViewItem(source.SourceName);
                    item.SubItems.Add(source.SourceType.ToString());
                    item.SubItems.Add($"+{source.Amount}");
                    productionSourcesListView.Items.Add(item);
                }
            }
        }
        // Update consumption sources list
        if (consumptionSourcesListView != null)
        {
            consumptionSourcesListView.Items.Clear();
            var consSources = selectedResource.Sources.Where(s => !s.IsProduction).ToList();
            if (consSources.Count == 0)
            {
                var placeholder = new ListViewItem("No consumption sources");
                placeholder.SubItems.Add("");
                placeholder.SubItems.Add("");
                placeholder.ForeColor = Color.Gray;
                consumptionSourcesListView.Items.Add(placeholder);
            }
            else
            {
                foreach (var source in consSources)
                {
                    var item = new ListViewItem(source.SourceName);
                    item.SubItems.Add(source.SourceType.ToString());
                    item.SubItems.Add($"-{source.Amount}");
                    consumptionSourcesListView.Items.Add(item);
                }
            }
        }
    }

    private void AdjustResourceButton_Click(object sender, EventArgs e)
    {
        ListView resourcesListView = FindControl<ListView>(((Button)sender).Parent.Parent.Parent, "ResourcesListView");
        if (resourcesListView == null || resourcesListView.SelectedItems.Count == 0) return;

        Resource selectedResource = (Resource)resourcesListView.SelectedItems[0].Tag;
        if (selectedResource == null) return;

        using (var dialog = new NumericInputDialog(
            $"Adjust {selectedResource.Type} Amount",
            "New Amount:",
            0,
            10000,
            selectedResource.Amount))
        {
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                selectedResource.Amount = dialog.Value;
                RefreshResourcesList();
            }
        }
    }

    private void RefreshResourcesList()
    {
        ListView resourcesListView = FindControl<ListView>(_tabControl.TabPages[3], "ResourcesListView");
        if (resourcesListView == null) return;

        resourcesListView.Items.Clear();
        foreach (var resource in _stronghold.Resources)
        {
            var item = new ListViewItem(resource.Type.ToString());
            item.SubItems.Add(resource.Amount.ToString());
            item.SubItems.Add($"{(resource.NetWeeklyChange >= 0 ? "+" : "")}{resource.NetWeeklyChange}");
            item.Tag = resource;
            resourcesListView.Items.Add(item);
        }
    }

    private void AddBuildingButton_Click(object sender, EventArgs e)
    {
        using (var addBuildingForm = new AddBuildingForm(_stronghold))
        {
            if (addBuildingForm.ShowDialog() == DialogResult.OK)
            {
                // The building has been added through GameStateService
                // which will trigger GameStateChanged event and refresh the UI
                // No need to manually update the ListView here
            }
        }
    }

    private void BuildingsListView_SelectedIndexChanged(object sender, EventArgs e)
    {
        ListView listView = (ListView)sender;
        if (listView.SelectedItems.Count > 0)
        {
            string buildingId = (string)listView.SelectedItems[0].Tag;
            _selectedBuildingId = buildingId;
            
            // Find the selected building
            var building = _stronghold.Buildings.Find(b => b.Id == buildingId);
            if (building == null) return;

            // Get assigned NPCs for production calculation
            var assignedNPCs = building.AssignedWorkers
                .Select(workerId => _stronghold.NPCs.Find(n => n.Id == workerId))
                .Where(npc => npc != null)
                .ToList();

            // Update building's production based on current workers
            building.UpdateProduction(assignedNPCs);

            // Update production list view with detailed breakdown
            var productionListView = FindControl<ListView>(_tabControl.TabPages[1], "ProductionListView");
            if (productionListView != null)
            {
                productionListView.Items.Clear();
                if (building.IsFunctional())
                {
                    // Get building info for bonus calculations
                    string jsonPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "BuildingData.json");
                    if (File.Exists(jsonPath))
                    {
                        string json = File.ReadAllText(jsonPath);
                        var buildingData = System.Text.Json.JsonSerializer.Deserialize<BuildingData>(json);
                        var buildingInfo = buildingData.buildings.Find(b => b.type == building.Type.ToString());
                        
                        if (buildingInfo != null)
                        {
                            var prodAtLevel = buildingInfo.productionScaling.FirstOrDefault(p => p.level == building.Level);
                            if (prodAtLevel != null)
                            {
                                // For each resource type produced
                                foreach (var resource in prodAtLevel.resources)
                                {
                                    var resourceType = (ResourceType)Enum.Parse(typeof(ResourceType), resource.resourceType);
                                    var applicableBonuses = buildingInfo.workerProductionBonus
                                        .Where(b => b.resourceType == resource.resourceType);

                                    // Add a header row for this resource
                                    var headerItem = new ListViewItem(resourceType.ToString());
                                    var availableWorkers = assignedNPCs.Where(w => 
                                        building.CurrentProject == null || !building.CurrentProject.AssignedWorkers.Contains(w.Id)).ToList();
                                    headerItem.SubItems.Add(availableWorkers.Count.ToString());
                                    
                                    // Calculate totals
                                    decimal totalBase = resource.perWorkerValue * availableWorkers.Count;
                                    decimal totalBonus = 0m;
                                    foreach (var worker in availableWorkers)
                                    {
                                        foreach (var bonus in applicableBonuses)
                                        {
                                            var skill = worker.Skills.Find(s => s.Name == bonus.skill);
                                            if (skill != null)
                                            {
                                                totalBonus += skill.Level * bonus.bonusValue;
                                            }
                                        }
                                    }
                                    decimal total = totalBase + totalBonus;
                                    
                                    headerItem.SubItems.Add(((int)total).ToString());
                                    headerItem.SubItems.Add(totalBase.ToString("0.#"));
                                    headerItem.SubItems.Add(totalBonus.ToString("0.#"));
                                    
                                    headerItem.BackColor = Color.LightGray;
                                    headerItem.Font = new Font(productionListView.Font, FontStyle.Bold);
                                    productionListView.Items.Add(headerItem);

                                    // For each worker
                                    foreach (var worker in assignedNPCs)
                                    {
                                        // Skip workers assigned to projects
                                        if (building.CurrentProject?.AssignedWorkers.Contains(worker.Id) ?? false)
                                            continue;

                                        decimal baseProduction = resource.perWorkerValue;
                                        decimal bonusProduction = 0m;
                                        string bonusBreakdown = "";

                                        // Calculate bonuses from skills
                                        foreach (var bonus in applicableBonuses)
                                        {
                                            var skill = worker.Skills.Find(s => s.Name == bonus.skill);
                                            if (skill != null)
                                            {
                                                decimal skillBonus = skill.Level * bonus.bonusValue;
                                                bonusProduction += skillBonus;
                                                bonusBreakdown += $"{bonus.skill}({skill.Level}): +{skillBonus:0.#}, ";
                                            }
                                        }

                                        decimal totalWorkerProduction = baseProduction + bonusProduction;

                                        // Add worker row
                                        var workerItem = new ListViewItem("");
                                        workerItem.SubItems.Add(worker.Name);
                                        workerItem.SubItems.Add(((int)totalWorkerProduction).ToString());
                                        workerItem.SubItems.Add(baseProduction.ToString("0.#"));
                                        workerItem.SubItems.Add(bonusBreakdown.TrimEnd(',', ' '));
                                        productionListView.Items.Add(workerItem);
                                    }

                                    // Add empty row for spacing
                                    productionListView.Items.Add(new ListViewItem(""));
                                }
                            }
                        }
                    }
                }
                else
                {
                    ListViewItem item = new ListViewItem("Not Producing");
                    item.SubItems.Add("-");
                    item.SubItems.Add("-");
                    item.SubItems.Add("-");
                    item.SubItems.Add("-");
                    item.ForeColor = Color.Gray;
                    productionListView.Items.Add(item);
                }
            }

            // Update production summary label
            var productionSummaryLabel = FindControl<Label>(_tabControl.TabPages[1], "ProductionSummaryLabel");
            if (productionSummaryLabel != null)
            {
                if (building.IsFunctional() && building.ActualProduction.Any())
                {
                    var summaryText = string.Join("; ", building.ActualProduction
                        .Select(p => $"{p.ResourceType}: +{p.Amount}/week"));
                    productionSummaryLabel.Text = summaryText;
                }
                else
                {
                    productionSummaryLabel.Text = "Not Producing";
                }
            }

            // Update upkeep list view with detailed breakdown
            var upkeepListView = FindControl<ListView>(_tabControl.TabPages[1], "UpkeepListView");
            if (upkeepListView != null)
            {
                upkeepListView.Items.Clear();
                
                // Get building info for upkeep calculations
                string jsonPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "BuildingData.json");
                if (File.Exists(jsonPath))
                {
                    string json = File.ReadAllText(jsonPath);
                    var buildingData = System.Text.Json.JsonSerializer.Deserialize<BuildingData>(json);
                    var buildingInfo = buildingData.buildings.Find(b => b.type == building.Type.ToString());
                    
                    if (buildingInfo != null)
                    {
                        // Calculate total worker salaries first
                        int totalSalaries = assignedNPCs.Sum(worker => 
                            Math.Max(1, worker.Skills.Any() ? worker.Skills.Max(s => s.Level) : 1));

                        // Get all upkeep values for current level
                        var upkeepAtLevel = buildingInfo.upkeepScaling.Where(u => u.level == building.Level).ToList();
                        
                        // Add resource header rows
                        foreach (var upkeep in upkeepAtLevel)
                        {
                            if (upkeep.resourceType == "Gold")
                            {
                                var headerItem = new ListViewItem("Gold");
                                headerItem.SubItems.Add("Base Upkeep");
                                headerItem.SubItems.Add((upkeep.baseValue + totalSalaries).ToString());
                                headerItem.SubItems.Add($"Base upkeep[{upkeep.baseValue}] + Salaries[{totalSalaries}]");
                                headerItem.BackColor = Color.LightGray;
                                headerItem.Font = new Font(upkeepListView.Font, FontStyle.Bold);
                                upkeepListView.Items.Add(headerItem);

                                // Add worker rows
                                foreach (var worker in assignedNPCs)
                                {
                                    int salary = Math.Max(1, worker.Skills.Any() ? worker.Skills.Max(s => s.Level) : 1);
                                    var highestSkill = worker.Skills.Any() ? 
                                        worker.Skills.OrderByDescending(s => s.Level).First() : null;

                                    var salaryItem = new ListViewItem("");  // Empty resource column
                                    salaryItem.SubItems.Add(worker.Name);
                                    salaryItem.SubItems.Add(salary.ToString());
                                    salaryItem.SubItems.Add(highestSkill != null ? 
                                        $"{highestSkill.Name} ({highestSkill.Level})" : 
                                        "No Skills (1)");
                                    upkeepListView.Items.Add(salaryItem);
                                }
                            }
                            else
                            {
                                var headerItem = new ListViewItem(upkeep.resourceType);
                                headerItem.SubItems.Add("Base Upkeep");
                                headerItem.SubItems.Add(upkeep.baseValue.ToString());
                                headerItem.SubItems.Add($"Base upkeep[{upkeep.baseValue}]");
                                headerItem.BackColor = Color.LightGray;
                                headerItem.Font = new Font(upkeepListView.Font, FontStyle.Bold);
                                upkeepListView.Items.Add(headerItem);
                            }
                        }
                    }
                }
            }

            // Update upkeep summary label
            var upkeepSummaryLabel = FindControl<Label>(_tabControl.TabPages[1], "UpkeepSummaryLabel");
            if (upkeepSummaryLabel != null)
            {
                var summaryParts = new List<string>();

                // Get building info for upkeep calculations
                string jsonPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "BuildingData.json");
                if (File.Exists(jsonPath))
                {
                    string json = File.ReadAllText(jsonPath);
                    var buildingData = System.Text.Json.JsonSerializer.Deserialize<BuildingData>(json);
                    var buildingInfo = buildingData.buildings.Find(b => b.type == building.Type.ToString());
                    
                    if (buildingInfo != null)
                    {
                        // Calculate total worker salaries
                        int totalSalaries = assignedNPCs.Sum(worker => 
                            Math.Max(1, worker.Skills.Any() ? worker.Skills.Max(s => s.Level) : 1));

                        // Get all upkeep values for current level
                        var upkeepAtLevel = buildingInfo.upkeepScaling.Where(u => u.level == building.Level).ToList();
                        
                        // Add each resource upkeep to summary
                        foreach (var upkeep in upkeepAtLevel)
                        {
                            int totalUpkeep = upkeep.baseValue;
                            if (upkeep.resourceType == "Gold")
                            {
                                totalUpkeep += totalSalaries;
                            }
                            summaryParts.Add($"{upkeep.resourceType}: -{totalUpkeep}/week");
                        }
                    }
                }

                upkeepSummaryLabel.Text = summaryParts.Any() ? string.Join("; ", summaryParts) : "No Upkeep";
            }

            // Update building details in the right panel
            var buildingNameValue = FindControl<TextBox>(_tabControl.TabPages[1], "BuildingNameValue");
            var buildingTypeValue = FindControl<Label>(_tabControl.TabPages[1], "BuildingTypeValue");
            var buildingLevelValue = FindControl<Label>(_tabControl.TabPages[1], "BuildingLevelValue");
            var buildingStatusValue = FindControl<Label>(_tabControl.TabPages[1], "BuildingStatusValue");
            var buildingConditionValue = FindControl<Label>(_tabControl.TabPages[1], "BuildingConditionValue");

            var upgradeButton = FindControl<Button>(_tabControl.TabPages[1], "UpgradeButton");
            var repairButton = FindControl<Button>(_tabControl.TabPages[1], "RepairButton");
            var cancelButton = FindControl<Button>(_tabControl.TabPages[1], "CancelButton");

            if (buildingNameValue != null) buildingNameValue.Text = building.Name;
            if (buildingTypeValue != null) buildingTypeValue.Text = building.Type.ToString();
            if (buildingLevelValue != null) buildingLevelValue.Text = building.Level.ToString();
            
            // Update status with progress and time if applicable
            if (buildingStatusValue != null)
            {
                string statusText = building.ConstructionStatus.ToString();
                if (building.ConstructionStatus == BuildingStatus.UnderConstruction ||
                    building.ConstructionStatus == BuildingStatus.Repairing ||
                    building.ConstructionStatus == BuildingStatus.Upgrading)
                {
                    if (building.AssignedWorkers.Count == 0)
                    {
                        statusText += " (No Workers)";
                    }
                    else
                    {
                        statusText += $" ({building.ConstructionProgress}%)";
                    }
                }
                buildingStatusValue.Text = statusText;
            }

            if (buildingConditionValue != null)
                buildingConditionValue.Text = $"{building.Condition}%";

            // Update button states
            if (upgradeButton != null)
            {
                string jsonPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "BuildingData.json");
                if (File.Exists(jsonPath))
                {
                    string json = File.ReadAllText(jsonPath);
                    var buildingData = System.Text.Json.JsonSerializer.Deserialize<BuildingData>(json);
                    var buildingInfo = buildingData.buildings.Find(b => b.type == building.Type.ToString());
                    bool isMaxLevel = buildingInfo != null && building.Level >= buildingInfo.maxLevel;
                    bool canUpgrade = building.ConstructionStatus == BuildingStatus.Complete && 
                                    buildingInfo != null && 
                                    !isMaxLevel &&
                                    building.AssignedWorkers.Count > 0;
                    upgradeButton.Enabled = canUpgrade;
                    upgradeButton.Text = isMaxLevel ? "Max Lvl" : "Upgrade";
                }
            }

            if (repairButton != null)
                repairButton.Enabled = building.Condition < 100 && building.ConstructionStatus != BuildingStatus.Repairing;

            if (cancelButton != null)
                cancelButton.Visible = building.ConstructionStatus == BuildingStatus.Planning;
        }
        else
        {
            _selectedBuildingId = null;
            ClearBuildingDetails();
        }
    }

    private void ShowResourceInTab(ResourceType resourceType)
    {
        // Find the Resources tab
        for (int i = 0; i < _tabControl.TabPages.Count; i++)
        {
            if (_tabControl.TabPages[i].Text == "Resources")
            {
                _tabControl.SelectedIndex = i;
                // Find the resources list view
                ListView resourcesListView = FindControl<ListView>(_tabControl.TabPages[i], "ResourcesListView");
                if (resourcesListView != null)
                {
                    foreach (ListViewItem item in resourcesListView.Items)
                    {
                        var resource = (Resource)item.Tag;
                        if (resource.Type == resourceType)
                        {
                            item.Selected = true;
                            item.Focused = true;
                            resourcesListView.Select();
                            resourcesListView.EnsureVisible(item.Index);
                            break;
                        }
                    }
                }
                break;
            }
        }
    }

    private void ClearBuildingDetails()
    {
        var buildingNameValue = FindControl<TextBox>(_tabControl.TabPages[1], "BuildingNameValue");
        var buildingTypeValue = FindControl<Label>(_tabControl.TabPages[1], "BuildingTypeValue");
        var buildingLevelValue = FindControl<Label>(_tabControl.TabPages[1], "BuildingLevelValue");
        var buildingStatusValue = FindControl<Label>(_tabControl.TabPages[1], "BuildingStatusValue");
        var buildingConditionValue = FindControl<Label>(_tabControl.TabPages[1], "BuildingConditionValue");
        var productionListView = FindControl<ListView>(_tabControl.TabPages[1], "ProductionListView");

        if (buildingNameValue != null) buildingNameValue.Text = string.Empty;
        if (buildingTypeValue != null) buildingTypeValue.Text = string.Empty;
        if (buildingLevelValue != null) buildingLevelValue.Text = string.Empty;
        if (buildingStatusValue != null) buildingStatusValue.Text = string.Empty;
        if (buildingConditionValue != null) buildingConditionValue.Text = string.Empty;
        if (productionListView != null) productionListView.Items.Clear();

        var upgradeButton = FindControl<Button>(_tabControl.TabPages[1], "UpgradeButton");
        var repairButton = FindControl<Button>(_tabControl.TabPages[1], "RepairButton");
        var cancelButton = FindControl<Button>(_tabControl.TabPages[1], "CancelButton");

        if (upgradeButton != null) upgradeButton.Enabled = false;
        if (repairButton != null) repairButton.Enabled = false;
        if (cancelButton != null) cancelButton.Visible = false;
    }

    private void RenameBuilding_Click(object sender, EventArgs e)
    {
        if (string.IsNullOrEmpty(_selectedBuildingId)) return;

        var building = _stronghold.Buildings.Find(b => b.Id == _selectedBuildingId);
        if (building == null) return;

        using (var renameDialog = new TextInputDialog("Rename Building", "Enter new name:", building.Name))
        {
            if (renameDialog.ShowDialog() == DialogResult.OK)
            {
                string newName = renameDialog.InputText.Trim();
                if (!string.IsNullOrEmpty(newName))
                {
                    building.Name = newName;
                    RefreshBuildingsTab();
                }
            }
        }
    }

    private void UpgradeBuilding_Click(object sender, EventArgs e)
    {
        if (string.IsNullOrEmpty(_selectedBuildingId)) return;

        var building = _stronghold.Buildings.Find(b => b.Id == _selectedBuildingId);
        if (building == null) return;

        // Get building info from BuildingData.json
        string jsonPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "BuildingData.json");
        if (!File.Exists(jsonPath))
        {
            MessageBox.Show(
                "Building data file not found. Cannot proceed with upgrade.",
                "Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return;
        }

        string json = File.ReadAllText(jsonPath);
        var buildingData = System.Text.Json.JsonSerializer.Deserialize<BuildingData>(json);
        var buildingInfo = buildingData.buildings.Find(b => b.type == building.Type.ToString());
        
        if (buildingInfo == null)
        {
            MessageBox.Show(
                "Building type information not found. Cannot proceed with upgrade.",
                "Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return;
        }

        if (building.Level >= buildingInfo.maxLevel)
        {
            MessageBox.Show(
                "Building is already at maximum level.",
                "Cannot Upgrade",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        // Check if workers are assigned
        if (building.AssignedWorkers.Count == 0)
        {
            MessageBox.Show(
                "Assign workers to the building to start Upgrading",
                "Workers Required",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        using (var confirmDialog = new UpgradeBuildingDialog(building))
        {
            if (confirmDialog.ShowDialog() == DialogResult.OK)
            {
                if (building.StartUpgrade(_stronghold.Resources))
                {
                    _gameStateService.OnGameStateChanged();
                    RefreshBuildingsTab();
                }
                else
                {
                    MessageBox.Show(
                        "Failed to start upgrade. Please check resource requirements and building status.",
                        "Upgrade Failed",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }
            }
        }
    }

    private void RepairBuilding_Click(object sender, EventArgs e)
    {
        if (string.IsNullOrEmpty(_selectedBuildingId)) return;

        var building = _stronghold.Buildings.Find(b => b.Id == _selectedBuildingId);
        if (building == null || building.Condition >= 100) return;

        using (var confirmDialog = new RepairBuildingDialog(building))
        {
            if (confirmDialog.ShowDialog() == DialogResult.OK)
            {
                _gameStateService.StartBuildingRepair(_selectedBuildingId);
                RefreshBuildingsTab();
            }
        }
    }

    private void CancelBuilding_Click(object sender, EventArgs e)
    {
        if (string.IsNullOrEmpty(_selectedBuildingId)) return;

        var building = _stronghold.Buildings.Find(b => b.Id == _selectedBuildingId);
        if (building == null || building.ConstructionStatus != BuildingStatus.Planning) return;

        var result = MessageBox.Show(
            "Are you sure you want to cancel construction? All resources will be refunded.",
            "Cancel Construction",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (result == DialogResult.Yes)
        {
            _gameStateService.CancelBuildingConstruction(_selectedBuildingId);
            RefreshBuildingsTab();
            
            // Get the buildings list view
            var buildingsListView = FindControl<ListView>(_tabControl.TabPages[1], "BuildingsListView");
            if (buildingsListView != null && buildingsListView.Items.Count > 0)
            {
                // Select the first item
                buildingsListView.Items[0].Selected = true;
                buildingsListView.Items[0].Focused = true;
            }
        }
    }

    private void BuildingsListView_ColumnClick(object sender, ColumnClickEventArgs e)
    {
        ListView listView = (ListView)sender;
        
        // Get the new sorting column
        ColumnHeader newSortingColumn = listView.Columns[e.Column];
        
        // Figure out the new sorting order
        SortOrder newSortOrder;
        
        // If we clicked the same column that was clicked last time,
        // reverse the sort order. Otherwise, default to ascending.
        if (_lastSortedColumn == e.Column)
        {
            newSortOrder = _lastSortOrder == SortOrder.Ascending ? 
                          SortOrder.Descending : SortOrder.Ascending;
        }
        else
        {
            newSortOrder = SortOrder.Ascending;
        }
        
        // Sort the items
        listView.ListViewItemSorter = new BuildingsListViewSorter(e.Column, newSortOrder);
        
        // Remember the new sorting column and order
        _lastSortedColumn = e.Column;
        _lastSortOrder = newSortOrder;
    }

    private class BuildingsListViewSorter : System.Collections.IComparer
    {
        private int _column;
        private SortOrder _sortOrder;

        public BuildingsListViewSorter(int column, SortOrder sortOrder)
        {
            _column = column;
            _sortOrder = sortOrder;
        }

        public int Compare(object x, object y)
        {
            ListViewItem itemX = (ListViewItem)x;
            ListViewItem itemY = (ListViewItem)y;
            
            int compareResult;
            
            // Different comparison logic based on column
            switch (_column)
            {
                case 0: // Name
                case 1: // Type
                    compareResult = String.Compare(
                        itemX.SubItems[_column].Text,
                        itemY.SubItems[_column].Text
                    );
                    break;
                    
                case 2: // Workers
                    // Extract current worker count
                    int workersX = int.Parse(itemX.SubItems[_column].Text.Split('/')[0]);
                    int workersY = int.Parse(itemY.SubItems[_column].Text.Split('/')[0]);
                    compareResult = workersX.CompareTo(workersY);
                    break;
                    
                case 3: // State
                    // Get the status without progress info
                    string statusX = itemX.SubItems[_column].Text.Split(' ')[0];
                    string statusY = itemY.SubItems[_column].Text.Split(' ')[0];
                    
                    // Convert status to priority number
                    int priorityX = GetStatusPriority(statusX);
                    int priorityY = GetStatusPriority(statusY);
                    
                    compareResult = priorityX.CompareTo(priorityY);
                    break;
                    
                case 4: // Condition
                    // Extract percentage value
                    int conditionX = int.Parse(itemX.SubItems[_column].Text.TrimEnd('%'));
                    int conditionY = int.Parse(itemY.SubItems[_column].Text.TrimEnd('%'));
                    compareResult = conditionX.CompareTo(conditionY);
                    break;
                    
                case 5: // Level
                    int levelX = int.Parse(itemX.SubItems[_column].Text);
                    int levelY = int.Parse(itemY.SubItems[_column].Text);
                    compareResult = levelX.CompareTo(levelY);
                    break;
                    
                default:
                    compareResult = 0;
                    break;
            }
            
            // Return the result based on sort order
            return _sortOrder == SortOrder.Ascending ? compareResult : -compareResult;
        }
        
        private int GetStatusPriority(string status)
        {
            return status switch
            {
                "Complete" => 0,
                "UnderConstruction" => 1,
                "Planning" => 2,
                "Upgrading" => 3,
                "Repairing" => 4,
                "Damaged" => 5,
                _ => 6
            };
        }
    }

    #endregion

    #endregion

    // Utility method to find a control by tag
    private T FindControl<T>(Control parent, string tag) where T : Control
    {
        if (parent == null) return null;
        
        if (parent is T && parent.Tag?.ToString() == tag)
            return (T)parent;
            
        foreach (Control control in parent.Controls)
        {
            T found = FindControl<T>(control, tag);
            if (found != null)
                return found;
        }
        
        return null;
    }
}
