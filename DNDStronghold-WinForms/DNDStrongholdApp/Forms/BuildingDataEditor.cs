using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Forms;
using DNDStrongholdApp.Models;

namespace DNDStrongholdApp.Forms
{
    public partial class BuildingDataEditor : Form
    {
        private BuildingData buildingData;
        private string jsonPath;
        private BuildingInfo currentBuilding;

        // Add class-level fields for controls
        private ComboBox buildingTypeCombo;
        private NumericUpDown numWorkerSlots;
        private NumericUpDown numRequiredConstructionPoints;
        private NumericUpDown numMaxLevel;
        private DataGridView dgvWorkerSlotIncrease;
        private DataGridView dgvProductionScaling;
        private DataGridView dgvUpkeepScaling;
        private ComboBox primarySkillCombo;
        private ComboBox secondarySkillCombo;
        private ComboBox tertiarySkillCombo;
        private DataGridView projectsDataGrid;
        private DataGridView dgvConstructionCost;
        private DataGridView dgvWorkerBonus;

        public BuildingDataEditor()
        {
            InitializeComponent();
            jsonPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "BuildingData.json");
            if (!File.Exists(jsonPath))
            {
                // Try the alternative path
                jsonPath = Path.Combine(Directory.GetCurrentDirectory(), "Data", "BuildingData.json");
            }
            LoadBuildingData();
            InitializeUI();
            this.Resize += BuildingDataEditor_Resize;
        }

        private void BuildingDataEditor_Resize(object sender, EventArgs e)
        {
            int margin = 10;
            // Find the bottom of the top controls (basicPropertiesPanel)
            int topControlsBottom = 0;
            foreach (Control ctrl in this.Controls)
            {
                if (ctrl.Name == "basicPropertiesPanel")
                {
                    topControlsBottom = ctrl.Bottom;
                    break;
                }
            }
            if (topControlsBottom == 0 && this.Controls.Count > 1)
                topControlsBottom = this.Controls[1].Bottom; // fallback

            // Find scalingPanel and saveButton
            Panel scalingPanel = null;
            Button saveButton = null;
            foreach (Control ctrl in this.Controls)
            {
                if (ctrl is Panel && ctrl.Controls.Count > 0 && ctrl.Controls[0] is Label && ((Label)ctrl.Controls[0]).Text.StartsWith("Level-based"))
                    scalingPanel = (Panel)ctrl;
                if (ctrl is Button && ((Button)ctrl).Text.Contains("Save"))
                    saveButton = (Button)ctrl;
            }
            if (scalingPanel == null || saveButton == null) return;

            int saveButtonHeight = saveButton.Height;
            int availableHeight = this.ClientSize.Height - topControlsBottom - saveButtonHeight - 3 * margin;

            scalingPanel.Location = new Point(10, topControlsBottom + margin);
            scalingPanel.Size = new Size(this.ClientSize.Width - 20, availableHeight);

            saveButton.Location = new Point(10, scalingPanel.Bottom + margin);
        }

