using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using DNDStrongholdApp.Models;
using DNDStrongholdApp.Services;

namespace DNDStrongholdApp.Forms
{
    public class AddNPCDialog : Form
    {
        private readonly GameStateService _gameStateService;
        private readonly BioGeneratorService _bioGenerator;
        
        // UI Controls
        private ComboBox _typeComboBox;
        private ComboBox _genderComboBox;
        private TextBox _nameTextBox;
        private Button _generateNameButton;
        private NumericUpDown _levelNumeric;
        private Panel _skillsPanel;
        private ScrollableControl _skillsContainer;
        private Dictionary<string, SkillControl> _skillControls;
        private TextBox _bioTextBox;
        private Button _generateBioButton;
        private ComboBox _healthStateComboBox;
        private Button _okButton;
        private Button _cancelButton;
        
        // Created NPC data
        public NPC CreatedNPC { get; private set; }
        
        private class SkillControl
        {
            public Label NameLabel { get; set; }
            public NumericUpDown LevelNumeric { get; set; }
            public ProgressBar ProgressBar { get; set; }
            public Label ProgressLabel { get; set; }
        }

        public AddNPCDialog()
        {
            _gameStateService = GameStateService.GetInstance();
            _bioGenerator = new BioGeneratorService();
            _skillControls = new Dictionary<string, SkillControl>();
            
            InitializeComponent();
            PopulateControls();
        }

        private void InitializeComponent()
        {
            this.Text = "Add New NPC";
            this.Size = new Size(650, 700);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterParent;

            // Main layout
            TableLayoutPanel mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 7,
                Padding = new Padding(15)
            };
            
