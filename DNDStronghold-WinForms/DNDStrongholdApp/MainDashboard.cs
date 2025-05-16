using System;
using System.Drawing;
using System.Windows.Forms;
using DNDStrongholdApp.Models;
using DNDStrongholdApp.Services;
using System.Linq;

namespace DNDStrongholdApp;

public partial class MainDashboard : Form
{
    // Reference to the game state service
    private GameStateService _gameStateService;
    
    // Reference to the current stronghold
    private Stronghold _stronghold => _gameStateService.GetCurrentStronghold();
    
    // UI controls that need to be updated
    private StatusStrip _statusStrip;
    private ToolStripStatusLabel _weekLabel;
    private ToolStripStatusLabel _yearLabel;
    private ToolStripStatusLabel _seasonLabel;
    private ToolStripStatusLabel _goldLabel;
    private TabControl _tabControl;
    private Button _nextTurnButton;

    // Track last created building/NPC for selection after refresh
    private string _lastCreatedBuildingId = null;
    private string _lastCreatedNpcId = null;

    public MainDashboard()
    {
        InitializeComponent();
        InitializeDashboard();
    }

    private void InitializeDashboard()
    {
        // Set window title
        this.Text = "D&D Stronghold Management";
        
        // Set window size
        this.Width = 1024;
        this.Height = 768;
        
        // Center the form on screen
        this.StartPosition = FormStartPosition.CenterScreen;

        // Get reference to game state service
        _gameStateService = GameStateService.Instance;
        _gameStateService.GameStateChanged += GameStateService_GameStateChanged;

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

        menuStrip.Items.Add(fileMenu);
        
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
        listView.Columns.Add("Status", 130);
        listView.Columns.Add("Workers", 70);
        
        // Dynamic column widths (45%, 35%, 20%)
        void ResizeBuildingColumns(object s, EventArgs e)
        {
            int totalWidth = listView.ClientSize.Width;
            listView.Columns[0].Width = (int)(totalWidth * 0.45);
            listView.Columns[1].Width = (int)(totalWidth * 0.35);
            listView.Columns[2].Width = (int)(totalWidth * 0.20);
        }
        listView.Resize += ResizeBuildingColumns;
        ResizeBuildingColumns(null, null);
        
        // Add items for each building
        foreach (var building in _stronghold.Buildings)
        {
            ListViewItem item = new ListViewItem(building.Name);
            item.SubItems.Add(building.ConstructionStatus.ToString());
            item.SubItems.Add($"{building.AssignedWorkers.Count}/{building.WorkerSlots}");
            item.Tag = building.Id;
            // Set color based on building status
            if (building.ConstructionStatus == BuildingStatus.Damaged)
            {
                item.ForeColor = Color.Red;
            }
            else if (building.ConstructionStatus == BuildingStatus.UnderConstruction)
            {
                item.ForeColor = Color.Blue;
            }
            listView.Items.Add(item);
        }
        // Double-click to open in Buildings tab
        listView.DoubleClick += (s, e) => {
            if (listView.SelectedItems.Count > 0)
            {
                string buildingId = (string)listView.SelectedItems[0].Tag;
                ShowBuildingInTab(buildingId);
            }
        };
        
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
        listView.Columns.Add("Morale", 80);
        
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
            item.SubItems.Add(npc.Happiness.ToString());
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

    private void InitializeBuildingsTab(TabPage tab)
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
        
        // Buildings list view
        ListView buildingsListView = new ListView();
        buildingsListView.Dock = DockStyle.Fill;
        buildingsListView.View = View.Details;
        buildingsListView.FullRowSelect = true;
        buildingsListView.MultiSelect = false;
        buildingsListView.Tag = "BuildingsListView"; // For identification
        
        // Add columns
        buildingsListView.Columns.Add("Name", 150);
        buildingsListView.Columns.Add("Type", 100);
        buildingsListView.Columns.Add("Status", 100);
        buildingsListView.Columns.Add("Condition", 80);
        buildingsListView.Columns.Add("Workers", 80);
        
        // Dynamic column widths (28%, 18%, 18%, 18%, 18%)
        void ResizeBuildingsTabColumns(object s, EventArgs e)
        {
            int totalWidth = buildingsListView.ClientSize.Width;
            buildingsListView.Columns[0].Width = (int)(totalWidth * 0.28);
            buildingsListView.Columns[1].Width = (int)(totalWidth * 0.18);
            buildingsListView.Columns[2].Width = (int)(totalWidth * 0.18);
            buildingsListView.Columns[3].Width = (int)(totalWidth * 0.18);
            buildingsListView.Columns[4].Width = (int)(totalWidth * 0.18);
        }
        buildingsListView.Resize += ResizeBuildingsTabColumns;
        ResizeBuildingsTabColumns(null, null);
        
        // Add items for each building
        foreach (var building in _stronghold.Buildings)
        {
            ListViewItem item = new ListViewItem(building.Name);
            item.SubItems.Add(building.Type.ToString());
            item.SubItems.Add(building.ConstructionStatus.ToString());
            item.SubItems.Add($"{building.Condition}%");
            item.SubItems.Add($"{building.AssignedWorkers.Count}/{building.WorkerSlots}");
            item.Tag = building.Id; // Store building ID for reference
            
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
            
            buildingsListView.Items.Add(item);
        }
        
        // Add selection changed event
        buildingsListView.SelectedIndexChanged += BuildingsListView_SelectedIndexChanged;
        
        // Building details panel
        GroupBox detailsGroupBox = new GroupBox();
        detailsGroupBox.Text = "Building Details";
        detailsGroupBox.Dock = DockStyle.Fill;
        
        TableLayoutPanel detailsLayout = new TableLayoutPanel();
        detailsLayout.Dock = DockStyle.Fill;
        detailsLayout.ColumnCount = 2;
        detailsLayout.RowCount = 8; // Add a row for assigned workers
        detailsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30F));
        detailsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 70F));
        
        // Labels for building details
        Label nameLabel = new Label { Text = "Name:", TextAlign = ContentAlignment.MiddleRight };
        Label nameValueLabel = new Label { Text = "", Tag = "BuildingName" };
        
        Label typeLabel = new Label { Text = "Type:", TextAlign = ContentAlignment.MiddleRight };
        Label typeValueLabel = new Label { Text = "", Tag = "BuildingType" };
        
        Label statusLabel = new Label { Text = "Status:", TextAlign = ContentAlignment.MiddleRight };
        Label statusValueLabel = new Label { Text = "", Tag = "BuildingStatus" };
        
        Label conditionLabel = new Label { Text = "Condition:", TextAlign = ContentAlignment.MiddleRight };
        Label conditionValueLabel = new Label { Text = "", Tag = "BuildingCondition" };
        
        Label workersLabel = new Label { Text = "Workers:", TextAlign = ContentAlignment.MiddleRight };
        Label workersValueLabel = new Label { Text = "", Tag = "BuildingWorkers" };
        
        Label productionLabel = new Label { Text = "Production:", TextAlign = ContentAlignment.MiddleRight };
        Label productionValueLabel = new Label { Text = "", Tag = "BuildingProduction" };
        
        Label upkeepLabel = new Label { Text = "Upkeep:", TextAlign = ContentAlignment.MiddleRight };
        Label upkeepValueLabel = new Label { Text = "", Tag = "BuildingUpkeep" };
        
        // Assigned workers list
        Label assignedWorkersLabel = new Label { Text = "Assigned Workers:", TextAlign = ContentAlignment.MiddleRight };
        ListBox assignedWorkersListBox = new ListBox { Tag = "AssignedWorkersListBox", Dock = DockStyle.Fill };
        assignedWorkersListBox.SelectedIndexChanged += (s, e) => {
            // Placeholder: In the future, this will open the NPCs tab and select the NPC
            // (handled by double-click now)
        };
        assignedWorkersListBox.DoubleClick += (s, e) => {
            if (assignedWorkersListBox.SelectedItem is NPC selectedNpc)
            {
                ShowNPCInTab(selectedNpc.Id);
            }
        };
        
        // Add labels to details layout
        detailsLayout.Controls.Add(nameLabel, 0, 0);
        detailsLayout.Controls.Add(nameValueLabel, 1, 0);
        detailsLayout.Controls.Add(typeLabel, 0, 1);
        detailsLayout.Controls.Add(typeValueLabel, 1, 1);
        detailsLayout.Controls.Add(statusLabel, 0, 2);
        detailsLayout.Controls.Add(statusValueLabel, 1, 2);
        detailsLayout.Controls.Add(conditionLabel, 0, 3);
        detailsLayout.Controls.Add(conditionValueLabel, 1, 3);
        detailsLayout.Controls.Add(workersLabel, 0, 4);
        detailsLayout.Controls.Add(workersValueLabel, 1, 4);
        detailsLayout.Controls.Add(productionLabel, 0, 5);
        detailsLayout.Controls.Add(productionValueLabel, 1, 5);
        detailsLayout.Controls.Add(upkeepLabel, 0, 6);
        detailsLayout.Controls.Add(upkeepValueLabel, 1, 6);
        detailsLayout.Controls.Add(assignedWorkersLabel, 0, 7);
        detailsLayout.Controls.Add(assignedWorkersListBox, 1, 7);
        
        detailsGroupBox.Controls.Add(detailsLayout);
        
        // Action buttons panel
        Panel actionsPanel = new Panel();
        actionsPanel.Dock = DockStyle.Fill;
        
        // Repair button
        Button repairButton = new Button();
        repairButton.Text = "Repair Building";
        repairButton.Size = new Size(120, 30);
        repairButton.Location = new Point(10, 90);
        repairButton.Tag = "RepairButton";
        repairButton.Enabled = false; // Disabled by default
        repairButton.Click += RepairButton_Click;
        
        // Assign workers button
        Button assignWorkersButton = new Button();
        assignWorkersButton.Text = "Assign Workers";
        assignWorkersButton.Size = new Size(120, 30);
        assignWorkersButton.Location = new Point(10, 50);
        assignWorkersButton.Tag = "AssignWorkersButton";
        assignWorkersButton.Enabled = false; // Disabled by default
        assignWorkersButton.Click += AssignWorkersButton_Click;
        
        // Cancel construction button
        Button cancelConstructionButton = new Button();
        cancelConstructionButton.Text = "Cancel Construction";
        cancelConstructionButton.Size = new Size(120, 30);
        cancelConstructionButton.Location = new Point(10, 130);
        cancelConstructionButton.Tag = "CancelConstructionButton";
        cancelConstructionButton.Enabled = false;
        cancelConstructionButton.Click += CancelConstructionButton_Click;
        
        // Add new building button
        Button addBuildingButton = new Button();
        addBuildingButton.Text = "Add New Building";
        addBuildingButton.Size = new Size(120, 30);
        addBuildingButton.Location = new Point(10, 10);
        addBuildingButton.Click += AddBuildingButton_Click;
        
        actionsPanel.Controls.Add(repairButton);
        actionsPanel.Controls.Add(assignWorkersButton);
        actionsPanel.Controls.Add(cancelConstructionButton);
        actionsPanel.Controls.Add(addBuildingButton);
        
        // Add controls to layout
        layout.Controls.Add(buildingsListView, 0, 0);
        layout.Controls.Add(detailsGroupBox, 1, 0);
        layout.Controls.Add(actionsPanel, 1, 1);
        
        // Add layout to tab
        tab.Controls.Add(layout);
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
        npcsListView.Columns.Add("Morale", 80);
        
        // Dynamic column widths (28%, 18%, 12%, 28%, 14%)
        void ResizeNPCsTabColumns(object s, EventArgs e)
        {
            int totalWidth = npcsListView.ClientSize.Width;
            npcsListView.Columns[0].Width = (int)(totalWidth * 0.28);
            npcsListView.Columns[1].Width = (int)(totalWidth * 0.18);
            npcsListView.Columns[2].Width = (int)(totalWidth * 0.12);
            npcsListView.Columns[3].Width = (int)(totalWidth * 0.28);
            npcsListView.Columns[4].Width = (int)(totalWidth * 0.14);
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
            item.SubItems.Add(npc.Happiness.ToString());
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
        detailsLayout.RowCount = 7;
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

    private void InitializeResourcesTab(TabPage tab)
    {
        // To be implemented
        Label placeholder = new Label();
        placeholder.Text = "Resources tab content will be implemented here";
        placeholder.Dock = DockStyle.Fill;
        placeholder.TextAlign = ContentAlignment.MiddleCenter;
        tab.Controls.Add(placeholder);
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
        // Store current selected tab
        int selectedIndex = _tabControl.SelectedIndex;
        // Store selected building ID (if on Buildings tab)
        string selectedBuildingId = null;
        if (_tabControl.TabPages.Count > 1 && _tabControl.SelectedTab.Text == "Buildings")
        {
            ListView buildingsListView = FindControl<ListView>(_tabControl.TabPages[1], "BuildingsListView");
            if (buildingsListView != null && buildingsListView.SelectedItems.Count > 0)
            {
                selectedBuildingId = buildingsListView.SelectedItems[0].Tag as string;
            }
        }
        // Store selected NPC ID (if on NPCs tab)
        string selectedNpcId = null;
        if (_tabControl.TabPages.Count > 2 && _tabControl.SelectedTab.Text == "NPCs")
        {
            ListView npcsListView = FindControl<ListView>(_tabControl.TabPages[2], "NPCsListView");
            if (npcsListView != null && npcsListView.SelectedItems.Count > 0)
            {
                selectedNpcId = npcsListView.SelectedItems[0].Tag as string;
            }
        }
        
        // Clear and recreate tab control
        this.Controls.Remove(_tabControl);
        CreateTabControl();
        
        // Restore selected tab
        if (selectedIndex >= 0 && selectedIndex < _tabControl.TabPages.Count)
            _tabControl.SelectedIndex = selectedIndex;
        else
            _tabControl.SelectedIndex = 0;
        
        // Select new building if just created
        if (_lastCreatedBuildingId != null && _tabControl.TabPages.Count > 1)
        {
            ListView buildingsListView = FindControl<ListView>(_tabControl.TabPages[1], "BuildingsListView");
            if (buildingsListView != null)
            {
                foreach (ListViewItem item in buildingsListView.Items)
                {
                    if ((string)item.Tag == _lastCreatedBuildingId)
                    {
                        item.Selected = true;
                        item.Focused = true;
                        buildingsListView.Select();
                        buildingsListView.EnsureVisible(item.Index);
                        break;
                    }
                }
            }
            _lastCreatedBuildingId = null;
        }
        // Otherwise, restore selected building
        else if (selectedBuildingId != null && _tabControl.TabPages.Count > 1)
        {
            ListView buildingsListView = FindControl<ListView>(_tabControl.TabPages[1], "BuildingsListView");
            if (buildingsListView != null)
            {
                foreach (ListViewItem item in buildingsListView.Items)
                {
                    if ((string)item.Tag == selectedBuildingId)
                    {
                        item.Selected = true;
                        item.Focused = true;
                        buildingsListView.Select();
                        buildingsListView.EnsureVisible(item.Index);
                        break;
                    }
                }
            }
        }
        // Select new NPC if just created
        if (_lastCreatedNpcId != null && _tabControl.TabPages.Count > 2)
        {
            ListView npcsListView = FindControl<ListView>(_tabControl.TabPages[2], "NPCsListView");
            if (npcsListView != null)
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
            }
            _lastCreatedNpcId = null;
        }
        // Otherwise, restore selected NPC
        else if (selectedNpcId != null && _tabControl.TabPages.Count > 2)
        {
            ListView npcsListView = FindControl<ListView>(_tabControl.TabPages[2], "NPCsListView");
            if (npcsListView != null)
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
        }
        
        // Ensure proper Z-order of controls
        this.Controls.SetChildIndex(_tabControl, 0); // Tab control at the back
        this.Controls.SetChildIndex(_nextTurnButton, 1); // Next turn button above tab control
        this.Controls.SetChildIndex(_statusStrip, 2); // Status strip on top
    }

    #region Buildings Tab Event Handlers
    
    private void BuildingsListView_SelectedIndexChanged(object sender, EventArgs e)
    {
        ListView listView = (ListView)sender;
        Button repairButton = FindControl<Button>(listView.Parent, "RepairButton");
        Button assignWorkersButton = FindControl<Button>(listView.Parent, "AssignWorkersButton");
        Button cancelConstructionButton = FindControl<Button>(listView.Parent, "CancelConstructionButton");
        if (listView.SelectedItems.Count > 0)
        {
            string buildingId = (string)listView.SelectedItems[0].Tag;
            Building selectedBuilding = _stronghold.Buildings.Find(b => b.Id == buildingId);
            if (selectedBuilding != null)
            {
                UpdateBuildingDetails(listView.Parent, selectedBuilding);
                if (repairButton != null)
                    repairButton.Enabled = selectedBuilding.ConstructionStatus == BuildingStatus.Damaged;
                if (assignWorkersButton != null)
                    assignWorkersButton.Enabled = selectedBuilding.ConstructionStatus == BuildingStatus.Complete;
                if (cancelConstructionButton != null)
                    cancelConstructionButton.Enabled = selectedBuilding.ConstructionStatus == BuildingStatus.Planning;
            }
        }
        else
        {
            ClearBuildingDetails(listView.Parent);
            if (repairButton != null) repairButton.Enabled = false;
            if (assignWorkersButton != null) assignWorkersButton.Enabled = false;
            if (cancelConstructionButton != null) cancelConstructionButton.Enabled = false;
        }
    }
    
    private void RepairButton_Click(object sender, EventArgs e)
    {
        // Get selected building
        ListView buildingsListView = FindControl<ListView>(((Button)sender).Parent.Parent, "BuildingsListView");
        
        if (buildingsListView != null && buildingsListView.SelectedItems.Count > 0)
        {
            string buildingId = (string)buildingsListView.SelectedItems[0].Tag;
            
            // Start repair
            bool success = _gameStateService.StartBuildingRepair(buildingId);
            
            if (!success)
            {
                MessageBox.Show("Not enough resources to repair this building.", "Repair Failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }
    
    private void AssignWorkersButton_Click(object sender, EventArgs e)
    {
        // Get selected building
        ListView buildingsListView = FindControl<ListView>(((Button)sender).Parent.Parent, "BuildingsListView");
        
        if (buildingsListView != null && buildingsListView.SelectedItems.Count > 0)
        {
            string buildingId = (string)buildingsListView.SelectedItems[0].Tag;
            Building selectedBuilding = _stronghold.Buildings.Find(b => b.Id == buildingId);
            
            if (selectedBuilding != null)
            {
                // Show worker assignment dialog
                using (var workerDialog = new WorkerAssignmentDialog(selectedBuilding, _stronghold.NPCs))
                {
                    if (workerDialog.ShowDialog() == DialogResult.OK)
                    {
                        // Update building with new worker assignments
                        _gameStateService.AssignWorkersToBuilding(selectedBuilding.Id, workerDialog.AssignedWorkerIds);
                    }
                }
            }
        }
    }
    
    private void AddBuildingButton_Click(object sender, EventArgs e)
    {
        // Show building type selection dialog
        using (var typeDialog = new AddBuildingDialog())
        {
            if (typeDialog.ShowDialog() == DialogResult.OK)
            {
                // Create new building
                Building newBuilding = new Building(typeDialog.SelectedBuildingType);
                newBuilding.Name = string.IsNullOrWhiteSpace(typeDialog.BuildingName) ? newBuilding.Type.ToString() : typeDialog.BuildingName;
                // Add to game state
                _lastCreatedBuildingId = newBuilding.Id;
                _gameStateService.AddBuilding(newBuilding);
            }
        }
    }
    
    private void UpdateBuildingDetails(Control parent, Building building)
    {
        // Update labels with building details
        Label nameLabel = FindControl<Label>(parent, "BuildingName");
        if (nameLabel != null) nameLabel.Text = building.Name;
        
        Label typeLabel = FindControl<Label>(parent, "BuildingType");
        if (typeLabel != null) typeLabel.Text = building.Type.ToString();
        
        Label statusLabel = FindControl<Label>(parent, "BuildingStatus");
        if (statusLabel != null)
        {
            statusLabel.Text = building.ConstructionStatus.ToString();
            
            // Add time remaining if under construction or repair
            if (building.ConstructionStatus == BuildingStatus.UnderConstruction)
            {
                statusLabel.Text += $" ({building.ConstructionTimeRemaining} weeks left)";
            }
            else if (building.ConstructionStatus == BuildingStatus.Repairing)
            {
                statusLabel.Text += $" ({building.RepairTimeRemaining} weeks left)";
            }
        }
        
        Label conditionLabel = FindControl<Label>(parent, "BuildingCondition");
        if (conditionLabel != null) conditionLabel.Text = $"{building.Condition}%";
        
        Label workersLabel = FindControl<Label>(parent, "BuildingWorkers");
        if (workersLabel != null) workersLabel.Text = $"{building.AssignedWorkers.Count}/{building.WorkerSlots}";
        
        Label productionLabel = FindControl<Label>(parent, "BuildingProduction");
        if (productionLabel != null)
        {
            string productionText = "";
            foreach (var production in building.ActualProduction)
            {
                productionText += $"{production.ResourceType}: +{production.Amount}/week\n";
            }
            productionLabel.Text = productionText.TrimEnd('\n');
        }
        
        Label upkeepLabel = FindControl<Label>(parent, "BuildingUpkeep");
        if (upkeepLabel != null)
        {
            string upkeepText = "";
            foreach (var upkeep in building.ActualUpkeep)
            {
                upkeepText += $"{upkeep.ResourceType}: -{upkeep.Amount}/week\n";
            }
            upkeepLabel.Text = upkeepText.TrimEnd('\n');
        }

        // Update assigned workers list
        ListBox assignedWorkersListBox = FindControl<ListBox>(parent, "AssignedWorkersListBox");
        if (assignedWorkersListBox != null)
        {
            assignedWorkersListBox.Items.Clear();
            foreach (var workerId in building.AssignedWorkers)
            {
                var npc = _stronghold.NPCs.Find(n => n.Id == workerId);
                if (npc != null)
                {
                    assignedWorkersListBox.Items.Add(npc);
                }
            }
            assignedWorkersListBox.DisplayMember = "Name";
        }
    }
    
    private void ClearBuildingDetails(Control parent)
    {
        Label nameLabel = FindControl<Label>(parent, "BuildingName");
        if (nameLabel != null) nameLabel.Text = "";
        
        Label typeLabel = FindControl<Label>(parent, "BuildingType");
        if (typeLabel != null) typeLabel.Text = "";
        
        Label statusLabel = FindControl<Label>(parent, "BuildingStatus");
        if (statusLabel != null) statusLabel.Text = "";
        
        Label conditionLabel = FindControl<Label>(parent, "BuildingCondition");
        if (conditionLabel != null) conditionLabel.Text = "";
        
        Label workersLabel = FindControl<Label>(parent, "BuildingWorkers");
        if (workersLabel != null) workersLabel.Text = "";
        
        Label productionLabel = FindControl<Label>(parent, "BuildingProduction");
        if (productionLabel != null) productionLabel.Text = "";
        
        Label upkeepLabel = FindControl<Label>(parent, "BuildingUpkeep");
        if (upkeepLabel != null) upkeepLabel.Text = "";

        // Clear assigned workers list
        ListBox assignedWorkersListBox = FindControl<ListBox>(parent, "AssignedWorkersListBox");
        if (assignedWorkersListBox != null)
        {
            assignedWorkersListBox.Items.Clear();
        }
    }
    
    // Helper method to find control by tag
    private T FindControl<T>(Control parent, string tag) where T : Control
    {
        foreach (Control control in parent.Controls)
        {
            if (control is T && control.Tag?.ToString() == tag)
            {
                return (T)control;
            }
            
            // Recursively search in child controls
            if (control.Controls.Count > 0)
            {
                T result = FindControl<T>(control, tag);
                if (result != null)
                {
                    return result;
                }
            }
        }
        
        return null;
    }
    
    #endregion

    // Event handler for NPCs list selection
    private void NPCsListView_SelectedIndexChanged(object sender, EventArgs e)
    {
        ListView listView = (ListView)sender;
        if (listView.SelectedItems.Count > 0)
        {
            string npcId = (string)listView.SelectedItems[0].Tag;
            NPC selectedNpc = _stronghold.NPCs.Find(n => n.Id == npcId);
            if (selectedNpc != null)
            {
                UpdateNPCDetails(listView.Parent, selectedNpc);
            }
        }
        else
        {
            ClearNPCDetails(listView.Parent);
        }
    }

    // Update NPC details panel
    private void UpdateNPCDetails(Control parent, NPC npc)
    {
        Label nameLabel = FindControl<Label>(parent, "NPCName");
        if (nameLabel != null) nameLabel.Text = npc.Name;
        Label typeLabel = FindControl<Label>(parent, "NPCType");
        if (typeLabel != null) typeLabel.Text = npc.Type.ToString();
        Label levelLabel = FindControl<Label>(parent, "NPCLevel");
        if (levelLabel != null) levelLabel.Text = npc.Level.ToString();
        Label expLabel = FindControl<Label>(parent, "NPCExperience");
        if (expLabel != null) expLabel.Text = npc.Experience.ToString();
        Label happinessLabel = FindControl<Label>(parent, "NPCHappiness");
        if (happinessLabel != null) happinessLabel.Text = npc.Happiness.ToString();
        Label skillsLabel = FindControl<Label>(parent, "NPCSkills");
        if (skillsLabel != null)
        {
            string skillsText = string.Join(", ", npc.Skills.ConvertAll(s => $"{s.Name} ({s.Level})"));
            skillsLabel.Text = skillsText;
        }
        Label assignmentLabel = FindControl<Label>(parent, "NPCAssignment");
        if (assignmentLabel != null)
        {
            assignmentLabel.Text = npc.Assignment.Type == AssignmentType.Unassigned ? "Unassigned" : npc.Assignment.TargetName;
        }
    }

    // Clear NPC details panel
    private void ClearNPCDetails(Control parent)
    {
        Label nameLabel = FindControl<Label>(parent, "NPCName");
        if (nameLabel != null) nameLabel.Text = "";
        Label typeLabel = FindControl<Label>(parent, "NPCType");
        if (typeLabel != null) typeLabel.Text = "";
        Label levelLabel = FindControl<Label>(parent, "NPCLevel");
        if (levelLabel != null) levelLabel.Text = "";
        Label expLabel = FindControl<Label>(parent, "NPCExperience");
        if (expLabel != null) expLabel.Text = "";
        Label happinessLabel = FindControl<Label>(parent, "NPCHappiness");
        if (happinessLabel != null) happinessLabel.Text = "";
        Label skillsLabel = FindControl<Label>(parent, "NPCSkills");
        if (skillsLabel != null) skillsLabel.Text = "";
        Label assignmentLabel = FindControl<Label>(parent, "NPCAssignment");
        if (assignmentLabel != null) assignmentLabel.Text = "";
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

    // Helper to switch to Buildings tab and select a building
    private void ShowBuildingInTab(string buildingId)
    {
        for (int i = 0; i < _tabControl.TabPages.Count; i++)
        {
            if (_tabControl.TabPages[i].Text == "Buildings")
            {
                _tabControl.SelectedIndex = i;
                ListView buildingsListView = FindControl<ListView>(_tabControl.TabPages[i], "BuildingsListView");
                if (buildingsListView != null)
                {
                    foreach (ListViewItem item in buildingsListView.Items)
                    {
                        if ((string)item.Tag == buildingId)
                        {
                            item.Selected = true;
                            item.Focused = true;
                            buildingsListView.Select();
                            buildingsListView.EnsureVisible(item.Index);
                            break;
                        }
                    }
                }
                break;
            }
        }
    }

    private void CancelConstructionButton_Click(object sender, EventArgs e)
    {
        ListView buildingsListView = FindControl<ListView>(((Button)sender).Parent.Parent, "BuildingsListView");
        if (buildingsListView != null && buildingsListView.SelectedItems.Count > 0)
        {
            string buildingId = (string)buildingsListView.SelectedItems[0].Tag;
            Building selectedBuilding = _stronghold.Buildings.Find(b => b.Id == buildingId);
            if (selectedBuilding != null && selectedBuilding.ConstructionStatus == BuildingStatus.Planning)
            {
                var result = MessageBox.Show($"Are you sure you want to cancel construction of '{selectedBuilding.Name}'? All resources will be refunded.", "Cancel Construction", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (result == DialogResult.Yes)
                {
                    bool canceled = _gameStateService.CancelBuildingConstruction(buildingId);
                    if (!canceled)
                    {
                        MessageBox.Show("Unable to cancel construction. The building may no longer be in the Planning phase.", "Cancel Failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                }
            }
        }
    }

    #endregion

    // Helper dialog for input
    public class InputBoxDialog : Form
    {
        public string InputText => _textBox.Text;
        private TextBox _textBox;
        public InputBoxDialog(string title, string prompt)
        {
            this.Text = title;
            this.Size = new Size(350, 150);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ShowInTaskbar = false;
            
            Label promptLabel = new Label();
            promptLabel.Text = prompt;
            promptLabel.Location = new Point(10, 10);
            promptLabel.Size = new Size(320, 20);
            
            _textBox = new TextBox();
            _textBox.Location = new Point(10, 40);
            _textBox.Size = new Size(320, 20);
            
            Button okButton = new Button();
            okButton.Text = "OK";
            okButton.DialogResult = DialogResult.OK;
            okButton.Location = new Point(170, 80);
            okButton.Size = new Size(75, 23);
            
            Button cancelButton = new Button();
            cancelButton.Text = "Cancel";
            cancelButton.DialogResult = DialogResult.Cancel;
            cancelButton.Location = new Point(255, 80);
            cancelButton.Size = new Size(75, 23);
            
            this.Controls.Add(promptLabel);
            this.Controls.Add(_textBox);
            this.Controls.Add(okButton);
            this.Controls.Add(cancelButton);
            this.AcceptButton = okButton;
            this.CancelButton = cancelButton;
        }
    }
}