        private void InitializeUI()
        {
            this.Text = "Building Data Editor";
            this.Size = new Size(1000, 800);

            // Building Type Selection Panel
            var buildingTypePanel = new Panel
            {
                Location = new Point(10, 10),
                Size = new Size(960, 40)
            };

            var buildingTypeLabel = new Label { Text = "Building Type:", Location = new Point(0, 7) };
            buildingTypeCombo = new ComboBox
            {
                Location = new Point(120, 4),
                Width = 200,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            buildingTypeCombo.Items.AddRange(Enum.GetNames(typeof(BuildingType)));
            buildingTypeCombo.SelectedIndexChanged += (s, e) =>
            {
                cmbBuildingType_SelectedIndexChanged(s, e);
            };

            buildingTypePanel.Controls.AddRange(new Control[] { buildingTypeLabel, buildingTypeCombo });

            // Basic Properties Panel
            var basicPropertiesPanel = new Panel
            {
                Name = "basicPropertiesPanel",
                Location = new Point(10, 60),
                Size = new Size(960, 100)
            };

            // Worker Slots
            var workerSlotsLabel = new Label { Text = "Slots", Location = new Point(0, 7), Width = 55 };
            numWorkerSlots = new NumericUpDown
            {
                Location = new Point(60, 4),
                Width = 70,
                Minimum = 0,
                Maximum = 10
            };
            numWorkerSlots.ValueChanged += (s, e) =>
            {
                if (currentBuilding != null)
                {
                    currentBuilding.workerSlots = (int)numWorkerSlots.Value;
                }
            };

            // Required Construction Points
            var constructionPointsLabel = new Label { Text = "Points", Location = new Point(140, 7), Width = 60 };
            numRequiredConstructionPoints = new NumericUpDown
            {
                Location = new Point(200, 4),
                Width = 90,
                Minimum = 0,
                Maximum = 1000
            };
            numRequiredConstructionPoints.ValueChanged += (s, e) =>
            {
                if (currentBuilding != null)
                {
                    currentBuilding.requiredConstructionPoints = (int)numRequiredConstructionPoints.Value;
                }
            };

            // Max Level
            var maxLevelLabel = new Label { Text = "MaxLvl", Location = new Point(310, 7), Width = 60 };
            numMaxLevel = new NumericUpDown
            {
                Location = new Point(370, 4),
                Width = 70,
                Minimum = 1,
                Maximum = 10
            };
            numMaxLevel.ValueChanged += (s, e) =>
            {
                if (currentBuilding != null)
                {
                    currentBuilding.maxLevel = (int)numMaxLevel.Value;
                }
            };

            // Primary/Secondary/Tertiary Skill dropdowns (on next row)
            var primarySkillLabel = new Label { Text = "Pri Skill", Location = new Point(0, 40), Width = 65 };
            primarySkillCombo = new ComboBox { Location = new Point(70, 37), Width = 140, DropDownStyle = ComboBoxStyle.DropDownList };
            var secondarySkillLabel = new Label { Text = "Sec Skill", Location = new Point(220, 40), Width = 70 };
            secondarySkillCombo = new ComboBox { Location = new Point(300, 37), Width = 140, DropDownStyle = ComboBoxStyle.DropDownList };
            var tertiarySkillLabel = new Label { Text = "Ter Skill", Location = new Point(450, 40), Width = 70 };
            tertiarySkillCombo = new ComboBox { Location = new Point(530, 37), Width = 140, DropDownStyle = ComboBoxStyle.DropDownList };

            // Populate skills with separator
            var basicSkills = Enum.GetNames(typeof(DNDStrongholdApp.Models.BasicSkill));
            var advancedSkills = Enum.GetNames(typeof(DNDStrongholdApp.Models.AdvancedSkill));
            var skillList = new List<string>(basicSkills);
            skillList.Add("— Advanced Skills —");
            skillList.AddRange(advancedSkills);
            // Add '-None-' option for secondary and tertiary
            var skillListWithNone = new List<string> { "-None-" };
            skillListWithNone.AddRange(skillList);
            primarySkillCombo.Items.AddRange(skillList.ToArray());
            secondarySkillCombo.Items.AddRange(skillListWithNone.ToArray());
            tertiarySkillCombo.Items.AddRange(skillListWithNone.ToArray());

            primarySkillCombo.SelectedIndexChanged += (s, e) => { if (currentBuilding != null && !primarySkillCombo.SelectedItem.ToString().StartsWith("—")) currentBuilding.primarySkill = primarySkillCombo.SelectedItem.ToString(); };
            secondarySkillCombo.SelectedIndexChanged += (s, e) => {
                if (currentBuilding != null)
                {
                    var val = secondarySkillCombo.SelectedItem.ToString();
                    currentBuilding.secondarySkill = (val == "-None-") ? string.Empty : val;
                }
            };
            tertiarySkillCombo.SelectedIndexChanged += (s, e) => {
                if (currentBuilding != null)
                {
                    var val = tertiarySkillCombo.SelectedItem.ToString();
                    currentBuilding.tertiarySkill = (val == "-None-") ? string.Empty : val;
                }
            };

            basicPropertiesPanel.Controls.AddRange(new Control[] {
                workerSlotsLabel, numWorkerSlots,
                constructionPointsLabel, numRequiredConstructionPoints,
                maxLevelLabel, numMaxLevel,
                primarySkillLabel, primarySkillCombo,
                secondarySkillLabel, secondarySkillCombo,
                tertiarySkillLabel, tertiarySkillCombo
            });
            basicPropertiesPanel.Height = Math.Max(basicPropertiesPanel.Height, tertiarySkillCombo.Bottom + 10);

            // Tabs Panel
            var tabsPanel = new Panel
            {
                Name = "tabsPanel",
                Location = new Point(10, 170),
                Size = new Size(950, 360),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom
            };

            var tabsLabel = new Label { Text = "Tabs", Location = new Point(0, 7), Font = new Font(Font, FontStyle.Bold) };

            // TabControl for level-based tables
            var tabControl = new TabControl
            {
                Location = new Point(0, 30),
                Size = new Size(950, 330),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom
            };

            // Construction Costs Tab
            var tabConstructionCosts = new TabPage("Construction Costs");
            dgvConstructionCost = new DataGridView
            {
                Name = "dgvConstructionCost",
                Dock = DockStyle.Fill,
                AllowUserToAddRows = true,
                AllowUserToDeleteRows = true,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                RowHeadersVisible = false
            };
            var costResourceTypeCol = new DataGridViewComboBoxColumn { Name = "ResourceType", HeaderText = "Resource Type" };
            costResourceTypeCol.Items.AddRange(Enum.GetNames(typeof(DNDStrongholdApp.Models.ResourceType)));
            dgvConstructionCost.Columns.Add(costResourceTypeCol);
            dgvConstructionCost.Columns.Add("Amount", "Amount");
            tabConstructionCosts.Controls.Add(dgvConstructionCost);

            // Worker Slot Increases Tab
            var tabWorkerSlots = new TabPage("Worker Slot Increases");
            dgvWorkerSlotIncrease = new DataGridView
            {
                Dock = DockStyle.Fill,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                AllowUserToAddRows = true,
                AllowUserToDeleteRows = true
            };
            dgvWorkerSlotIncrease.Columns.Add("Level", "Level");
            dgvWorkerSlotIncrease.Columns.Add("Increase", "Increase");
            dgvWorkerSlotIncrease.CellValueChanged += (s, e) => UpdateWorkerSlotIncreases();
            tabWorkerSlots.Controls.Add(dgvWorkerSlotIncrease);

            // Production Scaling Tab
            var tabProduction = new TabPage("Production Scaling");
            dgvProductionScaling = new DataGridView
            {
                Dock = DockStyle.Fill,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                AllowUserToAddRows = true,
                AllowUserToDeleteRows = true
            };
            dgvProductionScaling.Columns.Add("Level", "Level");
            var prodResourceTypeCol = new DataGridViewComboBoxColumn { Name = "ResourceType", HeaderText = "Resource Type" };
            prodResourceTypeCol.Items.AddRange(Enum.GetNames(typeof(DNDStrongholdApp.Models.ResourceType)));
            dgvProductionScaling.Columns.Add(prodResourceTypeCol);
            dgvProductionScaling.Columns.Add("PerWorkerValue", "Per Worker Value");
            dgvProductionScaling.CellValueChanged += (s, e) => UpdateProductionScaling();
            tabProduction.Controls.Add(dgvProductionScaling);

            // Upkeep Scaling Tab
            var tabUpkeep = new TabPage("Upkeep Scaling");
            dgvUpkeepScaling = new DataGridView
            {
                Dock = DockStyle.Fill,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                AllowUserToAddRows = true,
                AllowUserToDeleteRows = true
            };
            dgvUpkeepScaling.Columns.Add("Level", "Level");
            var upkeepResourceTypeCol = new DataGridViewComboBoxColumn { Name = "ResourceType", HeaderText = "Resource Type" };
            upkeepResourceTypeCol.Items.AddRange(Enum.GetNames(typeof(DNDStrongholdApp.Models.ResourceType)));
            dgvUpkeepScaling.Columns.Add(upkeepResourceTypeCol);
            dgvUpkeepScaling.Columns.Add("BaseValue", "Base Value");
            dgvUpkeepScaling.CellValueChanged += (s, e) => UpdateUpkeepScaling();
            tabUpkeep.Controls.Add(dgvUpkeepScaling);

            // Available Projects Tab
            var tabProjects = new TabPage("Available Projects");
            projectsDataGrid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                AllowUserToAddRows = true,
                AllowUserToDeleteRows = true
            };
            projectsDataGrid.Columns.Add("ProjectName", "Project Name");
            projectsDataGrid.Columns.Add("MinLevel", "Min Level");
            projectsDataGrid.CellValueChanged += (s, e) => UpdateAvailableProjects();
            tabProjects.Controls.Add(projectsDataGrid);

            // Worker Bonus Tab
            var tabWorkerBonus = new TabPage("Worker Production Bonus");
            dgvWorkerBonus = new DataGridView
            {
                Name = "dgvWorkerBonus",
                Dock = DockStyle.Fill,
                AllowUserToAddRows = true,
                AllowUserToDeleteRows = true,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                RowHeadersVisible = false
            };
            var bonusResourceCol = new DataGridViewComboBoxColumn { Name = "Resource", HeaderText = "Resource" };
            bonusResourceCol.Items.AddRange(Enum.GetNames(typeof(DNDStrongholdApp.Models.ResourceType)));
            dgvWorkerBonus.Columns.Add(bonusResourceCol);
            var bonusSkillCol = new DataGridViewComboBoxColumn { Name = "Skill", HeaderText = "Skill" };
            var allSkills = Enum.GetNames(typeof(DNDStrongholdApp.Models.BasicSkill)).ToList();
            allSkills.AddRange(Enum.GetNames(typeof(DNDStrongholdApp.Models.AdvancedSkill)));
            bonusSkillCol.Items.AddRange(allSkills.ToArray());
            dgvWorkerBonus.Columns.Add(bonusSkillCol);
            dgvWorkerBonus.Columns.Add(new DataGridViewTextBoxColumn { Name = "BonusValue", HeaderText = "Bonus Value" });
            tabWorkerBonus.Controls.Add(dgvWorkerBonus);

            // Add all tabs
            tabControl.TabPages.Add(tabConstructionCosts);
            tabControl.TabPages.Add(tabWorkerSlots);
            tabControl.TabPages.Add(tabProduction);
            tabControl.TabPages.Add(tabUpkeep);
            tabControl.TabPages.Add(tabProjects);
            tabControl.TabPages.Add(tabWorkerBonus);

            tabsPanel.Controls.Clear();
            tabsPanel.Controls.AddRange(new Control[] { tabsLabel, tabControl });

            var saveButton = new Button
            {
                Text = "Save Changes",
                Location = new Point(10, tabsPanel.Bottom + 10),
                Size = new Size(120, 30)
            };
            saveButton.Click += (s, e) => btnSave_Click(s, e);

            this.Controls.Clear();
            this.Controls.AddRange(new Control[]
            {
                buildingTypePanel,
                basicPropertiesPanel,
                tabsPanel,
                saveButton
            });
            // Initial layout
            BuildingDataEditor_Resize(this, EventArgs.Empty);
        }

