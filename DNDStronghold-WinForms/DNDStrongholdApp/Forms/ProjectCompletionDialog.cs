using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using DNDStrongholdApp.Models;
using DNDStrongholdApp.Services;

namespace DNDStrongholdApp.Forms
{
    public class ProjectCompletionDialog : Form
    {
        private readonly Building _building;
        private readonly Project _project;
        private readonly Stronghold _stronghold;
        private readonly GameStateService _gameState;
        private readonly BuildingInfo? _buildingInfo;

        private NumericUpDown _d20;
        private Label _infoLabel;
        private Label _resultLabel;
        private Button _resolveButton;
        private Button _skipButton;
        private bool _resolved;

        public ProjectCompletionResult? Result { get; private set; }

        public ProjectCompletionDialog(Building building, Project project, Stronghold stronghold, GameStateService gameState)
        {
            _building = building;
            _project = project;
            _stronghold = stronghold;
            _gameState = gameState;
            var data = new Commands.LoadBuildingDataCommand().Execute();
            _buildingInfo = data.buildings.Find(b => b.type == building.TypeName);
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            int bonus = ProjectResolutionService.ComputeBonus(_building, _buildingInfo, _project, _stronghold.NPCs);

            this.Text = $"Project complete: {_project.Name}";
            this.ClientSize = new Size(520, 340);
            this.MinimumSize = new Size(480, 300);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterParent;
            this.ShowInTaskbar = false;
            this.FormClosing += ProjectCompletionDialog_FormClosing;
            this.Shown += ProjectCompletionDialog_Shown;

            var buttonPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                FlowDirection = FlowDirection.RightToLeft,
                Height = 52,
                Padding = new Padding(8, 10, 8, 10),
                WrapContents = false
            };

            _resolveButton = new Button
            {
                Text = _project.RollMode == ProjectRollMode.None ? "OK" : "Resolve",
                Size = new Size(110, 32),
                DialogResult = DialogResult.None
            };
            _skipButton = new Button
            {
                Text = "Skip roll",
                Size = new Size(110, 32),
                Visible = _project.RollMode == ProjectRollMode.Optional
            };
            _resolveButton.Click += ResolveButton_Click;
            _skipButton.Click += (_, _) => Resolve(skipped: true);
            buttonPanel.Controls.Add(_resolveButton);
            buttonPanel.Controls.Add(_skipButton);
            this.AcceptButton = _resolveButton;

            string workers = string.Join(", ",
                _project.AssignedWorkers.Select(id => _stronghold.NPCs.Find(n => n.Id == id)?.Name).Where(n => !string.IsNullOrEmpty(n)));

            var info = new Label
            {
                Dock = DockStyle.Top,
                Height = 110,
                Padding = new Padding(12, 10, 12, 0),
                Text =
                    $"{_project.Name} at {_building.Name} is finished.\n" +
                    (string.IsNullOrEmpty(_project.Commission) ? "" : $"Commission: {_project.Commission}\n") +
                    $"Workers: {(string.IsNullOrEmpty(workers) ? "none" : workers)}\n" +
                    (_project.RollMode == ProjectRollMode.None
                        ? "No table roll. Outcome is applied automatically."
                        : $"DC {_project.DC} ({_project.Difficulty.DisplayName()}). Bonus +{bonus}. Enter the d20 total.")
            };
            _infoLabel = info;

            var d20Label = new Label
            {
                Text = "d20 total:",
                AutoSize = true,
                Location = new Point(12, 126),
                Visible = _project.RollMode != ProjectRollMode.None
            };
            _d20 = new NumericUpDown
            {
                Minimum = 1,
                Maximum = 20,
                Value = 10,
                Width = 80,
                Location = new Point(90, 122),
                Visible = _project.RollMode != ProjectRollMode.None
            };

            _resultLabel = new Label
            {
                Location = new Point(12, _project.RollMode == ProjectRollMode.None ? 126 : 160),
                Size = new Size(496, _project.RollMode == ProjectRollMode.None ? 144 : 110),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom,
                Text = ""
            };

            this.Controls.Add(_resultLabel);
            this.Controls.Add(_d20);
            this.Controls.Add(d20Label);
            this.Controls.Add(info);
            this.Controls.Add(buttonPanel);
        }

        private void ProjectCompletionDialog_Shown(object? sender, EventArgs e)
        {
            if (_project.RollMode == ProjectRollMode.None && !_resolved)
                Resolve(skipped: false);
        }

        private void ProjectCompletionDialog_FormClosing(object? sender, FormClosingEventArgs e)
        {
            if (_resolved) return;

            if (_project.RollMode == ProjectRollMode.Required)
            {
                MessageBox.Show("A d20 total is required before this project can finish.", "Roll required",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                e.Cancel = true;
                return;
            }

            // None / Optional: finishing the dialog always resolves the project.
            Resolve(skipped: _project.RollMode == ProjectRollMode.Optional);
            if (!_resolved)
                e.Cancel = true;
        }

        private void ResolveButton_Click(object? sender, EventArgs e)
        {
            if (_resolved)
            {
                this.DialogResult = DialogResult.OK;
                Close();
                return;
            }
            Resolve(skipped: false);
        }

        private void Resolve(bool skipped)
        {
            if (_resolved) return;
            try
            {
                int? d20 = _project.RollMode == ProjectRollMode.None || skipped ? null : (int)_d20.Value;
                Result = _gameState.FinishProject(_building.Id, d20, skipped, notify: false);
                _resolved = true;
                string gained = ProjectResolutionService.FormatCosts(Result?.YieldGranted);
                string workers = string.Join(", ",
                    _project.AssignedWorkers.Select(id => _stronghold.NPCs.Find(n => n.Id == id)?.Name).Where(n => !string.IsNullOrEmpty(n)));
                _infoLabel.Text =
                    $"{_project.Name} at {_building.Name} is finished.\n" +
                    $"Workers: {(string.IsNullOrEmpty(workers) ? "none" : workers)}\n" +
                    $"Gained: {gained}";
                _resultLabel.Text = FormatResolvedText(Result);
                _resolveButton.Text = "OK";
                _skipButton.Enabled = false;
                _d20.Enabled = false;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not finish the project: {ex.Message}", "Project",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static string FormatResolvedText(ProjectCompletionResult? result)
        {
            if (result == null) return "";
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(result.Summary))
                parts.Add(result.Summary.Trim());
            string gained = ProjectResolutionService.FormatCosts(result.YieldGranted);
            if (gained != "None")
                parts.Add("Gained: " + gained);
            if (!string.IsNullOrEmpty(result.Mishap))
                parts.Add(result.Mishap);
            return string.Join("\n\n", parts);
        }
    }
}
