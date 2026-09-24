using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using DNDStrongholdApp.Models;
using DNDStrongholdApp.Services;

namespace DNDStrongholdApp.Forms
{
    public class ManagePayrollDialog : Form
    {
        private readonly Stronghold _stronghold;
        private readonly GameStateService _gameStateService;
        private readonly HashSet<string> _pendingCuts = new(StringComparer.Ordinal);
        private bool _suppressCheckEvents;
        private bool _summaryQueued;

        private Label _availableLabel = null!;
        private Label _neededLabel = null!;
        private Label _shortfallLabel = null!;
        private ListView _workerList = null!;
        private Button _autoCutButton = null!;
        private Button _unassignSelectedButton = null!;
        private Button _resetButton = null!;
        private Button _applyButton = null!;
        private Button _cancelButton = null!;

        public ManagePayrollDialog(Stronghold stronghold, GameStateService gameStateService)
        {
            _stronghold = stronghold;
            _gameStateService = gameStateService;
            InitializeDialog();
            // Defer first fill until the handle exists so ListView check events behave.
            HandleCreated += (_, _) =>
            {
                if (_workerList.Items.Count == 0)
                    RefreshAll();
            };
            if (IsHandleCreated)
                RefreshAll();
        }

        private static Font SafeBold(Font baseFont)
        {
            try
            {
                return new Font(baseFont, FontStyle.Bold);
            }
            catch (ArgumentException)
            {
                return baseFont;
            }
        }