        private void cmbBuildingType_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (buildingTypeCombo.SelectedItem == null) return;

            string selectedType = buildingTypeCombo.SelectedItem.ToString();
            currentBuilding = buildingData.buildings.Find(b => b.type == selectedType);
            // Do not create a new entry here; only create on save if needed

            // Update basic properties
            numWorkerSlots.Value = currentBuilding.workerSlots;
            numRequiredConstructionPoints.Value = currentBuilding.requiredConstructionPoints;
            numMaxLevel.Value = currentBuilding.maxLevel;

            // Update skills dropdowns
            if (!string.IsNullOrEmpty(currentBuilding.primarySkill))
                primarySkillCombo.SelectedItem = currentBuilding.primarySkill;
            else
                primarySkillCombo.SelectedIndex = 0;
            if (!string.IsNullOrEmpty(currentBuilding.secondarySkill))
                secondarySkillCombo.SelectedItem = currentBuilding.secondarySkill;
            else
                secondarySkillCombo.SelectedItem = "-None-";
            if (!string.IsNullOrEmpty(currentBuilding.tertiarySkill))
                tertiarySkillCombo.SelectedItem = currentBuilding.tertiarySkill;
            else
                tertiarySkillCombo.SelectedItem = "-None-";

            // Update worker slot increases
            dgvWorkerSlotIncrease.Rows.Clear();
            foreach (var increase in currentBuilding.workerSlotIncrease)
            {
                dgvWorkerSlotIncrease.Rows.Add(increase.level, increase.increase);
            }

