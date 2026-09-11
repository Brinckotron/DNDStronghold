using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using DNDStrongholdApp.Models;
using DNDStrongholdApp.Services;

namespace DNDStrongholdApp.Forms
{
    public class AvailableProjectsDialog : Form
    {
        private readonly Building _building;
        private readonly BuildingInfo? _buildingInfo;
        private readonly List<Project> _availableProjects;
        private readonly List<NPC> _allNPCs;
        private readonly List<Resource> _availableResources;
        private readonly Stronghold _stronghold;

        private ListView _projectsListView;
        private TextBox _descriptionTextBox;
        private Label _costsLabel;
        private Label _durationLabel;
        private Label _bonusLabel;
        private Label _hintLabel;
        private ListView _workersListView;
        private ComboBox _difficultyCombo;
        private TextBox _commissionBox;
        private ListView _routeList;
        private Panel _establishPanel;
        private Panel _missionPanel;
        private Button _beginProjectButton;
        private Button _cancelButton;
        private Panel _setupPanel;
        private readonly string? _preselectProjectName;
        private readonly string? _preselectRouteId;
        private readonly bool _checkFirstWorker;

        public Project SelectedProject { get; private set; }

        public AvailableProjectsDialog(
            Building building,
            List<Project> availableProjects,
            List<NPC> allNPCs,
            List<Resource> availableResources,
            Stronghold stronghold,
            string? preselectProjectName = null,
            string? preselectRouteId = null,
            bool checkFirstWorker = false)
        {
            _building = building;
            _availableProjects = availableProjects;
            _allNPCs = allNPCs;
            _availableResources = availableResources;
            _stronghold = stronghold;
            _preselectProjectName = preselectProjectName;
            _preselectRouteId = preselectRouteId;
            _checkFirstWorker = checkFirstWorker;
            var data = new Commands.LoadBuildingDataCommand().Execute();
            _buildingInfo = building != null ? data.buildings.Find(b => b.type == building.TypeName) : null;

            InitializeComponent();
            LoadProjects();
            Shown += (_, _) => ApplyInitialSelection();
        }

        private Project? CurrentSelection =>
            _projectsListView.SelectedItems.Count == 0 ? null : _projectsListView.SelectedItems[0].Tag as Project;

