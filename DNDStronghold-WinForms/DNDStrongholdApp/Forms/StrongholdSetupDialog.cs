using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using DNDStrongholdApp.Models;

namespace DNDStrongholdApp
{
    public class StrongholdSetupDialog : Form
    {
        // Properties to store the setup data
        public string StrongholdName { get; private set; }
        public string StrongholdLocation { get; private set; }
        public List<Building> Buildings { get; private set; } = new List<Building>();
        public List<NPC> NPCs { get; private set; } = new List<NPC>();
        public Dictionary<ResourceType, int> StartingResources { get; private set; } = new Dictionary<ResourceType, int>();
        
        // UI Controls
        private TabControl _tabControl;
        private TextBox _nameTextBox;
        private TextBox _locationTextBox;
        private ListView _buildingsListView;
        private ListView _npcsListView;
        private ListView _resourcesListView;
        
        public StrongholdSetupDialog()
        {
            InitializeComponent();
            InitializeData();
        }
        
        private void InitializeComponent()
        {
            this.Text = "Stronghold Setup";
            this.Size = new Size(800, 600);
            this.StartPosition = FormStartPosition.CenterParent;
            this.MinimizeBox = false;
            this.MaximizeBox = false;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.ShowInTaskbar = false;
            
            // Create tab control
            _tabControl = new TabControl();
            _tabControl.Dock = DockStyle.Fill;
            
            // Create tabs
            TabPage basicInfoTab = new TabPage("Basic Info");
            TabPage buildingsTab = new TabPage("Buildings");
            TabPage npcsTab = new TabPage("NPCs");
            TabPage resourcesTab = new TabPage("Resources");
            
            // Add tabs to tab control
            _tabControl.TabPages.Add(basicInfoTab);
            _tabControl.TabPages.Add(buildingsTab);
            _tabControl.TabPages.Add(npcsTab);
            _tabControl.TabPages.Add(resourcesTab);
            
            // Initialize tab contents
            InitializeBasicInfoTab(basicInfoTab);
            InitializeBuildingsTab(buildingsTab);
            InitializeNPCsTab(npcsTab);
            InitializeResourcesTab(resourcesTab);
            
            // Create buttons panel
            Panel buttonsPanel = new Panel();
            buttonsPanel.Dock = DockStyle.Bottom;
            buttonsPanel.Height = 50;
            
            Button okButton = new Button();
            okButton.Text = "OK";
            okButton.DialogResult = DialogResult.OK;
            okButton.Location = new Point(buttonsPanel.Width - 180, 10);
            okButton.Size = new Size(80, 30);
            okButton.Click += OkButton_Click;
            
            Button cancelButton = new Button();
            cancelButton.Text = "Cancel";
            cancelButton.DialogResult = DialogResult.Cancel;
            cancelButton.Location = new Point(buttonsPanel.Width - 90, 10);
            cancelButton.Size = new Size(80, 30);
            
            buttonsPanel.Controls.Add(okButton);
            buttonsPanel.Controls.Add(cancelButton);
            
            // Add controls to form
            this.Controls.Add(_tabControl);
            this.Controls.Add(buttonsPanel);
            
            // Set form properties
            this.AcceptButton = okButton;
            this.CancelButton = cancelButton;
        }
        