            // Update production scaling
            dgvProductionScaling.Rows.Clear();
            foreach (var scaling in currentBuilding.productionScaling)
            {
                foreach (var resource in scaling.resources)
                {
                    dgvProductionScaling.Rows.Add(scaling.level, resource.resourceType, resource.perWorkerValue);
                }
            }

            // Update upkeep scaling
            dgvUpkeepScaling.Rows.Clear();
            if (currentBuilding.upkeepScaling != null)
            {
                foreach (var scaling in currentBuilding.upkeepScaling)
                {
                    dgvUpkeepScaling.Rows.Add(scaling.level, scaling.resourceType, scaling.baseValue);
                }
            }

            // Update available projects
            projectsDataGrid.Rows.Clear();
            foreach (var proj in currentBuilding.availableProjects)
            {
                projectsDataGrid.Rows.Add(proj.projectName, proj.minLevel);
            }

            // Update construction costs
            dgvConstructionCost.Rows.Clear();
            foreach (var cost in currentBuilding.constructionCost)
            {
                dgvConstructionCost.Rows.Add(cost.resourceType, cost.amount);
            }

            // Load worker bonuses
            dgvWorkerBonus.Rows.Clear();
            foreach (var bonus in currentBuilding.workerProductionBonus)
            {
                dgvWorkerBonus.Rows.Add(bonus.resourceType, bonus.skill, bonus.bonusValue);
            }
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            string selectedType = buildingTypeCombo.SelectedItem?.ToString();
            if (string.IsNullOrEmpty(selectedType)) return;

