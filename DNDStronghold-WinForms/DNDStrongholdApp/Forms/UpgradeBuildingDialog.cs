using System;
using System.Windows.Forms;
using System.Drawing;
using DNDStrongholdApp.Models;
using System.Collections.Generic;
using System.Linq;
using DNDStrongholdApp.Services;

namespace DNDStrongholdApp.Forms
{
    public class UpgradeBuildingDialog : Form
    {
        private readonly Building _building;
        private Button _upgradeButton;
        private readonly GameStateService _gameStateService;

        public UpgradeBuildingDialog(Building building)
        {
            _building = building;
            _gameStateService = GameStateService.GetInstance();
            InitializeComponents();
            UpdateUpgradeButtonState();
        }

        private void InitializeComponents()
        {
            this.Text = $"Upgrade {_building.Name}";
            this.Size = new Size(400, 300);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterParent;

            TableLayoutPanel layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Padding = new Padding(10)
            };

            // Costs section
            GroupBox costsGroup = new GroupBox
            {
                Text = "Upgrade Costs",
                Dock = DockStyle.Fill,
                Height = 100
            };

            FlowLayoutPanel costsPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                AutoSize = true
            };

            var upgradeCosts = _building.CalculateUpgradeCosts();
            foreach (var cost in upgradeCosts)
            {
                var resource = _gameStateService.GetCurrentStronghold().Resources.Find(r => r.Type == cost.ResourceType);
                bool hasEnough = resource != null && resource.Amount >= cost.Amount;

                Label costLabel = new Label
                {
                    Text = $"{cost.ResourceType}: {cost.Amount} ({resource?.Amount ?? 0})",
                    ForeColor = hasEnough ? Color.Green : Color.Red,
                    AutoSize = true
                };
                costsPanel.Controls.Add(costLabel);
            }

            costsGroup.Controls.Add(costsPanel);

            // Effects section
            GroupBox effectsGroup = new GroupBox
            {
                Text = "Upgrade Effects",
                Dock = DockStyle.Fill,
                Height = 100
            };

