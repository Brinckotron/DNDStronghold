using System;
using System.Windows.Forms;
using DNDStrongholdApp.Models;
using System.Linq;
using DNDStrongholdApp.Services;
using System.Drawing;
using System.Collections.Generic;
using DNDStrongholdApp.Forms;

namespace DNDStrongholdApp.Forms
{
    public class AddBuildingForm : Form
    {
        private readonly Stronghold _stronghold;
        private readonly GameStateService _gameStateService;
        private ComboBox _buildingTypeComboBox;
        private TextBox _nameTextBox;
        private TextBox _descriptionTextBox;
        private RichTextBox _costLabel;
        private Button _addButton;
        private Button _cancelButton;

        // DM Mode controls
        private Label _dmModeLabel;
        private NumericUpDown _levelNumeric;
        private ComboBox _stateComboBox;
        private NumericUpDown _constructionPointsNumeric;
        private Label _constructionPointsCostLabel;
        private NumericUpDown _conditionNumeric;
        private Panel _dmModePanel;

        public AddBuildingForm(Stronghold stronghold)
        {
            _stronghold = stronghold;
            _gameStateService = GameStateService.GetInstance();
            InitializeComponents();
        }

        private void InitializeComponents()
        {
            this.Text = "Add New Building";
            // Make form larger when in DM Mode
            this.Size = _gameStateService.DMMode ? 
                new System.Drawing.Size(500, 600) : 
                new System.Drawing.Size(500, 450);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterParent;

            // DM Mode Label (only visible in DM Mode)
            if (_gameStateService.DMMode)
            {
                _dmModeLabel = new Label
                {
                    Text = "DM MODE ON",
                    Location = new Point(20, 10),
                    AutoSize = true,
                    ForeColor = Color.DarkRed,
                    Font = new Font(this.Font, FontStyle.Bold)
                };
                this.Controls.Add(_dmModeLabel);
            }

            // Building Type ComboBox
            Label typeLabel = new Label
            {
                Text = "Building Type:",
                Location = new Point(20, _gameStateService.DMMode ? 40 : 20),
                AutoSize = true
            };

            _buildingTypeComboBox = new ComboBox
            {
                Location = new Point(20, typeLabel.Bottom + 5),
                Width = 440,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            _buildingTypeComboBox.Items.AddRange(Enum.GetNames(typeof(BuildingType)));
            _buildingTypeComboBox.SelectedIndexChanged += BuildingType_SelectedIndexChanged;

            // Name TextBox
            Label nameLabel = new Label
            {
                Text = "Building Name:",
                Location = new Point(20, _buildingTypeComboBox.Bottom + 20),
                AutoSize = true
            };

            _nameTextBox = new TextBox
            {
                Location = new Point(20, nameLabel.Bottom + 5),
                Width = 440
            };

            // Description TextBox
            Label descriptionLabel = new Label
            {
                Text = "Description:",
                Location = new Point(20, _nameTextBox.Bottom + 20),
                AutoSize = true
            };

            _descriptionTextBox = new TextBox
            {
                Location = new Point(20, descriptionLabel.Bottom + 5),
                Width = 440,
                Height = 80,
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                ReadOnly = true,
                BackColor = SystemColors.Window
            };

            // DM Mode Panel
            if (_gameStateService.DMMode)
            {
                _dmModePanel = new Panel
                {
                    Location = new Point(20, _descriptionTextBox.Bottom + 20),
                    Width = 440,
                    Height = 120,
                    BorderStyle = BorderStyle.FixedSingle
                };

                // Level selector
                Label levelLabel = new Label
                {
                    Text = "Level:",
                    Location = new Point(10, 10),
                    AutoSize = true
                };

                _levelNumeric = new NumericUpDown
                {
                    Location = new Point(120, 8),
                    Width = 60,
                    Minimum = 1,
                    Maximum = 10,
                    Value = 1
                };

                Label maxLevelLabel = new Label
                {
                    Text = "(Max: 1)",
                    Location = new Point(190, 10),
                    AutoSize = true,
                    ForeColor = Color.DarkGray
                };

                // State selector
                Label stateLabel = new Label
                {
                    Text = "State:",
                    Location = new Point(10, 40),
                    AutoSize = true
                };

                _stateComboBox = new ComboBox
                {
                    Location = new Point(120, 37),
                    Width = 150,
                    DropDownStyle = ComboBoxStyle.DropDownList
                };
                _stateComboBox.Items.AddRange(Enum.GetNames(typeof(BuildingStatus)));
                _stateComboBox.SelectedIndex = 0;
                _stateComboBox.SelectedIndexChanged += StateComboBox_SelectedIndexChanged;

                // Construction Points (only visible when UnderConstruction is selected)
                Label constructionPointsLabel = new Label
                {
                    Text = "Construct Pts:",
                    Location = new Point(10, 70),
                    AutoSize = true,
                    Visible = false
                };

                _constructionPointsNumeric = new NumericUpDown
                {
                    Location = new Point(120, 68),
                    Width = 80,
                    Minimum = 0,
                    Maximum = 1000,
                    Visible = false
                };

                _constructionPointsCostLabel = new Label
                {
                    Location = new Point(210, 70),
                    AutoSize = true,
                    Visible = false
                };

                // Condition (only visible when Damaged is selected)
                Label conditionLabel = new Label
                {
                    Text = "Condition:",
                    Location = new Point(10, 70),
                    AutoSize = true,
                    Visible = false
                };

                _conditionNumeric = new NumericUpDown
                {
                    Location = new Point(120, 68),
                    Width = 80,
                    Minimum = 1,
                    Maximum = 99,
                    Value = 50,
                    Visible = false
                };

                _dmModePanel.Controls.AddRange(new Control[] {
                    levelLabel, _levelNumeric, maxLevelLabel,
                    stateLabel, _stateComboBox,
                    constructionPointsLabel, _constructionPointsNumeric, _constructionPointsCostLabel,
                    conditionLabel, _conditionNumeric
                });

                // Store these controls as tags for easy access
                constructionPointsLabel.Tag = "constructionPoints";
                _constructionPointsNumeric.Tag = "constructionPoints";
                _constructionPointsCostLabel.Tag = "constructionPoints";
                conditionLabel.Tag = "condition";
                _conditionNumeric.Tag = "condition";
            }

            // Cost RichTextBox
            _costLabel = new RichTextBox
            {
                Location = new Point(20, _gameStateService.DMMode ? _dmModePanel.Bottom + 10 : _descriptionTextBox.Bottom + 20),
                Width = 250,
                Height = 80,
                ReadOnly = true,
                BackColor = SystemColors.Control,
                BorderStyle = BorderStyle.None
            };

            // Buttons
            _addButton = new Button
            {
                Text = "Add Building",
                DialogResult = DialogResult.OK,
                Location = new Point(290, _costLabel.Top),
                Width = 100,
                Height = 35,
                Enabled = false
            };
            _addButton.Click += AddButton_Click;

            _cancelButton = new Button
            {
                Text = "Cancel",
                DialogResult = DialogResult.Cancel,
                Location = new Point(290, _addButton.Bottom + 10),
                Width = 100,
                Height = 35
            };

            // Add controls to form
            var controls = new List<Control> 
            { 
                typeLabel,
                _buildingTypeComboBox,
                nameLabel,
                _nameTextBox,
                descriptionLabel,
                _descriptionTextBox,
                _costLabel,
                _addButton,
                _cancelButton
            };

            if (_gameStateService.DMMode)
            {
                controls.Add(_dmModePanel);
            }

            this.Controls.AddRange(controls.ToArray());
        }

        private void StateComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (!_gameStateService.DMMode) return;

            var selectedState = (BuildingStatus)Enum.Parse(typeof(BuildingStatus), _stateComboBox.SelectedItem.ToString());

            // Hide all conditional controls first
            foreach (Control control in _dmModePanel.Controls)
            {
                if (control.Tag != null)
                {
                    control.Visible = false;
                }
            }

            // Show relevant controls based on selected state
            switch (selectedState)
            {
                case BuildingStatus.UnderConstruction:
                    foreach (Control control in _dmModePanel.Controls)
                    {
                        if (control.Tag?.ToString() == "constructionPoints")
                        {
                            control.Visible = true;
                        }
                    }
                    break;
                case BuildingStatus.Damaged:
                    foreach (Control control in _dmModePanel.Controls)
                    {
                        if (control.Tag?.ToString() == "condition")
                        {
                            control.Visible = true;
                        }
                    }
                    break;
            }
        }