            // Find or create the building data block
            currentBuilding = buildingData.buildings.Find(b => b.type == selectedType);
            if (currentBuilding == null)
            {
                currentBuilding = new BuildingInfo
                {
                    type = selectedType,
                    workerSlots = (int)numWorkerSlots.Value,
                    requiredConstructionPoints = (int)numRequiredConstructionPoints.Value,
                    maxLevel = (int)numMaxLevel.Value,
                    workerSlotIncrease = new List<LevelIncrease>(),
                    productionScaling = new List<LevelResourceValue>(),
                    upkeepScaling = new List<LevelUpkeepValue>(),
                    constructionCost = new List<ResourceCostInfo>(),
                    availableProjects = new List<AvailableProjectInfo>(),
                    primarySkill = primarySkillCombo.SelectedItem?.ToString() ?? string.Empty,
                    secondarySkill = secondarySkillCombo.SelectedItem?.ToString() ?? string.Empty,
                    tertiarySkill = tertiarySkillCombo.SelectedItem?.ToString() ?? string.Empty,
                    workerProductionBonus = new List<WorkerBonusInfo>()
                };
                buildingData.buildings.Add(currentBuilding);
            }

            // Update basic properties
            currentBuilding.workerSlots = (int)numWorkerSlots.Value;
            currentBuilding.requiredConstructionPoints = (int)numRequiredConstructionPoints.Value;
            currentBuilding.maxLevel = (int)numMaxLevel.Value;