            // Row styles
            mainLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Basic info
            mainLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Name row
            mainLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Level row
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 40F)); // Skills
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 40F)); // Bio
            mainLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Health state
            mainLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Buttons

            // Basic info panel (Type & Gender)
            Panel basicInfoPanel = CreateBasicInfoPanel();
            mainLayout.Controls.Add(basicInfoPanel, 0, 0);

            // Name panel
            Panel namePanel = CreateNamePanel();
            mainLayout.Controls.Add(namePanel, 0, 1);

            // Level panel
            Panel levelPanel = CreateLevelPanel();
            mainLayout.Controls.Add(levelPanel, 0, 2);

            // Skills panel
            GroupBox skillsGroup = CreateSkillsPanel();
            mainLayout.Controls.Add(skillsGroup, 0, 3);

            // Bio panel
            GroupBox bioGroup = CreateBioPanel();
            mainLayout.Controls.Add(bioGroup, 0, 4);

            // Health state panel
            Panel healthPanel = CreateHealthStatePanel();
            mainLayout.Controls.Add(healthPanel, 0, 5);

            // Buttons panel
            Panel buttonsPanel = CreateButtonsPanel();
            mainLayout.Controls.Add(buttonsPanel, 0, 6);

            this.Controls.Add(mainLayout);
            this.AcceptButton = _okButton;
            this.CancelButton = _cancelButton;
        }

        private Panel CreateBasicInfoPanel()
        {
            TableLayoutPanel panel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 4,
                RowCount = 1,
                Height = 35,
                Margin = new Padding(0, 0, 0, 10)
            };
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); // Type label
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F)); // Type combo
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); // Gender label
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F)); // Gender combo

            Label typeLabel = new Label
            {
                Text = "Type:",
                TextAlign = ContentAlignment.MiddleRight,
                AutoSize = true,
                Margin = new Padding(0, 6, 10, 0)
            };

            _typeComboBox = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 3, 20, 3)
            };
            _typeComboBox.SelectedIndexChanged += TypeComboBox_SelectedIndexChanged;

            Label genderLabel = new Label
            {
                Text = "Gender:",
                TextAlign = ContentAlignment.MiddleRight,
                AutoSize = true,
                Margin = new Padding(0, 6, 10, 0)
            };

            _genderComboBox = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 3, 0, 3)
            };
            _genderComboBox.SelectedIndexChanged += GenderComboBox_SelectedIndexChanged;

            panel.Controls.Add(typeLabel, 0, 0);
            panel.Controls.Add(_typeComboBox, 1, 0);
            panel.Controls.Add(genderLabel, 2, 0);
            panel.Controls.Add(_genderComboBox, 3, 0);

            return panel;
        }

        private Panel CreateNamePanel()
        {
            TableLayoutPanel panel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 1,
                Height = 35,
                Margin = new Padding(0, 0, 0, 10)
            };
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); // Label
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F)); // TextBox
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); // Button

            Label nameLabel = new Label
            {
                Text = "Name:",
                TextAlign = ContentAlignment.MiddleRight,
                AutoSize = true,
                Margin = new Padding(0, 6, 10, 0)
            };

            _nameTextBox = new TextBox
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 3, 10, 3)
            };

            _generateNameButton = new Button
            {
                Text = "Generate",
                Width = 80,
                Height = 25,
                Margin = new Padding(0, 3, 0, 3)
            };
            _generateNameButton.Click += GenerateNameButton_Click;

            panel.Controls.Add(nameLabel, 0, 0);
            panel.Controls.Add(_nameTextBox, 1, 0);
            panel.Controls.Add(_generateNameButton, 2, 0);

            return panel;
        }

        private Panel CreateLevelPanel()
        {
            TableLayoutPanel panel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 1,
                Height = 35,
                Margin = new Padding(0, 0, 0, 10)
            };
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); // Label
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); // NumericUpDown
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F)); // Spacer

            Label levelLabel = new Label
            {
                Text = "Level (Max 10):",
                TextAlign = ContentAlignment.MiddleRight,
                AutoSize = true,
                Margin = new Padding(0, 6, 10, 0)
            };

            _levelNumeric = new NumericUpDown
            {
                Minimum = 1,
                Maximum = 10,
                Value = 1,
                Width = 60,
                Margin = new Padding(0, 3, 0, 3)
            };
            _levelNumeric.ValueChanged += LevelNumeric_ValueChanged;

            panel.Controls.Add(levelLabel, 0, 0);
            panel.Controls.Add(_levelNumeric, 1, 0);

            return panel;
        }

        private GroupBox CreateSkillsPanel()
        {
            GroupBox skillsGroup = new GroupBox
            {
                Text = "Skills (Assign skill points based on level)",
                Dock = DockStyle.Fill,
                Padding = new Padding(10)
            };

            _skillsContainer = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true
            };

            skillsGroup.Controls.Add(_skillsContainer);
            return skillsGroup;
        }

        private GroupBox CreateBioPanel()
        {
            GroupBox bioGroup = new GroupBox
            {
                Text = "Biography",
                Dock = DockStyle.Fill,
                Padding = new Padding(10)
            };

            TableLayoutPanel bioLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2
            };
            bioLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F)); // TextBox
            bioLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Button

            _bioTextBox = new TextBox
            {
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 0, 0, 5)
            };

            _generateBioButton = new Button
            {
                Text = "Generate Bio",
                Height = 30,
                Dock = DockStyle.Right,
                Width = 100
            };
            _generateBioButton.Click += GenerateBioButton_Click;

            bioLayout.Controls.Add(_bioTextBox, 0, 0);
            bioLayout.Controls.Add(_generateBioButton, 0, 1);

            bioGroup.Controls.Add(bioLayout);
            return bioGroup;
        }

        private Panel CreateHealthStatePanel()
        {
            TableLayoutPanel panel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Height = 35,
                Margin = new Padding(0, 0, 0, 10)
            };
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); // Label
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F)); // ComboBox

            Label healthLabel = new Label
            {
                Text = "Starting Health State:",
                TextAlign = ContentAlignment.MiddleRight,
                AutoSize = true,
                Margin = new Padding(0, 6, 10, 0)
            };

            _healthStateComboBox = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 200,
                Margin = new Padding(0, 3, 0, 3)
            };

            panel.Controls.Add(healthLabel, 0, 0);
            panel.Controls.Add(_healthStateComboBox, 1, 0);

            return panel;
        }

        private Panel CreateButtonsPanel()
        {
            FlowLayoutPanel buttonsPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                Height = 40,
                Margin = new Padding(0, 10, 0, 0)
            };

            _cancelButton = new Button
            {
                Text = "Cancel",
                DialogResult = DialogResult.Cancel,
                Width = 80,
                Height = 30,
                Margin = new Padding(0, 0, 10, 0)
            };

            _okButton = new Button
            {
                Text = "Create NPC",
                DialogResult = DialogResult.OK,
                Width = 100,
                Height = 30
            };
            _okButton.Click += OkButton_Click;

            buttonsPanel.Controls.Add(_cancelButton);
            buttonsPanel.Controls.Add(_okButton);

            return buttonsPanel;
        }

        private void PopulateControls()
        {
            // Populate type combo
            _typeComboBox.Items.AddRange(Enum.GetNames(typeof(NPCType)));
            _typeComboBox.SelectedIndex = 0;

            // Populate gender combo
            _genderComboBox.Items.AddRange(Enum.GetNames(typeof(NPCGender)));
            _genderComboBox.SelectedIndex = 0;

            // Populate health state combo
            _healthStateComboBox.Items.Add("Healthy");
            _healthStateComboBox.Items.AddRange(Enum.GetNames(typeof(NPCStateType)));
            _healthStateComboBox.SelectedIndex = 0;

            // Update level constraints for initial type
            UpdateLevelConstraints();

            // Initialize skills
            CreateSkillControls();
            
            // Generate initial name
            GenerateRandomName();
        }

        private void CreateSkillControls()
        {
            _skillsContainer.Controls.Clear();
            _skillControls.Clear();

            int yPosition = 10;
            int maxLevel = (int)_levelNumeric.Value;

            // Get mandatory skills before creating controls
            var mandatorySkills = GetMandatorySkills();

            // Basic skills
            foreach (string skillName in Enum.GetNames(typeof(BasicSkill)))
            {
                var skillControl = CreateSkillControl(skillName, yPosition, maxLevel);
                
                // Set mandatory skill level if applicable
                if (mandatorySkills.ContainsKey(skillName))
                {
                    skillControl.LevelNumeric.Value = mandatorySkills[skillName];
                }
                
                _skillControls[skillName] = skillControl;
                yPosition += 35;
            }

            // Advanced skills
            foreach (string skillName in Enum.GetNames(typeof(AdvancedSkill)))
            {
                var skillControl = CreateSkillControl(skillName, yPosition, maxLevel);
                
                // Set mandatory skill level if applicable
                if (mandatorySkills.ContainsKey(skillName))
                {
                    skillControl.LevelNumeric.Value = mandatorySkills[skillName];
                }
                
                _skillControls[skillName] = skillControl;
                yPosition += 35;
            }

            UpdateSkillConstraints();
        }

        private SkillControl CreateSkillControl(string skillName, int yPosition, int maxLevel)
        {
            var skillControl = new SkillControl();

            // Name label
            skillControl.NameLabel = new Label
            {
                Text = skillName + ":",
                Location = new Point(10, yPosition + 5),
                Width = 120,
                TextAlign = ContentAlignment.MiddleRight
            };

            // Level numeric
            skillControl.LevelNumeric = new NumericUpDown
            {
                Location = new Point(140, yPosition),
                Width = 60,
                Minimum = 0,
                Maximum = maxLevel,
                Value = 0,
                ReadOnly = true
            };
            skillControl.LevelNumeric.ValueChanged += (s, e) => UpdateSkillConstraints();

            // Progress bar (visual indicator)
            skillControl.ProgressBar = new ProgressBar
            {
                Location = new Point(210, yPosition + 2),
                Width = 100,
                Height = 20,
                Maximum = maxLevel,
                Value = 0
            };

            // Progress label
            skillControl.ProgressLabel = new Label
            {
                Location = new Point(320, yPosition + 5),
                Width = 80,
                Text = "0 / " + maxLevel
            };

            // Add event handler to update progress bar and validate skill points
            skillControl.LevelNumeric.ValueChanged += (s, e) =>
            {
                skillControl.ProgressBar.Value = (int)skillControl.LevelNumeric.Value;
                skillControl.ProgressLabel.Text = $"{(int)skillControl.LevelNumeric.Value} / {maxLevel}";
                
                // Validate total skill points in real-time
                ValidateSkillPointAllocation();
            };

            _skillsContainer.Controls.Add(skillControl.NameLabel);
            _skillsContainer.Controls.Add(skillControl.LevelNumeric);
            _skillsContainer.Controls.Add(skillControl.ProgressBar);
            _skillsContainer.Controls.Add(skillControl.ProgressLabel);

            return skillControl;
        }

        private void UpdateLevelConstraints()
        {
            if (_typeComboBox.SelectedItem == null) return;
            
            var selectedType = (NPCType)Enum.Parse(typeof(NPCType), _typeComboBox.SelectedItem.ToString());
            
            // Peasants can start at level 0, others start at minimum level 1
            if (selectedType == NPCType.Peasant)
            {
                _levelNumeric.Minimum = 0;
                _levelNumeric.Value = 0; // Set default to 0 for Peasants
            }
            else
            {
                _levelNumeric.Minimum = 1;
                if (_levelNumeric.Value < 1) _levelNumeric.Value = 1;
            }
        }

        private void UpdateSkillConstraints()
        {
            int maxLevel = (int)_levelNumeric.Value;
            int totalSkillPoints = _skillControls.Values.Sum(sc => (int)sc.LevelNumeric.Value);

            // Get mandatory skills and their levels
            var mandatorySkills = GetMandatorySkills();

            // Update maximum levels and constraints
            foreach (var kvp in _skillControls)
            {
                var skillName = kvp.Key;
                var skillControl = kvp.Value;
                skillControl.LevelNumeric.Maximum = maxLevel;
                skillControl.ProgressBar.Maximum = maxLevel;
                skillControl.ProgressLabel.Text = $"{(int)skillControl.LevelNumeric.Value} / {maxLevel}";
                
                // Check if this is a mandatory skill
                bool isMandatory = mandatorySkills.ContainsKey(skillName);
                
                if (isMandatory)
                {
                    // Set mandatory level and disable control
                    int mandatoryLevel = mandatorySkills[skillName];
                    skillControl.LevelNumeric.Value = mandatoryLevel;
                    skillControl.LevelNumeric.Enabled = false;
                    skillControl.NameLabel.ForeColor = Color.Blue; // Visual indicator
                }
                else
                {
                    // Enable control and reset color
                    skillControl.LevelNumeric.Enabled = true;
                    skillControl.NameLabel.ForeColor = SystemColors.ControlText;
                }
            }

            // Calculate remaining points once for the whole method
            int remainingPoints = maxLevel - totalSkillPoints;
            
            // Apply remaining points logic to non-mandatory skills
            foreach (var kvp in _skillControls)
            {
                var skillName = kvp.Key;
                var skillControl = kvp.Value;
                bool isMandatory = mandatorySkills.ContainsKey(skillName);
                
                if (!isMandatory)
                {
                    int currentValue = (int)skillControl.LevelNumeric.Value;
                    
                    if (remainingPoints <= 0 && currentValue == 0)
                    {
                        // No remaining points and this skill is at 0, disable it
                        skillControl.LevelNumeric.Enabled = false;
                    }
                    else if (remainingPoints < 0)
                    {
                        // Over limit - this shouldn't happen but handle it
                        skillControl.LevelNumeric.Enabled = true;
                        skillControl.NameLabel.ForeColor = Color.Red;
                    }
                }
            }

            // Update title to show remaining points with color coding
            var skillsGroup = _skillsContainer.Parent as GroupBox;
            if (skillsGroup != null)
            {
                string titleText = $"Skills (Remaining Points: {remainingPoints} / {maxLevel})";
                skillsGroup.Text = titleText;
                
                // Color code the group box title if over limit
                if (remainingPoints < 0)
                {
                    skillsGroup.ForeColor = Color.Red;
                }
                else
                {
                    skillsGroup.ForeColor = SystemColors.ControlText;
                }
            }
        }

        private Dictionary<string, int> GetMandatorySkills()
        {
            var mandatorySkills = new Dictionary<string, int>();
            
            if (_typeComboBox.SelectedItem == null) return mandatorySkills;
            
            var selectedType = (NPCType)Enum.Parse(typeof(NPCType), _typeComboBox.SelectedItem.ToString());
            
            // Based on NPC.cs InitializeSkills() method
            switch (selectedType)
            {
                case NPCType.Peasant:
                    // Peasants have no mandatory skills
                    break;
                    
                case NPCType.Laborer:
                    mandatorySkills["Construction"] = 1;
                    break;
                    
                case NPCType.Farmer:
                    mandatorySkills["Farming"] = 1;
                    break;
                    
                case NPCType.Militia:
                    mandatorySkills["Combat"] = 1;
                    break;
                    
                case NPCType.Scout:
                    mandatorySkills["Survival"] = 1;
                    break;
                    
                case NPCType.Artisan:
                    mandatorySkills["Crafting"] = 1;
                    break;
                    
                case NPCType.Scholar:
                    mandatorySkills["Lore"] = 1;
                    break;
                    
                case NPCType.Merchant:
                    mandatorySkills["Trade"] = 1;
                    break;
            }
            
            return mandatorySkills;
        }

        private void GenerateRandomName()
        {
            try
            {
                var gender = (NPCGender)Enum.Parse(typeof(NPCGender), _genderComboBox.SelectedItem.ToString());
                var type = (NPCType)Enum.Parse(typeof(NPCType), _typeComboBox.SelectedItem.ToString());
                
                // Create a temporary NPC to generate name
                var tempNPC = new NPC(type);
                tempNPC.Gender = gender;
                
                // Use reflection to call the private GenerateRandomName method
                var method = typeof(NPC).GetMethod("GenerateRandomName", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (method != null)
                {
                    string generatedName = (string)method.Invoke(tempNPC, null);
                    _nameTextBox.Text = generatedName;
                }
                else
                {
                    // Fallback name generation
                    _nameTextBox.Text = GenerateFallbackName(gender);
                }
            }
            catch
            {
                _nameTextBox.Text = GenerateFallbackName(NPCGender.Male);
            }
        }

        private string GenerateFallbackName(NPCGender gender)
        {
            string[] maleNames = { "John", "William", "James", "Robert", "Michael", "Thomas", "David", "Richard" };
            string[] femaleNames = { "Mary", "Patricia", "Jennifer", "Linda", "Elizabeth", "Barbara", "Susan", "Jessica" };
            string[] surnames = { "Smith", "Johnson", "Williams", "Jones", "Brown", "Davis", "Miller", "Wilson" };
            
            Random random = new Random();
            string firstName = gender == NPCGender.Male 
                ? maleNames[random.Next(maleNames.Length)] 
                : femaleNames[random.Next(femaleNames.Length)];
                
            string lastName = surnames[random.Next(surnames.Length)];
            return $"{firstName} {lastName}";
        }

        private void GenerateBio()
        {
            try
            {
                var gender = (NPCGender)Enum.Parse(typeof(NPCGender), _genderComboBox.SelectedItem.ToString());
                var type = (NPCType)Enum.Parse(typeof(NPCType), _typeComboBox.SelectedItem.ToString());
                
                // Create a temporary NPC for bio generation
                var tempNPC = new NPC(type, _nameTextBox.Text);
                tempNPC.Gender = gender;
                tempNPC.Level = (int)_levelNumeric.Value;
                
                // Generate bio
                string bio = _bioGenerator.GenerateBio(tempNPC);
                _bioTextBox.Text = bio;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error generating bio: {ex.Message}", "Error", 
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        #region Event Handlers

        private void TypeComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            // Update level constraints based on NPC type
            UpdateLevelConstraints();
            
            // Regenerate skills to apply mandatory skill levels
            CreateSkillControls();
            
            // Only generate a new name if the text box is empty
            if (_nameTextBox.Text == "")
            {
                GenerateRandomName();
            }
        }

        private void GenderComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            // Automatically regenerate name when gender changes
            GenerateRandomName();
        }

        private void GenerateNameButton_Click(object sender, EventArgs e)
        {
            GenerateRandomName();
        }

        private void LevelNumeric_ValueChanged(object sender, EventArgs e)
        {
            // First update the maximum values for all skill controls
            int maxLevel = (int)_levelNumeric.Value;
            foreach (var kvp in _skillControls)
            {
                var skillControl = kvp.Value;
                skillControl.LevelNumeric.Maximum = maxLevel;
                skillControl.ProgressBar.Maximum = maxLevel;
            }
            
            // Then reset all skills to their minimum values when level changes
            ResetSkillsToMinimum();
            
            // Finally update all constraints
            UpdateSkillConstraints();
        }

        private void ResetSkillsToMinimum()
        {
            var mandatorySkills = GetMandatorySkills();
            
            foreach (var kvp in _skillControls)
            {
                var skillName = kvp.Key;
                var skillControl = kvp.Value;
                
                // Set to mandatory level if applicable, otherwise reset to 0
                if (mandatorySkills.ContainsKey(skillName))
                {
                    skillControl.LevelNumeric.Value = mandatorySkills[skillName];
                }
                else
                {
                    skillControl.LevelNumeric.Value = 0;
                }
            }
        }

        private void ValidateSkillPointAllocation()
        {
            int maxLevel = (int)_levelNumeric.Value;
            int totalSkillPoints = _skillControls.Values.Sum(sc => (int)sc.LevelNumeric.Value);
            
            // If total exceeds the limit, find the skill that was just changed and reduce it
            if (totalSkillPoints > maxLevel)
            {
                // Find which control triggered this validation (the one with focus)
                var focusedControl = _skillsContainer.Controls.OfType<NumericUpDown>().FirstOrDefault(c => c.Focused);
                if (focusedControl != null)
                {
                    // Reduce the focused control's value to not exceed the limit
                    int excess = totalSkillPoints - maxLevel;
                    int newValue = Math.Max(0, (int)focusedControl.Value - excess);
                    
                    // Check if this is a mandatory skill
                    var mandatorySkills = GetMandatorySkills();
                    var skillName = _skillControls.FirstOrDefault(kvp => kvp.Value.LevelNumeric == focusedControl).Key;
                    if (!string.IsNullOrEmpty(skillName) && mandatorySkills.ContainsKey(skillName))
                    {
                        // Don't reduce below mandatory level
                        newValue = Math.Max(mandatorySkills[skillName], newValue);
                    }
                    
                    focusedControl.Value = newValue;
                }
            }
            
            // Update constraints after validation
            UpdateSkillConstraints();
        }

        private void GenerateBioButton_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_nameTextBox.Text))
            {
                MessageBox.Show("Please enter a name before generating a bio.", "Name Required", 
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            GenerateBio();
        }

        private void OkButton_Click(object sender, EventArgs e)
        {
            // Validate inputs
            if (string.IsNullOrWhiteSpace(_nameTextBox.Text))
            {
                MessageBox.Show("Please enter a name for the NPC.", "Name Required", 
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Check skill points constraint
            int maxLevel = (int)_levelNumeric.Value;
            int totalSkillPoints = _skillControls.Values.Sum(sc => (int)sc.LevelNumeric.Value);
            if (totalSkillPoints > maxLevel)
            {
                MessageBox.Show($"Total skill points ({totalSkillPoints}) cannot exceed NPC level ({maxLevel}).", 
                    "Invalid Skill Distribution", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                // Create the NPC
                var type = (NPCType)Enum.Parse(typeof(NPCType), _typeComboBox.SelectedItem.ToString());
                var gender = (NPCGender)Enum.Parse(typeof(NPCGender), _genderComboBox.SelectedItem.ToString());
                
                CreatedNPC = new NPC(type, _nameTextBox.Text);
                CreatedNPC.Gender = gender;
                CreatedNPC.Level = (int)_levelNumeric.Value;

                // Set skills
                foreach (var kvp in _skillControls)
                {
                    string skillName = kvp.Key;
                    int skillLevel = (int)kvp.Value.LevelNumeric.Value;
                    
                    var skill = CreatedNPC.Skills.Find(s => s.Name == skillName);
                    if (skill != null)
                    {
                        skill.Level = skillLevel;
                        // Calculate experience for this level
                        skill.Experience = skillLevel > 0 ? skillLevel * 50 : 0; // Partial XP towards next level
                    }
                }

                // Set bio
                if (!string.IsNullOrWhiteSpace(_bioTextBox.Text))
                {
                    CreatedNPC.Bio.SetAsCustom(_bioTextBox.Text);
                }

                // Set health state
                if (_healthStateComboBox.SelectedIndex > 0) // 0 is "Healthy"
                {
                    var healthState = (NPCStateType)Enum.Parse(typeof(NPCStateType), 
                        _healthStateComboBox.SelectedItem.ToString());
                    CreatedNPC.AddHealthState(healthState);
                }

                // Update upkeep costs based on final configuration
                CreatedNPC.UpdateUpkeepCosts();

                this.DialogResult = DialogResult.OK;
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error creating NPC: {ex.Message}", "Error", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        #endregion
    }
} 