        private void BuildingType_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_buildingTypeComboBox.SelectedItem == null)
            {
                _addButton.Enabled = false;
                _descriptionTextBox.Text = string.Empty;
                _costLabel.Clear();
                if (_gameStateService.DMMode && _constructionPointsCostLabel != null)
                {
                    _constructionPointsCostLabel.Text = string.Empty;
                    // Update max level label
                    foreach (Control control in _dmModePanel.Controls)
                    {
                        if (control is Label label && label.Text.StartsWith("(Max:"))
                        {
                            label.Text = "(Max: 1)";
                        }
                    }
                }
                return;
            }

            BuildingType selectedType = (BuildingType)Enum.Parse(typeof(BuildingType), _buildingTypeComboBox.SelectedItem.ToString());
            
            // Set description
            _descriptionTextBox.Text = GetBuildingDescription(selectedType);

            // Create temporary building to get costs
            var tempBuilding = new Building(selectedType);
            
            // Update cost display with color coding and current amounts
            _costLabel.Clear();
            _costLabel.AppendText("Construction Costs:\n");
            
            foreach (var cost in tempBuilding.ConstructionCost)
            {
                var resource = _stronghold.Resources.Find(r => r.Type == cost.ResourceType);
                bool hasEnough = resource != null && resource.Amount >= cost.Amount;
                
                // Set color based on availability
                _costLabel.SelectionColor = hasEnough ? Color.Green : Color.Red;
                _costLabel.AppendText($"{cost.ResourceType}: {cost.Amount} ({resource?.Amount ?? 0})\n");
            }