            // Update skills
            currentBuilding.primarySkill = primarySkillCombo.SelectedItem?.ToString() ?? string.Empty;
            var sec = secondarySkillCombo.SelectedItem?.ToString() ?? string.Empty;
            currentBuilding.secondarySkill = (sec == "-None-") ? string.Empty : sec;
            var ter = tertiarySkillCombo.SelectedItem?.ToString() ?? string.Empty;
            currentBuilding.tertiarySkill = (ter == "-None-") ? string.Empty : ter;

            // Update worker slot increases
            currentBuilding.workerSlotIncrease.Clear();
            foreach (DataGridViewRow row in dgvWorkerSlotIncrease.Rows)
            {
                if (row.IsNewRow) continue;
                if (row.Cells[0].Value != null && row.Cells[1].Value != null)
                {
                    currentBuilding.workerSlotIncrease.Add(new LevelIncrease
                    {
                        level = Convert.ToInt32(row.Cells[0].Value),
                        increase = Convert.ToInt32(row.Cells[1].Value)
                    });
                }
            }

            // Update production scaling
            currentBuilding.productionScaling.Clear();
            var levelGroups = dgvProductionScaling.Rows.Cast<DataGridViewRow>()
                .Where(r => !r.IsNewRow && r.Cells[0].Value != null && r.Cells[1].Value != null && r.Cells[2].Value != null)
                .GroupBy(r => Convert.ToInt32(r.Cells[0].Value));

            foreach (var levelGroup in levelGroups)
            {
                var levelScaling = new LevelResourceValue { level = levelGroup.Key };
                foreach (var row in levelGroup)
                {
                    levelScaling.resources.Add(new ResourceScaling
                    {
                        resourceType = row.Cells[1].Value.ToString(),
                        perWorkerValue = Convert.ToInt32(row.Cells[2].Value)
                    });
                }
                currentBuilding.productionScaling.Add(levelScaling);
            }

            // Update upkeep scaling
            currentBuilding.upkeepScaling = new List<LevelUpkeepValue>();
            foreach (DataGridViewRow row in dgvUpkeepScaling.Rows)
            {
                if (row.IsNewRow) continue;
                if (row.Cells[0].Value != null && row.Cells[1].Value != null && row.Cells[2].Value != null)
                {
                    currentBuilding.upkeepScaling.Add(new LevelUpkeepValue
                    {
                        level = Convert.ToInt32(row.Cells[0].Value),
                        resourceType = row.Cells[1].Value.ToString(),
                        baseValue = Convert.ToInt32(row.Cells[2].Value)
                    });
                }
            }

            // Update construction costs
            currentBuilding.constructionCost.Clear();
            foreach (DataGridViewRow row in dgvConstructionCost.Rows)
            {
                if (row.IsNewRow) continue;
                if (row.Cells[0].Value != null && row.Cells[1].Value != null)
                {
                    currentBuilding.constructionCost.Add(new ResourceCostInfo
                    {
                        resourceType = row.Cells[0].Value.ToString(),
                        amount = Convert.ToInt32(row.Cells[1].Value)
                    });
                }
            }

            // Update worker bonuses
            currentBuilding.workerProductionBonus.Clear();
            foreach (DataGridViewRow row in dgvWorkerBonus.Rows)
            {
                if (row.IsNewRow) continue;
                if (row.Cells[0].Value != null && row.Cells[1].Value != null && row.Cells[2].Value != null)
                {
                    currentBuilding.workerProductionBonus.Add(new WorkerBonusInfo
                    {
                        resourceType = row.Cells[0].Value.ToString(),
                        skill = row.Cells[1].Value.ToString(),
                        bonusValue = Convert.ToDecimal(row.Cells[2].Value)
                    });
                }
            }