        private void InitializeBasicInfoTab(TabPage tab)
        {
            TableLayoutPanel layout = new TableLayoutPanel();
            layout.Dock = DockStyle.Fill;
            layout.ColumnCount = 2;
            layout.RowCount = 3;
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 70F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            
            // Stronghold name
            Label nameLabel = new Label();
            nameLabel.Text = "Stronghold Name:";
            nameLabel.TextAlign = ContentAlignment.MiddleRight;
            nameLabel.Dock = DockStyle.Fill;
            
            _nameTextBox = new TextBox();
            _nameTextBox.Dock = DockStyle.Fill;
            _nameTextBox.Text = "New Stronghold";
            
            // Stronghold location
            Label locationLabel = new Label();
            locationLabel.Text = "Location:";
            locationLabel.TextAlign = ContentAlignment.MiddleRight;
            locationLabel.Dock = DockStyle.Fill;
            
            _locationTextBox = new TextBox();
            _locationTextBox.Dock = DockStyle.Fill;
            _locationTextBox.Text = "Unknown";
            
            // Add controls to layout
            layout.Controls.Add(nameLabel, 0, 0);
            layout.Controls.Add(_nameTextBox, 1, 0);
            layout.Controls.Add(locationLabel, 0, 1);
            layout.Controls.Add(_locationTextBox, 1, 1);
            
            // Description
            Label descriptionLabel = new Label();
            descriptionLabel.Text = "Enter the basic information for your stronghold. You can customize buildings, NPCs, and resources in the other tabs.";
            descriptionLabel.Dock = DockStyle.Fill;
            descriptionLabel.TextAlign = ContentAlignment.TopLeft;
            
            layout.Controls.Add(descriptionLabel, 0, 2);
            layout.SetColumnSpan(descriptionLabel, 2);
            
            tab.Controls.Add(layout);
        }
        
