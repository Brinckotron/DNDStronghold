using System;
using System.Windows.Forms;
using System.Drawing;
using DNDStrongholdApp.Models;
using DNDStrongholdApp.Services;

namespace DNDStrongholdApp.Forms
{
    public class RepairBuildingDialog : Form
    {
        private readonly Building _building;
        private Button _repairButton;
        private readonly GameStateService _gameStateService;

        public RepairBuildingDialog(Building building)
        {
            _building = building;
            _gameStateService = GameStateService.GetInstance();
            InitializeComponents();
            UpdateRepairButtonState();
        }

        private void InitializeComponents()
        {
            this.Text = $"Repair {_building.Name}";
            this.Size = new Size(400, 200);
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

            // Current condition
            Label conditionLabel = new Label
            {
                Text = $"Current Condition: {_building.Condition}%",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft
            };

            // Costs section
            GroupBox costsGroup = new GroupBox
            {
                Text = "Repair Costs",
                Dock = DockStyle.Fill,
                Height = 100
            };

            FlowLayoutPanel costsPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                AutoSize = true
            };

            foreach (var cost in _building.RepairCost)
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

            _repairButton = new Button
            {
                Text = "Repair",
                DialogResult = DialogResult.OK,
                Width = 75
            };

            buttonPanel.Controls.Add(cancelButton);
            buttonPanel.Controls.Add(_repairButton);

            // Add all sections to main layout
            layout.Controls.Add(conditionLabel, 0, 0);
            layout.Controls.Add(costsGroup, 0, 1);
            layout.Controls.Add(buttonPanel, 0, 2);

            this.Controls.Add(layout);
            this.AcceptButton = _repairButton;
            this.CancelButton = cancelButton;
        }

        private void UpdateRepairButtonState()
        {
            bool canAfford = true;
            var stronghold = _gameStateService.GetCurrentStronghold();
            foreach (var cost in _building.RepairCost)
            {
                var resource = stronghold.Resources.Find(r => r.Type == cost.ResourceType);
                if (resource == null || resource.Amount < cost.Amount)
                {
                    canAfford = false;
                    break;
                }
            }

            _repairButton.Enabled = canAfford;
        }
    }
} 