            // Update available projects
            currentBuilding.availableProjects.Clear();
            foreach (DataGridViewRow row in projectsDataGrid.Rows)
            {
                if (row.IsNewRow) continue;
                if (row.Cells[0].Value != null && row.Cells[1].Value != null)
                {
                    currentBuilding.availableProjects.Add(new AvailableProjectInfo
                    {
                        projectName = row.Cells[0].Value.ToString(),
                        minLevel = Convert.ToInt32(row.Cells[1].Value)
                    });
                }
            }

            // Save to file
            var options = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(buildingData, options);
            File.WriteAllText(jsonPath, json);

            MessageBox.Show("Building data saved successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void LoadBuildingData()
        {
            if (File.Exists(jsonPath))
            {
                string json = File.ReadAllText(jsonPath);
                buildingData = JsonSerializer.Deserialize<BuildingData>(json);
                if (buildingData == null)
                {
                    buildingData = new BuildingData();
                }
            }
            else
            {
                buildingData = new BuildingData();
            }
        }

        private void UpdateWorkerSlotIncreases()
        {
            var building = currentBuilding;
            if (building == null) return;

            building.workerSlotIncrease.Clear();
            foreach (DataGridViewRow row in dgvWorkerSlotIncrease.Rows)
            {
                if (row.Cells[0].Value != null && row.Cells[1].Value != null)
                {
                    building.workerSlotIncrease.Add(new LevelIncrease
                    {
                        level = Convert.ToInt32(row.Cells[0].Value),
                        increase = Convert.ToInt32(row.Cells[1].Value)
                    });
                }
            }
        }

        private void UpdateProductionScaling()
        {
            var building = currentBuilding;
            if (building == null) return;

            building.productionScaling.Clear();
            var levelGroups = dgvProductionScaling.Rows.Cast<DataGridViewRow>()
                .Where(r => r.Cells[0].Value != null && r.Cells[1].Value != null && r.Cells[2].Value != null)
                .GroupBy(r => Convert.ToInt32(r.Cells[0].Value));

            foreach (var levelGroup in levelGroups)
            {
                var levelScaling = new LevelResourceValue { level = levelGroup.Key };
                foreach (var row in levelGroup)
                {
                    levelScaling.resources.Add(new ResourceScaling
                    {
                        resourceType = row.Cells[1].Value.ToString(),
                        perWorkerValue = Convert.ToInt32(row.Cells[2].Value)
                    });
                }
                building.productionScaling.Add(levelScaling);
            }
        }

        private void UpdateUpkeepScaling()
        {
            var building = currentBuilding;
            if (building == null) return;

            building.upkeepScaling.Clear();
            foreach (DataGridViewRow row in dgvUpkeepScaling.Rows)
            {
                if (row.Cells[0].Value != null && row.Cells[1].Value != null && row.Cells[2].Value != null)
                {
                    building.upkeepScaling.Add(new LevelUpkeepValue
                    {
                        level = Convert.ToInt32(row.Cells[0].Value),
                        resourceType = row.Cells[1].Value.ToString(),
                        baseValue = Convert.ToInt32(row.Cells[2].Value)
                    });
                }
            }
        }

        private void UpdateAvailableProjects()
        {
            if (currentBuilding == null) return;
            currentBuilding.availableProjects.Clear();
            foreach (DataGridViewRow row in projectsDataGrid.Rows)
            {
                if (row.Cells[0].Value != null && row.Cells[1].Value != null)
                {
                    currentBuilding.availableProjects.Add(new AvailableProjectInfo
                    {
                        projectName = row.Cells[0].Value.ToString(),
                        minLevel = Convert.ToInt32(row.Cells[1].Value)
                    });
                }
            }
        }
    }
} 