            // Update construction points cost label if in DM Mode
            if (_gameStateService.DMMode && _constructionPointsCostLabel != null)
            {
                _constructionPointsCostLabel.Text = $"(Total: {tempBuilding.RequiredConstructionPoints})";
                _constructionPointsNumeric.Maximum = tempBuilding.RequiredConstructionPoints;

                // Update max level label and level numeric maximum
                foreach (Control control in _dmModePanel.Controls)
                {
                    if (control is Label label && label.Text.StartsWith("(Max:"))
                    {
                        label.Text = $"(Max: {tempBuilding.MaxLevel})";
                        _levelNumeric.Maximum = tempBuilding.MaxLevel;
                    }
                }
            }

            _addButton.Enabled = true;
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
                    return "A quarry extracts stone from the earth. Workers can produce stone at a base rate, with additional production based on their labor skill.";
                default:
                    return "No description available.";
            }
        }

        private void AddButton_Click(object sender, EventArgs e)
        {
            if (_buildingTypeComboBox.SelectedItem == null) return;

            BuildingType selectedType = (BuildingType)Enum.Parse(typeof(BuildingType), _buildingTypeComboBox.SelectedItem.ToString());
            
            // If name is empty, use the building type as the default name
            string buildingName = string.IsNullOrWhiteSpace(_nameTextBox.Text) 
                ? selectedType.ToString() 
                : _nameTextBox.Text.Trim();

            // Create the building
            var building = new Building(selectedType)
            {
                Name = buildingName
            };

            // Apply DM Mode settings if enabled
            if (_gameStateService.DMMode)
            {
                building.Level = (int)_levelNumeric.Value;
                building.ConstructionStatus = (BuildingStatus)Enum.Parse(typeof(BuildingStatus), _stateComboBox.SelectedItem.ToString());

                if (building.ConstructionStatus == BuildingStatus.UnderConstruction)
                {
                    building.CurrentConstructionPoints = (int)_constructionPointsNumeric.Value;
                    building.ConstructionProgress = (int)((float)building.CurrentConstructionPoints / building.RequiredConstructionPoints * 100);
                }
                else if (building.ConstructionStatus == BuildingStatus.Damaged)
                {
                    building.Condition = (int)_conditionNumeric.Value;
                }

                // Add the building without deducting resources in DM Mode
                _gameStateService.AddBuildingAndDeductCosts(building);
                this.DialogResult = DialogResult.OK;
                this.Close();
                return;
            }

            // Normal mode: Add the building and deduct resources
            if (!_gameStateService.AddBuildingAndDeductCosts(building))
            {
                MessageBox.Show("Failed to add building: Insufficient resources", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // Show construction crew assignment dialog for new buildings in Planning state
            if (building.ConstructionStatus == BuildingStatus.Planning)
            {
                using (var crewDialog = new ConstructionCrewAssignmentDialog(_stronghold.NPCs, building, "Construction"))
                {
                    if (crewDialog.ShowDialog() == DialogResult.OK && crewDialog.AssignedCrewIds.Count > 0)
                    {
                        _gameStateService.AssignConstructionCrewToBuilding(building.Id, crewDialog.AssignedCrewIds);
                    }
                }
            }

            this.DialogResult = DialogResult.OK;
            this.Close();
        }
    }
} 