            FlowLayoutPanel effectsPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                AutoSize = true
            };

            // Get building info from BuildingData.json
            string jsonPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "BuildingData.json");
            if (System.IO.File.Exists(jsonPath))
            {
                string json = System.IO.File.ReadAllText(jsonPath);
                var buildingData = System.Text.Json.JsonSerializer.Deserialize<BuildingData>(json);
                var buildingInfo = buildingData.buildings.Find(b => b.type == _building.Type.ToString());
                
                if (buildingInfo != null && _building.Level < buildingInfo.maxLevel)
                {
                    // Worker slots increase
                    var nextLevelSlotIncrease = buildingInfo.workerSlotIncrease
                        .FirstOrDefault(w => w.level == _building.Level + 1);
                    if (nextLevelSlotIncrease != null)
                    {
                        Label slotLabel = new Label
                        {
                            Text = $"Worker Slots: +{nextLevelSlotIncrease.increase}",
                            AutoSize = true
                        };
                        effectsPanel.Controls.Add(slotLabel);
                    }

                    // Production scaling
                    var currentProduction = buildingInfo.productionScaling
                        .FirstOrDefault(p => p.level == _building.Level)?.resources ?? new List<ResourceScaling>();
                    var nextProduction = buildingInfo.productionScaling
                        .FirstOrDefault(p => p.level == _building.Level + 1)?.resources ?? new List<ResourceScaling>();

                    // Show both changed and new production resources
                    foreach (var nextProd in nextProduction)
                    {
                        var currentProd = currentProduction.FirstOrDefault(c => c.resourceType == nextProd.resourceType);
                        if (currentProd == null)
                        {
                            // New resource that wasn't produced before
                            Label prodLabel = new Label
                            {
                                Text = $"{nextProd.resourceType} per worker: 0 → {nextProd.perWorkerValue} (New!)",
                                AutoSize = true
                            };
                            effectsPanel.Controls.Add(prodLabel);
                        }
                        else if (nextProd.perWorkerValue != currentProd.perWorkerValue)
                        {
                            // Changed production value
                            Label prodLabel = new Label
                            {
                                Text = $"{nextProd.resourceType} per worker: {currentProd.perWorkerValue} → {nextProd.perWorkerValue}",
                                AutoSize = true
                            };
                            effectsPanel.Controls.Add(prodLabel);
                        }
                    }

                    // Upkeep changes
                    var currentUpkeep = buildingInfo.upkeepScaling
                        .Where(u => u.level == _building.Level)
                        .ToList();
                    var nextUpkeep = buildingInfo.upkeepScaling
                        .Where(u => u.level == _building.Level + 1)
                        .ToList();

                    // Show both changed and new upkeep resources
                    foreach (var nextUp in nextUpkeep)
                    {
                        var currentUp = currentUpkeep.FirstOrDefault(c => c.resourceType == nextUp.resourceType);
                        if (currentUp == null)
                        {
                            // New upkeep resource
                            Label upkeepLabel = new Label
                            {
                                Text = $"{nextUp.resourceType} upkeep: 0 → {nextUp.baseValue} (New!)",
                                AutoSize = true
                            };
                            effectsPanel.Controls.Add(upkeepLabel);
                        }
                        else if (nextUp.baseValue != currentUp.baseValue)
                        {
                            // Changed upkeep value
                            Label upkeepLabel = new Label
                            {
                                Text = $"{nextUp.resourceType} upkeep: {currentUp.baseValue} → {nextUp.baseValue}",
                                AutoSize = true
                            };
                            effectsPanel.Controls.Add(upkeepLabel);
                        }
                    }

                    // New available projects
                    var newProjects = buildingInfo.availableProjects
                        .Where(p => p.minLevel == _building.Level + 1)
                        .ToList();

                    if (newProjects.Any())
                    {
                        Label projectsLabel = new Label
                        {
                            Text = "New Projects Available:",
                            AutoSize = true
                        };
                        effectsPanel.Controls.Add(projectsLabel);

                        foreach (var project in newProjects)
                        {
                            Label projectLabel = new Label
                            {
                                Text = $"  • {project.projectName}",
                                AutoSize = true,
                                Margin = new Padding(10, 0, 0, 0)
                            };
                            effectsPanel.Controls.Add(projectLabel);
                        }
                    }
                }
            }

            effectsGroup.Controls.Add(effectsPanel);

            // Buttons
            FlowLayoutPanel buttonPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                Height = 40
            };

            Button cancelButton = new Button
            {
                Text = "Cancel",
                DialogResult = DialogResult.Cancel,
                Width = 75
            };

            _upgradeButton = new Button
            {
                Text = "Upgrade",
                DialogResult = DialogResult.OK,
                Width = 75
            };

            buttonPanel.Controls.Add(cancelButton);
            buttonPanel.Controls.Add(_upgradeButton);

            // Add all sections to main layout
            layout.Controls.Add(costsGroup, 0, 0);
            layout.Controls.Add(effectsGroup, 0, 1);
            layout.Controls.Add(buttonPanel, 0, 2);

            this.Controls.Add(layout);
            this.AcceptButton = _upgradeButton;
            this.CancelButton = cancelButton;
        }

        private void UpdateUpgradeButtonState()
        {
            bool canAfford = true;
            var stronghold = _gameStateService.GetCurrentStronghold();
            var upgradeCosts = _building.CalculateUpgradeCosts();
            foreach (var cost in upgradeCosts)
            {
                var resource = stronghold.Resources.Find(r => r.Type == cost.ResourceType);
                if (resource == null || resource.Amount < cost.Amount)
                {
                    canAfford = false;
                    break;
                }
            }

            // Also check if building is at max level
            string jsonPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "BuildingData.json");
            if (System.IO.File.Exists(jsonPath))
            {
                string json = System.IO.File.ReadAllText(jsonPath);
                var buildingData = System.Text.Json.JsonSerializer.Deserialize<BuildingData>(json);
                var buildingInfo = buildingData.buildings.Find(b => b.type == _building.Type.ToString());
                if (buildingInfo != null && _building.Level >= buildingInfo.maxLevel)
                {
                    canAfford = false;
                }
            }

            _upgradeButton.Enabled = canAfford;
        }
    }
} 