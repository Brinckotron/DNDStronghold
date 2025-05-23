using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using DNDStrongholdApp.Models;

namespace DNDStrongholdApp
{
    public class AddBuildingDialog : Form
    {
        private ListView _buildingTypesListView;
        private TextBox _descriptionTextBox;
        private Label _costLabel;
        private Label _timeLabel;
        private Label _workersLabel;
        private Label _productionLabel;
        private Label _upkeepLabel;
        private TextBox _nameTextBox;
        
        public BuildingType SelectedBuildingType { get; private set; }
        public string BuildingName { get; private set; }
        
        public AddBuildingDialog()
        {
            InitializeComponent();
            LoadBuildingTypes();
        }
        
        private void InitializeComponent()
        {
            this.Text = "Select Building Type";
            this.Size = new Size(600, 550); // Made taller to accommodate name field
            this.StartPosition = FormStartPosition.CenterParent;
            this.MinimizeBox = false;
            this.MaximizeBox = false;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.ShowInTaskbar = false;
            
            // Create layout
            TableLayoutPanel layout = new TableLayoutPanel();
            layout.Dock = DockStyle.Fill;
            layout.ColumnCount = 2;
            layout.RowCount = 3; // Added row for name input
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60F));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 10F)); // Name input row
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 70F)); // Main content
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 20F)); // Buttons
            
            // Name input panel
            Panel namePanel = new Panel();
            namePanel.Dock = DockStyle.Fill;
            
            Label nameLabel = new Label();
            nameLabel.Text = "Building Name:";
            nameLabel.Location = new Point(10, 15);
            nameLabel.AutoSize = true;
            
            _nameTextBox = new TextBox();
            _nameTextBox.Location = new Point(150, 12);
            _nameTextBox.Size = new Size(200, 20);
            
            namePanel.Controls.Add(nameLabel);
            namePanel.Controls.Add(_nameTextBox);
            
            // Building types list
            GroupBox buildingTypesGroup = new GroupBox();
            buildingTypesGroup.Text = "Building Types";
            buildingTypesGroup.Dock = DockStyle.Fill;
            
            _buildingTypesListView = new ListView();
            _buildingTypesListView.Dock = DockStyle.Fill;
            _buildingTypesListView.View = View.Details;
            _buildingTypesListView.FullRowSelect = true;
            _buildingTypesListView.MultiSelect = false;
            _buildingTypesListView.HideSelection = false;
            
            // Add columns
            _buildingTypesListView.Columns.Add("Type", 150);
            
            _buildingTypesListView.SelectedIndexChanged += BuildingTypesListView_SelectedIndexChanged;
            
            buildingTypesGroup.Controls.Add(_buildingTypesListView);
            
            // Building details panel
            GroupBox detailsGroup = new GroupBox();
            detailsGroup.Text = "Building Details";
            detailsGroup.Dock = DockStyle.Fill;
            
            TableLayoutPanel detailsLayout = new TableLayoutPanel();
            detailsLayout.Dock = DockStyle.Fill;
            detailsLayout.ColumnCount = 1;
            detailsLayout.RowCount = 6;
            
            // Description
            _descriptionTextBox = new TextBox();
            _descriptionTextBox.Multiline = true;
            _descriptionTextBox.ReadOnly = true;
            _descriptionTextBox.Dock = DockStyle.Fill;
            _descriptionTextBox.ScrollBars = ScrollBars.Vertical;
            
            // Cost
            _costLabel = new Label();
            _costLabel.Dock = DockStyle.Fill;
            _costLabel.Text = "Cost: ";
            
            // Construction time
            _timeLabel = new Label();
            _timeLabel.Dock = DockStyle.Fill;
            _timeLabel.Text = "Construction Time: ";
            
            // Worker slots
            _workersLabel = new Label();
            _workersLabel.Dock = DockStyle.Fill;
            _workersLabel.Text = "Worker Slots: ";
            
            // Production
            _productionLabel = new Label();
            _productionLabel.Dock = DockStyle.Fill;
            _productionLabel.Text = "Production: ";
            
            // Upkeep
            _upkeepLabel = new Label();
            _upkeepLabel.Dock = DockStyle.Fill;
            _upkeepLabel.Text = "Upkeep: ";
            
            // Add controls to details layout
            detailsLayout.Controls.Add(_descriptionTextBox, 0, 0);
            detailsLayout.Controls.Add(_costLabel, 0, 1);
            detailsLayout.Controls.Add(_timeLabel, 0, 2);
            detailsLayout.Controls.Add(_workersLabel, 0, 3);
            detailsLayout.Controls.Add(_productionLabel, 0, 4);
            detailsLayout.Controls.Add(_upkeepLabel, 0, 5);
            
            // Set row styles
            detailsLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 40F));
            detailsLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 12F));
            detailsLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 12F));
            detailsLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 12F));
            detailsLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 12F));
            detailsLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 12F));
            
            detailsGroup.Controls.Add(detailsLayout);
            
            // Button panel
            Panel buttonPanel = new Panel();
            buttonPanel.Dock = DockStyle.Fill;
            
            Button okButton = new Button();
            okButton.Text = "OK";
            okButton.DialogResult = DialogResult.OK;
            okButton.Size = new Size(80, 30);
            okButton.Location = new Point(buttonPanel.Width - 180, 10);
            okButton.Click += OkButton_Click;
            okButton.Enabled = false; // Disabled until a building type is selected
            
            Button cancelButton = new Button();
            cancelButton.Text = "Cancel";
            cancelButton.DialogResult = DialogResult.Cancel;
            cancelButton.Size = new Size(80, 30);
            cancelButton.Location = new Point(buttonPanel.Width - 90, 10);
            
            buttonPanel.Controls.Add(okButton);
            buttonPanel.Controls.Add(cancelButton);
            
            // Add controls to layout
            layout.Controls.Add(namePanel, 0, 0);
            layout.SetColumnSpan(namePanel, 2);
            layout.Controls.Add(buildingTypesGroup, 0, 1);
            layout.Controls.Add(detailsGroup, 1, 1);
            layout.Controls.Add(buttonPanel, 0, 2);
            layout.SetColumnSpan(buttonPanel, 2);
            
            this.Controls.Add(layout);
            this.AcceptButton = okButton;
            this.CancelButton = cancelButton;
        }
        
        private void LoadBuildingTypes()
        {
            // Get all building types from enum
            var buildingTypes = Enum.GetValues(typeof(BuildingType)).Cast<BuildingType>();
            
            foreach (var type in buildingTypes)
            {
                ListViewItem item = new ListViewItem(type.ToString());
                item.Tag = type;
                _buildingTypesListView.Items.Add(item);
            }
        }
        
        private void BuildingTypesListView_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_buildingTypesListView.SelectedItems.Count > 0)
            {
                BuildingType selectedType = (BuildingType)_buildingTypesListView.SelectedItems[0].Tag;
                
                // Create temporary building to get details
                Building tempBuilding = new Building(selectedType);
                
                // Update description
                _descriptionTextBox.Text = GetBuildingDescription(selectedType);
                
                // Update cost
                string costText = "Cost: ";
                foreach (var cost in tempBuilding.ConstructionCost)
                {
                    costText += $"{cost.ResourceType}: {cost.Amount}, ";
                }
                _costLabel.Text = costText.TrimEnd(',', ' ');
                
                // Update construction time
                _timeLabel.Text = $"Construction Time: {tempBuilding.ConstructionTimeRemaining} weeks";
                
                // Update worker slots
                _workersLabel.Text = $"Worker Slots: {tempBuilding.WorkerSlots}";
                
                // Update production
                string productionText = "Production: ";
                if (tempBuilding.BaseProduction.Count > 0)
                {
                    foreach (var production in tempBuilding.BaseProduction)
                    {
                        productionText += $"{production.ResourceType}: +{production.Amount}/week, ";
                    }
                }
                else
                {
                    productionText += "None";
                }
                _productionLabel.Text = productionText.TrimEnd(',', ' ');
                
                // Update upkeep
                string upkeepText = "Upkeep: ";
                if (tempBuilding.BaseUpkeep.Count > 0)
                {
                    foreach (var upkeep in tempBuilding.BaseUpkeep)
                    {
                        upkeepText += $"{upkeep.ResourceType}: -{upkeep.Amount}/week, ";
                    }
                }
                else
                {
                    upkeepText += "None";
                }
                _upkeepLabel.Text = upkeepText.TrimEnd(',', ' ');
                
                // Store selected building type
                SelectedBuildingType = selectedType;
                
                // Enable OK button
                Button okButton = (Button)this.AcceptButton;
                if (okButton != null)
                {
                    okButton.Enabled = true;
                }
            }
        }
        
        private string GetBuildingDescription(BuildingType type)
        {
            switch (type)
            {
                case BuildingType.Farm:
                    return "A farm produces food for your stronghold. Each farm can support several workers who tend to crops and livestock.";
                    
                case BuildingType.Watchtower:
                    return "A watchtower provides visibility of the surrounding area, helping to spot threats before they arrive. Guards stationed here can warn of approaching danger.";
                    
                case BuildingType.Smithy:
                    return "A smithy allows for the production of weapons, armor, and tools. Skilled blacksmiths can create items of higher quality.";
                    
                case BuildingType.Laboratory:
                    return "A laboratory enables magical research and alchemical experiments. Wizards and alchemists can create potions and magical items here.";
                    
                case BuildingType.Chapel:
                    return "A chapel dedicated to a deity provides spiritual guidance and healing. Clerics can perform rituals and bless the stronghold.";
                    
                case BuildingType.Mine:
                    return "A mine extracts valuable ore and stone from the earth. Miners can produce iron, stone, and occasionally precious metals.";
                    
                case BuildingType.Barracks:
                    return "Barracks house and train your military forces. Soldiers stationed here can defend the stronghold or be sent on missions.";
                    
                case BuildingType.Library:
                    return "A library stores knowledge in books and scrolls. Scholars can research new technologies and magical discoveries.";
                    
                case BuildingType.TradeOffice:
                    return "A trade office manages commercial relations with other settlements. Merchants can generate gold through trade deals.";
                    
                case BuildingType.Stables:
                    return "Stables house and breed mounts and pack animals. Handlers can train animals for riding, hauling, or combat.";
                    
                case BuildingType.Tavern:
                    return "A tavern provides entertainment and relaxation for your people. It can attract visitors with news and rumors.";
                    
                case BuildingType.MasonsYard:
                    return "A mason's yard allows for the creation of stone structures. Masons can build and repair stone buildings more efficiently.";
                    
                case BuildingType.Workshop:
                    return "A workshop enables the creation of furniture, tools, and other wooden items. Carpenters can craft various useful items.";
                    
                case BuildingType.Granary:
                    return "A granary stores food to prevent spoilage. It increases your food storage capacity and helps survive lean seasons.";
                    
                case BuildingType.Quarry:
                    return "A quarry extracts stone from the earth. Workers can produce stone at a base rate, with additional production based on their labor skill. The quarry can be upgraded to increase worker capacity and production efficiency.";
                    
                default:
                    return "No description available.";
            }
        }
        
        private void OkButton_Click(object sender, EventArgs e)
        {
            // Store the building name
            BuildingName = _nameTextBox.Text.Trim();
            // SelectedBuildingType is already set in the SelectedIndexChanged event
        }
    }
} 