        private void InitializeBuildingsTab(TabPage tab)
        {
            TableLayoutPanel layout = new TableLayoutPanel();
            layout.Dock = DockStyle.Fill;
            layout.ColumnCount = 1;
            layout.RowCount = 2;
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 80F));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 20F));
            
            // Buildings list view
            _buildingsListView = new ListView();
            _buildingsListView.Dock = DockStyle.Fill;
            _buildingsListView.View = View.Details;
            _buildingsListView.FullRowSelect = true;
            _buildingsListView.MultiSelect = false;
            _buildingsListView.CheckBoxes = true;
            
            // Add columns
            _buildingsListView.Columns.Add("Type", 150);
            _buildingsListView.Columns.Add("Status", 100);
            _buildingsListView.Columns.Add("Condition", 80);
            
            // Add buildings list view to layout
            layout.Controls.Add(_buildingsListView, 0, 0);
            
            // Add building controls panel
            Panel buildingControlsPanel = new Panel();
            buildingControlsPanel.Dock = DockStyle.Fill;
            
            // Add building button
            Button addBuildingButton = new Button();
            addBuildingButton.Text = "Add Building";
            addBuildingButton.Location = new Point(10, 10);
            addBuildingButton.Size = new Size(100, 30);
            addBuildingButton.Click += AddBuildingButton_Click;
            
            // Remove building button
            Button removeBuildingButton = new Button();
            removeBuildingButton.Text = "Remove";
            removeBuildingButton.Location = new Point(120, 10);
            removeBuildingButton.Size = new Size(100, 30);
            removeBuildingButton.Click += RemoveBuildingButton_Click;
            
            // Set damaged button
            Button setDamagedButton = new Button();
            setDamagedButton.Text = "Set Damaged";
            setDamagedButton.Location = new Point(230, 10);
            setDamagedButton.Size = new Size(100, 30);
            setDamagedButton.Click += SetDamagedButton_Click;
            
            buildingControlsPanel.Controls.Add(addBuildingButton);
            buildingControlsPanel.Controls.Add(removeBuildingButton);
            buildingControlsPanel.Controls.Add(setDamagedButton);
            
            layout.Controls.Add(buildingControlsPanel, 0, 1);
            
            tab.Controls.Add(layout);
        }
        
        private void InitializeNPCsTab(TabPage tab)
        {
            TableLayoutPanel layout = new TableLayoutPanel();
            layout.Dock = DockStyle.Fill;
            layout.ColumnCount = 1;
            layout.RowCount = 2;
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 80F));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 20F));
            
            // NPCs list view
            _npcsListView = new ListView();
            _npcsListView.Dock = DockStyle.Fill;
            _npcsListView.View = View.Details;
            _npcsListView.FullRowSelect = true;
            _npcsListView.MultiSelect = false;
            
            // Add columns
            _npcsListView.Columns.Add("Name", 150);
            _npcsListView.Columns.Add("Type", 100);
            _npcsListView.Columns.Add("Level", 50);
            
            // Add NPCs list view to layout
            layout.Controls.Add(_npcsListView, 0, 0);
            
            // Add NPC controls panel
            Panel npcControlsPanel = new Panel();
            npcControlsPanel.Dock = DockStyle.Fill;
            
            // Add NPC button
            Button addNPCButton = new Button();
            addNPCButton.Text = "Add NPC";
            addNPCButton.Location = new Point(10, 10);
            addNPCButton.Size = new Size(100, 30);
            addNPCButton.Click += AddNPCButton_Click;
            
            // Remove NPC button
            Button removeNPCButton = new Button();
            removeNPCButton.Text = "Remove";
            removeNPCButton.Location = new Point(120, 10);
            removeNPCButton.Size = new Size(100, 30);
            removeNPCButton.Click += RemoveNPCButton_Click;
            
            npcControlsPanel.Controls.Add(addNPCButton);
            npcControlsPanel.Controls.Add(removeNPCButton);
            
            layout.Controls.Add(npcControlsPanel, 0, 1);
            
            tab.Controls.Add(layout);
        }
        
        private void InitializeResourcesTab(TabPage tab)
        {
            TableLayoutPanel layout = new TableLayoutPanel();
            layout.Dock = DockStyle.Fill;
            layout.ColumnCount = 1;
            layout.RowCount = 2;
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 80F));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 20F));
            
            // Resources list view
            _resourcesListView = new ListView();
            _resourcesListView.Dock = DockStyle.Fill;
            _resourcesListView.View = View.Details;
            _resourcesListView.FullRowSelect = true;
            _resourcesListView.MultiSelect = false;
            
            // Add columns
            _resourcesListView.Columns.Add("Resource", 150);
            _resourcesListView.Columns.Add("Amount", 100);
            
            // Add resources list view to layout
            layout.Controls.Add(_resourcesListView, 0, 0);
            
            // Add resource controls panel
            Panel resourceControlsPanel = new Panel();
            resourceControlsPanel.Dock = DockStyle.Fill;
            
            // Edit resource button
            Button editResourceButton = new Button();
            editResourceButton.Text = "Edit Amount";
            editResourceButton.Location = new Point(10, 10);
            editResourceButton.Size = new Size(100, 30);
            editResourceButton.Click += EditResourceButton_Click;
            
            resourceControlsPanel.Controls.Add(editResourceButton);
            
            layout.Controls.Add(resourceControlsPanel, 0, 1);
            
            tab.Controls.Add(layout);
        }
        
        private void InitializeData()
        {
            // No default buildings, NPCs, or resources
            Buildings.Clear();
            NPCs.Clear();
            StartingResources.Clear();

            // Add Griffin's Hunt preset button
            Button griffinsHuntButton = new Button();
            griffinsHuntButton.Text = "Load Griffin's Hunt Preset";
            griffinsHuntButton.Size = new Size(180, 30);
            griffinsHuntButton.Location = new Point(10, 10);
            griffinsHuntButton.Click += GriffinsHuntButton_Click;
            
            // Find the basic info tab
            if (_tabControl.TabPages.Count > 0)
            {
                TabPage basicInfoTab = _tabControl.TabPages[0];
                basicInfoTab.Controls.Add(griffinsHuntButton);
            }
            
            // Refresh lists
            RefreshBuildingsList();
            RefreshNPCsList();
            RefreshResourcesList();
        }
        
        private void RefreshBuildingsList()
        {
            _buildingsListView.Items.Clear();
            
            foreach (var building in Buildings)
            {
                ListViewItem item = new ListViewItem(building.Type.ToString());
                item.SubItems.Add(building.ConstructionStatus.ToString());
                item.SubItems.Add(building.Condition.ToString() + "%");
                item.Tag = building;
                
                // Set color based on building status
                if (building.ConstructionStatus == BuildingStatus.Damaged)
                {
                    item.ForeColor = Color.Red;
                }
                else if (building.ConstructionStatus == BuildingStatus.UnderConstruction)
                {
                    item.ForeColor = Color.Blue;
                }
                
                _buildingsListView.Items.Add(item);
            }
        }
        
        private void RefreshNPCsList()
        {
            _npcsListView.Items.Clear();
            
            foreach (var npc in NPCs)
            {
                ListViewItem item = new ListViewItem(npc.Name);
                item.SubItems.Add(npc.Type.ToString());
                item.SubItems.Add(npc.Level.ToString());
                item.Tag = npc;
                
                _npcsListView.Items.Add(item);
            }
        }
        
        private void RefreshResourcesList()
        {
            _resourcesListView.Items.Clear();
            
            foreach (var resource in StartingResources)
            {
                ListViewItem item = new ListViewItem(resource.Key.ToString());
                item.SubItems.Add(resource.Value.ToString());
                item.Tag = resource.Key;
                
                _resourcesListView.Items.Add(item);
            }
        }
        
        private void AddBuilding(BuildingType type, BuildingStatus status = BuildingStatus.Planning)
        {
            Building building = new Building(type);
            building.ConstructionStatus = status;
            Buildings.Add(building);
            RefreshBuildingsList();
        }
        
        private void AddNPC(NPCType type)
        {
            NPC npc = new NPC(type);
            NPCs.Add(npc);
            RefreshNPCsList();
        }
        
        private void AddBuildingButton_Click(object sender, EventArgs e)
        {
            // Show building type selection dialog
            using (var dialog = new ComboBoxDialog("Select Building Type", "Building Type:", Enum.GetNames(typeof(BuildingType))))
            {
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    BuildingType type = (BuildingType)Enum.Parse(typeof(BuildingType), dialog.SelectedValue);
                    
                    // Show building status selection dialog
                    using (var statusDialog = new ComboBoxDialog("Select Building Status", "Building Status:", Enum.GetNames(typeof(BuildingStatus))))
                    {
                        if (statusDialog.ShowDialog() == DialogResult.OK)
                        {
                            BuildingStatus status = (BuildingStatus)Enum.Parse(typeof(BuildingStatus), statusDialog.SelectedValue);
                            AddBuilding(type, status);
                        }
                    }
                }
            }
        }
        
        private void RemoveBuildingButton_Click(object sender, EventArgs e)
        {
            if (_buildingsListView.SelectedItems.Count > 0)
            {
                Building building = (Building)_buildingsListView.SelectedItems[0].Tag;
                Buildings.Remove(building);
                RefreshBuildingsList();
            }
        }
        
        private void SetDamagedButton_Click(object sender, EventArgs e)
        {
            if (_buildingsListView.SelectedItems.Count > 0)
            {
                Building building = (Building)_buildingsListView.SelectedItems[0].Tag;
                
                // Show damage amount dialog
                using (var dialog = new NumericInputDialog("Set Damage", "Damage Amount (0-100):", 0, 100, 50))
                {
                    if (dialog.ShowDialog() == DialogResult.OK)
                    {
                        int damageAmount = dialog.Value;
                        building.Damage(damageAmount);
                        RefreshBuildingsList();
                    }
                }
            }
        }
        
        private void AddNPCButton_Click(object sender, EventArgs e)
        {
            // Show NPC type selection dialog
            using (var dialog = new ComboBoxDialog("Select NPC Type", "NPC Type:", Enum.GetNames(typeof(NPCType))))
            {
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    NPCType type = (NPCType)Enum.Parse(typeof(NPCType), dialog.SelectedValue);
                    AddNPC(type);
                }
            }
        }
        
        private void RemoveNPCButton_Click(object sender, EventArgs e)
        {
            if (_npcsListView.SelectedItems.Count > 0)
            {
                NPC npc = (NPC)_npcsListView.SelectedItems[0].Tag;
                NPCs.Remove(npc);
                RefreshNPCsList();
            }
        }
        
        private void EditResourceButton_Click(object sender, EventArgs e)
        {
            if (_resourcesListView.SelectedItems.Count > 0)
            {
                ResourceType resourceType = (ResourceType)_resourcesListView.SelectedItems[0].Tag;
                int currentAmount = StartingResources[resourceType];
                
                // Show resource amount dialog
                using (var dialog = new NumericInputDialog("Edit Resource Amount", "Amount:", 0, 10000, currentAmount))
                {
                    if (dialog.ShowDialog() == DialogResult.OK)
                    {
                        StartingResources[resourceType] = dialog.Value;
                        RefreshResourcesList();
                    }
                }
            }
        }
        
        private void OkButton_Click(object sender, EventArgs e)
        {
            // Get stronghold name and location
            StrongholdName = _nameTextBox.Text;
            StrongholdLocation = _locationTextBox.Text;
            
            // Get selected buildings
            Buildings.Clear();
            foreach (ListViewItem item in _buildingsListView.Items)
            {
                if (item.Checked)
                {
                    Buildings.Add((Building)item.Tag);
                }
            }
            
            // Get selected NPCs
            NPCs.Clear();
            foreach (ListViewItem item in _npcsListView.Items)
            {
                if (item.Checked)
                {
                    NPCs.Add((NPC)item.Tag);
                }
            }
        }
        
        private void GriffinsHuntButton_Click(object sender, EventArgs e)
        {
            // Set stronghold name and location
            _nameTextBox.Text = "Griffin's Hunt";
            _locationTextBox.Text = "Araskal Highlands";
            
            // Clear existing buildings and NPCs
            Buildings.Clear();
            _buildingsListView.Items.Clear();
            
            NPCs.Clear();
            _npcsListView.Items.Clear();
            
            // Add damaged buildings
            AddBuilding(BuildingType.Barracks, BuildingStatus.Damaged);
            Building mainHall = _buildingsListView.Items[_buildingsListView.Items.Count - 1].Tag as Building;
            if (mainHall != null)
            {
                mainHall.Name = "Main Hall";
                mainHall.Condition = 20;
            }
            
            AddBuilding(BuildingType.Watchtower, BuildingStatus.Damaged);
            Building watchtower = _buildingsListView.Items[_buildingsListView.Items.Count - 1].Tag as Building;
            if (watchtower != null)
            {
                watchtower.Name = "Northeast Watchtower";
                watchtower.Condition = 15;
            }
            
            AddBuilding(BuildingType.Stables, BuildingStatus.Damaged);
            Building stables = _buildingsListView.Items[_buildingsListView.Items.Count - 1].Tag as Building;
            if (stables != null)
            {
                stables.Name = "Stables";
                stables.Condition = 30;
            }
            
            // Add one intact building
            AddBuilding(BuildingType.Farm, BuildingStatus.Complete);
            Building farm = _buildingsListView.Items[_buildingsListView.Items.Count - 1].Tag as Building;
            if (farm != null)
            {
                farm.Name = "Small Garden";
            }
            
            // Add NPCs
            AddNPC(NPCType.Militia);
            NPC captain = _npcsListView.Items[_npcsListView.Items.Count - 1].Tag as NPC;
            if (captain != null)
            {
                captain.Name = "Captain Harrick";
                captain.Level = 3;
            }
            
            AddNPC(NPCType.Scout);
            NPC scout = _npcsListView.Items[_npcsListView.Items.Count - 1].Tag as NPC;
            if (scout != null)
            {
                scout.Name = "Elara Swiftfoot";
                scout.Level = 2;
            }
            
            AddNPC(NPCType.Laborer);
            NPC laborer = _npcsListView.Items[_npcsListView.Items.Count - 1].Tag as NPC;
            if (laborer != null)
            {
                laborer.Name = "Tormund";
                laborer.Level = 2;
            }
            
            AddNPC(NPCType.Farmer);
            NPC farmer = _npcsListView.Items[_npcsListView.Items.Count - 1].Tag as NPC;
            if (farmer != null)
            {
                farmer.Name = "Old Bess";
                farmer.Level = 2;
            }
            
            // Set starting resources (limited due to damaged state)
            StartingResources.Clear();
            StartingResources[ResourceType.Gold] = 200;
            StartingResources[ResourceType.Food] = 50;
            StartingResources[ResourceType.Wood] = 30;
            StartingResources[ResourceType.Stone] = 15;
            StartingResources[ResourceType.Iron] = 5;
            
            // Refresh lists
            RefreshBuildingsList();
            RefreshNPCsList();
            RefreshResourcesList();
            
            // Check all items by default
            foreach (ListViewItem item in _buildingsListView.Items)
            {
                item.Checked = true;
            }
            
            foreach (ListViewItem item in _npcsListView.Items)
            {
                item.Checked = true;
            }
            
            // Show message
            MessageBox.Show(
                "Griffin's Hunt was once a proud border fort in the Araskal Highlands, " +
                "but it was recently damaged in an attack by hill giants. " +
                "The lord has granted it to your party to restore and manage. " +
                "The fort is in disrepair with most buildings damaged, " +
                "but a small staff remains loyal and ready to help rebuild.",
                "Griffin's Hunt Background",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information
            );
        }
    }
    
    // Helper dialog for selecting from a combo box
    public class ComboBoxDialog : Form
    {
        private ComboBox _comboBox;
        public string SelectedValue { get; private set; }
        
        public ComboBoxDialog(string title, string prompt, string[] items)
        {
            this.Text = title;
            this.Size = new Size(300, 150);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ShowInTaskbar = false;
            
            Label promptLabel = new Label();
            promptLabel.Text = prompt;
            promptLabel.Location = new Point(10, 10);
            promptLabel.Size = new Size(280, 20);
            
            _comboBox = new ComboBox();
            _comboBox.Location = new Point(10, 40);
            _comboBox.Size = new Size(280, 20);
            _comboBox.DropDownStyle = ComboBoxStyle.DropDownList;
            _comboBox.Items.AddRange(items);
            if (items.Length > 0)
                _comboBox.SelectedIndex = 0;
            
            Button okButton = new Button();
            okButton.Text = "OK";
            okButton.DialogResult = DialogResult.OK;
            okButton.Location = new Point(130, 80);
            okButton.Size = new Size(75, 23);
            okButton.Click += (s, e) => SelectedValue = _comboBox.SelectedItem.ToString();
            
            Button cancelButton = new Button();
            cancelButton.Text = "Cancel";
            cancelButton.DialogResult = DialogResult.Cancel;
            cancelButton.Location = new Point(215, 80);
            cancelButton.Size = new Size(75, 23);
            
            this.Controls.Add(promptLabel);
            this.Controls.Add(_comboBox);
            this.Controls.Add(okButton);
            this.Controls.Add(cancelButton);
            
            this.AcceptButton = okButton;
            this.CancelButton = cancelButton;
        }
    }
    
    // Helper dialog for numeric input
    public class NumericInputDialog : Form
    {
        private NumericUpDown _numericUpDown;
        public int Value => (int)_numericUpDown.Value;
        
        public NumericInputDialog(string title, string prompt, int min, int max, int defaultValue)
        {
            this.Text = title;
            this.Size = new Size(300, 150);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ShowInTaskbar = false;
            
            Label promptLabel = new Label();
            promptLabel.Text = prompt;
            promptLabel.Location = new Point(10, 10);
            promptLabel.Size = new Size(280, 20);
            
            _numericUpDown = new NumericUpDown();
            _numericUpDown.Location = new Point(10, 40);
            _numericUpDown.Size = new Size(280, 20);
            _numericUpDown.Minimum = min;
            _numericUpDown.Maximum = max;
            _numericUpDown.Value = defaultValue;
            
            Button okButton = new Button();
            okButton.Text = "OK";
            okButton.DialogResult = DialogResult.OK;
            okButton.Location = new Point(130, 80);
            okButton.Size = new Size(75, 23);
            
            Button cancelButton = new Button();
            cancelButton.Text = "Cancel";
            cancelButton.DialogResult = DialogResult.Cancel;
            cancelButton.Location = new Point(215, 80);
            cancelButton.Size = new Size(75, 23);
            
            this.Controls.Add(promptLabel);
            this.Controls.Add(_numericUpDown);
            this.Controls.Add(okButton);
            this.Controls.Add(cancelButton);
            
            this.AcceptButton = okButton;
            this.CancelButton = cancelButton;
        }
    }
} 