        private void InitializeDialog()
        {
            Text = "Manage Payroll / Upkeep";
            Size = new Size(920, 660);
            MinimumSize = new Size(800, 520);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.Sizable;

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                Padding = new Padding(10)
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 110));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));

            root.Controls.Add(CreateSummaryPanel(), 0, 0);
            root.Controls.Add(CreateListPanel(), 0, 1);
            root.Controls.Add(CreateBulkPanel(), 0, 2);
            root.Controls.Add(CreateButtonPanel(), 0, 3);
            Controls.Add(root);
        }

        private Control CreateSummaryPanel()
        {
            var box = new GroupBox
            {
                Text = "Gold + material upkeep (Wood/Stone/Iron/Luxury — Food uses rationing)",
                Dock = DockStyle.Fill,
                Padding = new Padding(8)
            };
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 1
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.3f));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.3f));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.4f));

            _availableLabel = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
            _neededLabel = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
            _shortfallLabel = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = SafeBold(Font)
            };

            layout.Controls.Add(_availableLabel, 0, 0);
            layout.Controls.Add(_neededLabel, 1, 0);
            layout.Controls.Add(_shortfallLabel, 2, 0);
            box.Controls.Add(layout);
            return box;
        }

        private Control CreateListPanel()
        {
            var box = new GroupBox
            {
                Text = "Assigned workers (check = lay off). Red = building lacks materials to operate.",
                Dock = DockStyle.Fill,
                Padding = new Padding(8)
            };

            _workerList = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                CheckBoxes = true,
                GridLines = true,
                MultiSelect = true
            };
            _workerList.Columns.Add("Worker", 140);
            _workerList.Columns.Add("Building", 150);
            _workerList.Columns.Add("Priority", 130);
            _workerList.Columns.Add("Salary", 60);
            _workerList.Columns.Add("Role", 100);
            _workerList.Columns.Add("Status", 160);
            _workerList.ItemChecked += WorkerList_ItemChecked;

            box.Controls.Add(_workerList);
            return box;
        }

        private void WorkerList_ItemChecked(object? sender, ItemCheckedEventArgs e)
        {
            if (_suppressCheckEvents) return;

            if (e.Item?.Tag is UpkeepService.PaidWorker w && w.IsProtected && e.Item.Checked)
            {
                _suppressCheckEvents = true;
                e.Item.Checked = false;
                _suppressCheckEvents = false;
            }

            QueueSummaryUpdate();
        }

        private void QueueSummaryUpdate()
        {
            if (_summaryQueued || !IsHandleCreated) return;
            _summaryQueued = true;
            BeginInvoke(new Action(() =>
            {
                _summaryQueued = false;
                if (_suppressCheckEvents) return;
                UpdateSummaryProjection();
            }));
        }

        private Control CreateBulkPanel()
        {
            var panel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Padding = new Padding(0, 4, 0, 0)
            };

            _autoCutButton = new Button
            {
                Text = "Auto-unassign to afford upkeep",
                AutoSize = true,
                Padding = new Padding(8, 4, 8, 4)
            };
            _autoCutButton.Click += AutoCutButton_Click;

            _unassignSelectedButton = new Button
            {
                Text = "Check selected",
                AutoSize = true,
                Padding = new Padding(8, 4, 8, 4)
            };
            _unassignSelectedButton.Click += (_, _) =>
            {
                _suppressCheckEvents = true;
                foreach (ListViewItem item in _workerList.SelectedItems)
                {
                    if (item.Tag is UpkeepService.PaidWorker w && !w.IsProtected)
                        item.Checked = true;
                }
                _suppressCheckEvents = false;
                UpdateSummaryProjection();
            };

            _resetButton = new Button
            {
                Text = "Reset checks",
                AutoSize = true,
                Padding = new Padding(8, 4, 8, 4)
            };
            _resetButton.Click += (_, _) =>
            {
                _pendingCuts.Clear();
                _suppressCheckEvents = true;
                foreach (ListViewItem item in _workerList.Items)
                    item.Checked = false;
                _suppressCheckEvents = false;
                UpdateSummaryProjection();
            };

            panel.Controls.Add(_autoCutButton);
            panel.Controls.Add(_unassignSelectedButton);
            panel.Controls.Add(_resetButton);
            return panel;
        }

        private Control CreateButtonPanel()
        {
            var panel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false
            };

            _cancelButton = new Button
            {
                Text = "Cancel",
                DialogResult = DialogResult.Cancel,
                Width = 100,
                Height = 30
            };
            _applyButton = new Button
            {
                Text = "Apply layoffs",
                Width = 120,
                Height = 30
            };
            _applyButton.Click += ApplyButton_Click;

            panel.Controls.Add(_cancelButton);
            panel.Controls.Add(_applyButton);
            AcceptButton = _applyButton;
            CancelButton = _cancelButton;
            return panel;
        }

        private void RefreshAll()
        {
            _suppressCheckEvents = true;
            try
            {
                var shutteredIds = new HashSet<string>(
                    UpkeepService.ListShutteredBuildings(_stronghold).Select(b => b.Id));

                _workerList.BeginUpdate();
                _workerList.Items.Clear();

                var buildings = _stronghold.Buildings ?? new List<Building>();
                var workers = buildings
                    .SelectMany(b => UpkeepService.ListPaidWorkers(_stronghold, b))
                    .OrderByDescending(w => (int)w.Priority)
                    .ThenByDescending(w => w.Salary)
                    .ThenBy(w => w.NpcName ?? "", StringComparer.OrdinalIgnoreCase);

                foreach (var w in workers)
                {
                    var item = new ListViewItem(w.NpcName ?? "?") { Tag = w };
                    item.SubItems.Add(w.BuildingName ?? "");
                    item.SubItems.Add(UpkeepService.PriorityLabel(w.Priority));
                    item.SubItems.Add(w.Salary.ToString());
                    item.SubItems.Add(w.IsConstructionCrew ? "Construction" : "Worker");
                    item.SubItems.Add(w.IsProtected
                        ? (w.ProtectReason ?? "Protected")
                        : shutteredIds.Contains(w.BuildingId) ? "No materials" : "Employed");
                    if (w.IsProtected)
                        item.ForeColor = Color.Gray;
                    else if (shutteredIds.Contains(w.BuildingId))
                        item.ForeColor = Color.Firebrick;
                    _workerList.Items.Add(item);
                }

                _workerList.EndUpdate();

                foreach (ListViewItem item in _workerList.Items)
                {
                    if (item.Tag is UpkeepService.PaidWorker w && !w.IsProtected)
                        item.Checked = _pendingCuts.Contains(w.NpcId);
                }
            }
            finally
            {
                // Keep checks suppressed until after deferred ListView events drain.
                if (IsHandleCreated)
                {
                    BeginInvoke(new Action(() =>
                    {
                        _suppressCheckEvents = false;
                        UpdateSummaryProjection();
                    }));
                }
                else
                {
                    _suppressCheckEvents = false;
                    UpdateSummaryProjection();
                }
            }
        }

        private void SyncPendingFromChecks()
        {
            _pendingCuts.Clear();
            foreach (ListViewItem item in _workerList.Items)
            {
                if (item.Checked && item.Tag is UpkeepService.PaidWorker w && !w.IsProtected)
                    _pendingCuts.Add(w.NpcId);
            }
        }

        private void UpdateSummaryProjection()
        {
            if (_availableLabel == null || _neededLabel == null || _shortfallLabel == null)
                return;

            SyncPendingFromChecks();
            var live = UpkeepService.GetGoldPayroll(_stronghold);
            int projectedNeeded = UpkeepService.ProjectedNeededAfterCuts(_stronghold, _pendingCuts);
            int goldShort = Math.Max(0, projectedNeeded - live.Available);
            var shuttered = UpkeepService.ListShutteredBuildings(_stronghold);

            int stillBlocked = 0;
            foreach (var b in shuttered)
            {
                var unpaid = UpkeepService.ListPaidWorkers(_stronghold, b).Where(w => !w.IsProtected).ToList();
                if (unpaid.Count == 0 || unpaid.Any(w => !_pendingCuts.Contains(w.NpcId)))
                    stillBlocked++;
            }

            _availableLabel.Text = $"Available: {live.Available} gold\n(stores + projected production)";
            _neededLabel.Text =
                $"Gold needed: {live.Needed} → after cuts: {projectedNeeded}\n" +
                $"Material-blocked buildings: {shuttered.Count}";

            if (stillBlocked > 0 && shuttered.Count > 0)
            {
                var sample = shuttered[0];
                _shortfallLabel.Text =
                    $"{stillBlocked} building(s) lack materials\n" +
                    UpkeepService.FormatMaterialGaps(sample, _stronghold);
                _shortfallLabel.ForeColor = Color.Firebrick;
            }
            else if (goldShort > 0)
            {
                _shortfallLabel.Text = $"Shortfall: {goldShort} gold";
                _shortfallLabel.ForeColor = Color.Firebrick;
            }
            else if (_pendingCuts.Count > 0)
            {
                _shortfallLabel.Text = "Upkeep covered after these layoffs";
                _shortfallLabel.ForeColor = Color.DarkGreen;
            }
            else
            {
                _shortfallLabel.Text = "Upkeep affordable";
                _shortfallLabel.ForeColor = Color.DarkGreen;
            }

            var shutteredIds = new HashSet<string>(shuttered.Select(b => b.Id));
            foreach (ListViewItem item in _workerList.Items)
            {
                if (item.Tag is not UpkeepService.PaidWorker w || w.IsProtected) continue;
                if (item.SubItems.Count < 6) continue;
                if (item.Checked)
                    item.SubItems[5].Text = "Pending layoff";
                else if (shutteredIds.Contains(w.BuildingId))
                    item.SubItems[5].Text = "No materials";
                else
                    item.SubItems[5].Text = "Employed";
            }
        }

        private void AutoCutButton_Click(object? sender, EventArgs e)
        {
            var plan = UpkeepService.PlanAutoCuts(_stronghold);
            if (plan.Count == 0)
            {
                MessageBox.Show(this,
                    UpkeepService.HasAnyUpkeepShortfall(_stronghold)
                        ? "No more cuttable workers (protected posts only, or unavoidable costs)."
                        : "Gold and material upkeep are already affordable.",
                    "Auto-unassign",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            int shutterCount = UpkeepService.ListShutteredBuildings(_stronghold).Count;
            var confirm = MessageBox.Show(this,
                "Unassign workers until buildings can operate and gold payroll fits:\n\n" +
                "• First: empty buildings that lack Wood/Stone/Iron/Luxury for upkeep\n" +
                "• Then: cut by priority (Construction → Projects → Valuables → Wood/Stone → Food)\n\n" +
                (shutterCount > 0 ? $"Material-blocked buildings now: {shutterCount}\n" : "") +
                $"This would mark {plan.Count} worker(s) for layoff.\n\nContinue?",
                "Auto-unassign to afford upkeep",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (confirm != DialogResult.Yes) return;

            _pendingCuts.Clear();
            foreach (var w in plan)
                _pendingCuts.Add(w.NpcId);

            _suppressCheckEvents = true;
            foreach (ListViewItem item in _workerList.Items)
            {
                if (item.Tag is UpkeepService.PaidWorker w && !w.IsProtected)
                    item.Checked = _pendingCuts.Contains(w.NpcId);
            }
            BeginInvoke(new Action(() =>
            {
                _suppressCheckEvents = false;
                UpdateSummaryProjection();
            }));
        }

        private void ApplyButton_Click(object? sender, EventArgs e)
        {
            SyncPendingFromChecks();
            if (_pendingCuts.Count == 0)
            {
                DialogResult = DialogResult.Cancel;
                Close();
                return;
            }

            int n = _gameStateService.UnassignWorkersForPayroll(_pendingCuts, notify: false);
            if (n == 0)
            {
                MessageBox.Show(this, "No workers were unassigned.", "Manage Payroll",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