        private void InitializeComponent()
        {
            this.Text = $"Available Projects - {_building.Name}";
            this.Size = new Size(980, 860);
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterParent;
            this.MinimumSize = new Size(860, 720);

            var mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                Padding = new Padding(10)
            };
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 36F));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 90F));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 64F));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50F));

            var projectsGroup = new GroupBox { Text = "Available Projects", Dock = DockStyle.Fill, Padding = new Padding(8) };
            _projectsListView = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                MultiSelect = false
            };
            _projectsListView.Columns.Add("Project", 200);
            _projectsListView.Columns.Add("Duration", 90);
            _projectsListView.Columns.Add("Cost", 180);
            _projectsListView.Columns.Add("Roll", 80);
            _projectsListView.SelectedIndexChanged += (_, _) => RefreshSetup();
            projectsGroup.Controls.Add(_projectsListView);

            var detailsGroup = new GroupBox { Text = "Details", Dock = DockStyle.Fill, Padding = new Padding(8) };
            var detailsLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
            _descriptionTextBox = new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical };
            _durationLabel = new Label { Text = "Duration:", Dock = DockStyle.Fill, Height = 18 };
            _costsLabel = new Label { Text = "Cost:", Dock = DockStyle.Fill, Height = 18 };
            detailsLayout.Controls.Add(_descriptionTextBox, 0, 0);
            detailsLayout.Controls.Add(_durationLabel, 0, 1);
            detailsLayout.Controls.Add(_costsLabel, 0, 2);
            detailsLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            detailsLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 20F));
            detailsLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 20F));
            detailsGroup.Controls.Add(detailsLayout);

            _setupPanel = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
            BuildSetupControls();

            var buttonPanel = new Panel { Dock = DockStyle.Fill };
            _beginProjectButton = new Button
            {
                Text = "Begin Project",
                Size = new Size(140, 35),
                Anchor = AnchorStyles.Right | AnchorStyles.Top,
                Enabled = false
            };
            _cancelButton = new Button
            {
                Text = "Cancel",
                Size = new Size(90, 35),
                Anchor = AnchorStyles.Right | AnchorStyles.Top,
                DialogResult = DialogResult.Cancel
            };
            buttonPanel.Resize += (_, _) =>
            {
                _beginProjectButton.Location = new Point(buttonPanel.Width - 250, 8);
                _cancelButton.Location = new Point(buttonPanel.Width - 100, 8);
            };
            _beginProjectButton.Click += BeginProjectButton_Click;
            buttonPanel.Controls.Add(_beginProjectButton);
            buttonPanel.Controls.Add(_cancelButton);

            mainLayout.Controls.Add(projectsGroup, 0, 0);
            mainLayout.Controls.Add(detailsGroup, 0, 1);
            mainLayout.Controls.Add(_setupPanel, 0, 2);
            mainLayout.Controls.Add(buttonPanel, 0, 3);

            this.Controls.Add(mainLayout);
            this.CancelButton = _cancelButton;
        }

        private void BuildSetupControls()
        {
            _setupPanel.Controls.Clear();
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 2,
                Padding = new Padding(4)
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58F));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28F));

            var workersGroup = new GroupBox { Text = "Workers (check at least one)", Dock = DockStyle.Fill, Padding = new Padding(6) };
            _workersListView = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                CheckBoxes = true,
                FullRowSelect = true,
                GridLines = true
            };
            _workersListView.Columns.Add("Worker", 140);
            _workersListView.Columns.Add("Type", 90);
            _workersListView.Columns.Add("Skills", 180);
            _workersListView.ItemChecked += (_, _) => UpdateLiveFields();
            workersGroup.Controls.Add(_workersListView);

            var extras = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5 };
            extras.RowStyles.Add(new RowStyle(SizeType.Absolute, 24F));
            extras.RowStyles.Add(new RowStyle(SizeType.Absolute, 48F));
            extras.RowStyles.Add(new RowStyle(SizeType.Absolute, 48F));
            extras.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            extras.RowStyles.Add(new RowStyle(SizeType.Absolute, 0F));

            _bonusLabel = new Label { Text = "Bonus: +0", AutoSize = true, Dock = DockStyle.Fill };

            var difficultyRow = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
            difficultyRow.Controls.Add(new Label { Text = "Difficulty:", AutoSize = true, Padding = new Padding(0, 6, 8, 0) });
            _difficultyCombo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 160 };
            foreach (DifficultyTier tier in Enum.GetValues<DifficultyTier>())
                _difficultyCombo.Items.Add(tier.DisplayName());
            _difficultyCombo.SelectedIndex = 2;
            _difficultyCombo.SelectedIndexChanged += (_, _) => UpdateLiveFields();
            difficultyRow.Controls.Add(_difficultyCombo);

            var commissionRow = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
            commissionRow.Controls.Add(new Label { Text = "Commission:", AutoSize = true, Padding = new Padding(0, 6, 8, 0) });
            _commissionBox = new TextBox { Width = 220 };
            commissionRow.Controls.Add(_commissionBox);

            _establishPanel = BuildEstablishPanel();
            _missionPanel = BuildMissionPanel();
            _establishPanel.Visible = false;
            _missionPanel.Visible = false;

            var optionsHost = new Panel { Dock = DockStyle.Fill };
            _establishPanel.Dock = DockStyle.Fill;
            _missionPanel.Dock = DockStyle.Fill;
            optionsHost.Controls.Add(_missionPanel);
            optionsHost.Controls.Add(_establishPanel);

            extras.Controls.Add(_bonusLabel, 0, 0);
            extras.Controls.Add(difficultyRow, 0, 1);
            extras.Controls.Add(commissionRow, 0, 2);
            extras.Controls.Add(optionsHost, 0, 3);

            _hintLabel = new Label { Dock = DockStyle.Fill, ForeColor = Color.DimGray, Text = "" };

            layout.Controls.Add(workersGroup, 0, 0);
            layout.SetRowSpan(workersGroup, 2);
            layout.Controls.Add(extras, 1, 0);
            layout.Controls.Add(_hintLabel, 1, 1);
            _setupPanel.Controls.Add(layout);

            LoadWorkers();
            LoadRoutes();
        }

        private Panel BuildEstablishPanel()
        {
            var panel = new Panel { Dock = DockStyle.Fill };
            var box = new GroupBox
            {
                Text = "Destination",
                Dock = DockStyle.Fill,
                Padding = new Padding(10)
            };
            var label = new Label
            {
                Dock = DockStyle.Fill,
                Text = "Click Begin Project to choose a destination from the catalog, or create a custom route with its own rates, demand, and specialty."
            };
            box.Controls.Add(label);
            panel.Controls.Add(box);
            return panel;
        }

        private Panel BuildMissionPanel()
        {
            var panel = new Panel { Dock = DockStyle.Fill };
            var box = new GroupBox { Text = "Established route", Dock = DockStyle.Fill, Padding = new Padding(6) };
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48F));

            _routeList = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                MultiSelect = false
            };
            _routeList.Columns.Add("Route", 140);
            _routeList.Columns.Add("Weeks", 60);
            _routeList.Columns.Add("Status", 140);
            _routeList.SelectedIndexChanged += (_, _) => UpdateLiveFields();

            var hint = new Label
            {
                Dock = DockStyle.Fill,
                Text = "Begin Project opens a cargo dialog for the selected route: rates, what you send, and what to bring back."
            };

            layout.Controls.Add(_routeList, 0, 0);
            layout.Controls.Add(hint, 0, 1);
            box.Controls.Add(layout);
            panel.Controls.Add(box);
            return panel;
        }

        private void LoadWorkers()
        {
            _workersListView.Items.Clear();
            var assigned = _building.AssignedWorkers
                .Select(id => _allNPCs.Find(n => n.Id == id))
                .Where(n => n != null)
                .Cast<NPC>();
            foreach (var worker in assigned)
            {
                var item = new ListViewItem(worker.Name);
                item.SubItems.Add(worker.Type.ToString());
                var skillsText = worker.Skills.Any()
                    ? string.Join(", ", worker.Skills.Where(s => s.Level > 0).Select(s => $"{s.Name} {s.Level}"))
                    : "—";
                item.SubItems.Add(skillsText);
                item.Tag = worker.Id;
                _workersListView.Items.Add(item);
            }
        }

        private void LoadRoutes()
        {
            _routeList.Items.Clear();
            foreach (var route in (_stronghold.TradeRoutes ?? new List<TradeRoute>()).Where(r => r.Status == TradeRouteStatus.Open))
            {
                bool busy = route.IsOccupied;
                bool quarantine = TradeService.HasRouteEvent(_stronghold, route, TradeMarketEventKind.Quarantine);
                var item = new ListViewItem(route.Name);
                item.SubItems.Add($"{route.DistanceWeeks}w");
                item.SubItems.Add(busy ? "Caravan in transit" : quarantine ? "Quarantine" : "Ready");
                item.Tag = route.Id;
                if (busy || quarantine) item.ForeColor = Color.Gray;
                _routeList.Items.Add(item);
            }
        }

        private void LoadProjects()
        {
            _projectsListView.Items.Clear();
            foreach (var project in _availableProjects)
            {
                string costText = project.InitialCost.Any()
                    ? ProjectResolutionService.FormatCosts(project.InitialCost)
                    : (project.RequiresCargo ? "Cargo (set below)" : "No cost");
                string duration = project.Duration > 0 ? $"{project.Duration} wk" : "From destination";
                var item = new ListViewItem(project.Name);
                item.SubItems.Add(duration);
                item.SubItems.Add(costText);
                item.SubItems.Add(project.RollMode.ToString());
                item.Tag = project;
                _projectsListView.Items.Add(item);
            }
        }

        private void ApplyInitialSelection()
        {
            if (!string.IsNullOrEmpty(_preselectProjectName))
            {
                foreach (ListViewItem item in _projectsListView.Items)
                {
                    if (item.Tag is Project project &&
                        string.Equals(project.Name, _preselectProjectName, StringComparison.OrdinalIgnoreCase))
                    {
                        item.Selected = true;
                        item.Focused = true;
                        item.EnsureVisible();
                        break;
                    }
                }
            }

            if (_checkFirstWorker && _workersListView.Items.Count > 0)
            {
                var first = _workersListView.Items[0];
                first.Checked = true;
                first.Selected = true;
                first.Focused = true;
            }
        }

        private List<string> SelectedWorkerIds()
        {
            return _workersListView.Items.Cast<ListViewItem>()
                .Where(i => i.Checked)
                .Select(i => (string)i.Tag)
                .ToList();
        }

        private TradeRoute? SelectedRoute()
        {
            if (_routeList.SelectedItems.Count == 0) return null;
            string id = (string)_routeList.SelectedItems[0].Tag;
            return _stronghold.TradeRoutes.Find(r => r.Id == id);
        }

        private void RefreshSetup()
        {
            var project = CurrentSelection;
            if (project == null)
            {
                _descriptionTextBox.Text = "";
                _durationLabel.Text = "Duration:";
                _costsLabel.Text = "Cost:";
                _beginProjectButton.Enabled = false;
                _establishPanel.Visible = false;
                _missionPanel.Visible = false;
                _hintLabel.Text = "Select a project.";
                return;
            }

            _descriptionTextBox.Text = project.Description;
            _difficultyCombo.Enabled = project.RequiresDifficulty;
            _commissionBox.Enabled = project.RequiresCommission || project.OutcomeType != ProjectOutcomeType.InApp;

            bool isEstablish = string.Equals(project.Name, "Establish Trade Route", StringComparison.OrdinalIgnoreCase);
            bool isMission = string.Equals(project.Name, "Trade Mission", StringComparison.OrdinalIgnoreCase);
            _establishPanel.Visible = isEstablish;
            _missionPanel.Visible = isMission;
            if (isMission)
            {
                LoadRoutes();
                if (_routeList.Items.Count > 0 && _routeList.SelectedItems.Count == 0)
                {
                    ListViewItem? preferred = null;
                    if (!string.IsNullOrEmpty(_preselectRouteId))
                    {
                        preferred = _routeList.Items.Cast<ListViewItem>()
                            .FirstOrDefault(i => string.Equals((string)i.Tag, _preselectRouteId, StringComparison.Ordinal));
                    }
                    preferred ??= _routeList.Items.Cast<ListViewItem>().FirstOrDefault(i => i.ForeColor != Color.Gray);
                    var pick = preferred ?? _routeList.Items[0];
                    pick.Selected = true;
                    pick.Focused = true;
                    pick.EnsureVisible();
                }
            }

            UpdateLiveFields();
        }

        private void UpdateLiveFields()
        {
            var project = CurrentSelection;
            if (project == null) return;

            project.AssignedWorkers = SelectedWorkerIds();
            int bonus = ProjectResolutionService.ComputeBonus(_building, _buildingInfo, project, _allNPCs);
            string extra = "";
            int duration = project.Duration;
            List<ResourceCost> cost = project.InitialCost.ToList();

            var route = SelectedRoute();
            if (string.Equals(project.Name, "Trade Mission", StringComparison.OrdinalIgnoreCase) && route != null)
            {
                duration = route.DistanceWeeks;
                extra = $"  Route: {route.Name}";
            }

            if (project.RequiresDifficulty)
            {
                var tier = (DifficultyTier)_difficultyCombo.SelectedIndex;
                extra += $"  DC {tier.GetDC()} ({tier.DisplayName()})";
            }

            _bonusLabel.Text = $"Roll bonus: +{bonus}{extra}";
            _durationLabel.Text = duration > 0 ? $"Duration: {duration} weeks" : "Duration: from destination";
            _costsLabel.Text = $"Cost: {ProjectResolutionService.FormatCosts(cost)}";

            bool workersOk = project.AssignedWorkers.Count >= Math.Max(1, project.MinWorkers);
            if (project.MaxWorkers > 0 && project.AssignedWorkers.Count > project.MaxWorkers)
                workersOk = false;

            bool destOk = true;
            if (string.Equals(project.Name, "Trade Mission", StringComparison.OrdinalIgnoreCase))
                destOk = route != null && TradeService.CanSendCaravan(route, _stronghold);

            bool canAfford = CanAfford(cost);
            _beginProjectButton.Enabled = workersOk && destOk && canAfford;
            _beginProjectButton.Text = !canAfford ? "Insufficient Resources" : "Begin Project";

            if (!workersOk)
                _hintLabel.Text = "Check at least one worker assigned to this building.";
            else if (string.Equals(project.Name, "Establish Trade Route", StringComparison.OrdinalIgnoreCase))
                _hintLabel.Text = "Begin Project opens a destination list (catalog or custom route).";
            else if (string.Equals(project.Name, "Trade Fair", StringComparison.OrdinalIgnoreCase))
                _hintLabel.Text = "Begin Project asks which good the fair is focused on.";
            else if (string.Equals(project.Name, "Trade Mission", StringComparison.OrdinalIgnoreCase) && _routeList.Items.Count == 0)
                _hintLabel.Text = "No open trade routes. Finish Establish Trade Route first.";
            else if (string.Equals(project.Name, "Trade Mission", StringComparison.OrdinalIgnoreCase) && !destOk)
            {
                _hintLabel.Text = route != null && TradeService.HasRouteEvent(_stronghold, route, TradeMarketEventKind.Quarantine)
                    ? "This settlement is under quarantine. No caravan can leave until it lifts."
                    : "Select an open route that is not already carrying a caravan.";
            }
            else if (string.Equals(project.Name, "Trade Mission", StringComparison.OrdinalIgnoreCase) && destOk)
                _hintLabel.Text = "Begin Project opens the cargo dialog for this route's rates.";
            else if (!canAfford)
                _hintLabel.Text = "Not enough resources for the listed cost.";
            else
                _hintLabel.Text = "";
        }

        private bool CanAfford(List<ResourceCost> costs)
        {
            foreach (var cost in costs)
            {
                var resource = _availableResources.Find(r => r.Type == cost.ResourceType);
                if (resource == null || resource.Amount < cost.Amount) return false;
            }
            return true;
        }

        private void BeginProjectButton_Click(object sender, EventArgs e)
        {
            var project = CurrentSelection;
            if (project == null) return;

            var workers = SelectedWorkerIds();
            if (workers.Count == 0)
            {
                MessageBox.Show("Assign at least one worker.", "No Workers", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            project.AssignedWorkers = workers;
            if (project.RequiresDifficulty)
            {
                project.Difficulty = (DifficultyTier)_difficultyCombo.SelectedIndex;
                project.DC = project.Difficulty.GetDC();
            }
            project.Commission = _commissionBox.Text.Trim();

            if (string.Equals(project.Name, "Trade Fair", StringComparison.OrdinalIgnoreCase))
            {
                using var focusDialog = new TradeFairFocusDialog(_building.Level);
                if (focusDialog.ShowDialog(this) != DialogResult.OK)
                    return;

                var focus = focusDialog.SelectedResource;
                project.FairFocusResource = focus;
                int amount = ProjectResolutionService.RollTradeFairFocusAmount(focus);
                project.ExpectedReturn = amount > 0
                    ? new List<ResourceCost> { new ResourceCost { ResourceType = focus, Amount = amount } }
                    : new List<ResourceCost>();
            }
            else if (string.Equals(project.Name, "Establish Trade Route", StringComparison.OrdinalIgnoreCase))
            {
                using var picker = new TradeDestinationPickerDialog(_stronghold);
                if (picker.ShowDialog(this) != DialogResult.OK || picker.SelectedDestination == null)
                    return;

                var dest = picker.SelectedDestination;
                if (TradeService.HasOpenRouteTo(_stronghold, dest.Id))
                {
                    MessageBox.Show($"A route to {dest.Name} is already open.", "Destination",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                if (TradeService.HasPendingEstablish(_stronghold, dest.Id, _building.Id))
                {
                    MessageBox.Show($"Another Trade Office is already establishing a route to {dest.Name}.",
                        "Destination", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                project.TradeDestinationId = dest.Id;
                project.CustomDestination = dest;
                project.Duration = Math.Max(1, dest.DistanceWeeks);
                project.TimeRemaining = project.Duration;
            }
            else if (string.Equals(project.Name, "Trade Mission", StringComparison.OrdinalIgnoreCase))
            {
                var route = SelectedRoute();
                if (route == null || !TradeService.CanSendCaravan(route, _stronghold))
                {
                    string message = route != null && TradeService.HasRouteEvent(_stronghold, route, TradeMarketEventKind.Quarantine)
                        ? "This settlement is under quarantine. No caravan can leave until it lifts."
                        : "Select an open route that is not already carrying a caravan.";
                    MessageBox.Show(message, "Route", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                using var cargoDialog = new TradeMissionCargoDialog(
                    route, _stronghold, _allNPCs, _availableResources, workers);
                if (cargoDialog.ShowDialog(this) != DialogResult.OK)
                    return;

                project.TradeRouteId = route.Id;
                project.CargoOut = cargoDialog.CargoOut;
                project.RequestedReturns = cargoDialog.RequestedReturns;
                project.ExpectedReturn = cargoDialog.ExpectedReturn;
                project.InitialCost = cargoDialog.CargoOut;
                project.Duration = route.DistanceWeeks;
                project.TimeRemaining = project.Duration;
            }

            if (!_building.StartProject(project, _availableResources))
            {
                MessageBox.Show("Failed to start project. Check resources and building status.", "Project Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (!string.IsNullOrEmpty(project.TradeRouteId))
            {
                var occupied = _stronghold.TradeRoutes.Find(r => r.Id == project.TradeRouteId);
                if (occupied != null)
                    occupied.ActiveMissionProjectId = project.Id;
            }

            var gold = _availableResources.Find(r => r.Type == ResourceType.Gold);
            if (gold != null)
                _stronghold.Treasury = gold.Amount;

            SelectedProject = project;
            this.DialogResult = DialogResult.OK;
            this.Close();
        }
    }
}
