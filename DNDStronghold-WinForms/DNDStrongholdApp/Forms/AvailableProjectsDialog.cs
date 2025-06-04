using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using DNDStrongholdApp.Models;

namespace DNDStrongholdApp.Forms
{
    public class AvailableProjectsDialog : Form
    {
        private readonly Building _building;
        private readonly List<Project> _availableProjects;
        private readonly List<NPC> _allNPCs;
        private readonly List<Resource> _availableResources;
        
        private ListView _projectsListView;
        private TextBox _descriptionTextBox;
        private Label _costsLabel;
        private Label _durationLabel;
        private Button _beginProjectButton;
        private Button _cancelButton;
        
        public Project SelectedProject { get; private set; }

        public AvailableProjectsDialog(Building building, List<Project> availableProjects, List<NPC> allNPCs, List<Resource> availableResources)
        {
            _building = building;
            _availableProjects = availableProjects;
            _allNPCs = allNPCs;
            _availableResources = availableResources;
            
            InitializeComponent();
            LoadProjects();
        }

        private void InitializeComponent()
        {
            this.Text = $"Available Projects - {_building.Name}";
            this.Size = new Size(600, 500);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterParent;

            // Main layout
            TableLayoutPanel mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Padding = new Padding(10)
            };
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 40F)); // Projects list
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 45F)); // Details
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 15F)); // Buttons

            // Projects list view
            GroupBox projectsGroup = new GroupBox
            {
                Text = "Available Projects",
                Dock = DockStyle.Fill,
                Padding = new Padding(10)
            };

            _projectsListView = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                MultiSelect = false
            };

            _projectsListView.Columns.Add("Project Name", 200);
            _projectsListView.Columns.Add("Duration", 100);
            _projectsListView.Columns.Add("Cost", 200);
            _projectsListView.SelectedIndexChanged += ProjectsListView_SelectedIndexChanged;

            projectsGroup.Controls.Add(_projectsListView);

            // Details panel
            GroupBox detailsGroup = new GroupBox
            {
                Text = "Project Details",
                Dock = DockStyle.Fill,
                Padding = new Padding(10)
            };

            TableLayoutPanel detailsLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4
            };

            // Description
            Label descLabel = new Label
            {
                Text = "Description:",
                Dock = DockStyle.Top,
                Height = 20
            };

            _descriptionTextBox = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical
            };

            // Duration
            _durationLabel = new Label
            {
                Text = "Duration: ",
                Dock = DockStyle.Top,
                Height = 20
            };

            // Costs
            _costsLabel = new Label
            {
                Text = "Initial Cost: ",
                Dock = DockStyle.Top,
                Height = 20
            };

            detailsLayout.Controls.Add(descLabel, 0, 0);
            detailsLayout.Controls.Add(_descriptionTextBox, 0, 1);
            detailsLayout.Controls.Add(_durationLabel, 0, 2);
            detailsLayout.Controls.Add(_costsLabel, 0, 3);

            detailsLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 25F));
            detailsLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            detailsLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 25F));
            detailsLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 25F));

            detailsGroup.Controls.Add(detailsLayout);

            // Buttons
            Panel buttonPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Height = 50
            };

            _beginProjectButton = new Button
            {
                Text = "Begin Project",
                Size = new Size(120, 35),
                Anchor = AnchorStyles.Right | AnchorStyles.Top,
                Enabled = false
            };
            _beginProjectButton.Location = new Point(buttonPanel.Width - _beginProjectButton.Width - 140, 10);
            _beginProjectButton.Click += BeginProjectButton_Click;

            _cancelButton = new Button
            {
                Text = "Cancel",
                Size = new Size(80, 35),
                Anchor = AnchorStyles.Right | AnchorStyles.Top,
                DialogResult = DialogResult.Cancel
            };
            _cancelButton.Location = new Point(buttonPanel.Width - _cancelButton.Width - 20, 10);

            buttonPanel.Resize += (s, e) =>
            {
                _beginProjectButton.Location = new Point(buttonPanel.Width - _beginProjectButton.Width - 140, 10);
                _cancelButton.Location = new Point(buttonPanel.Width - _cancelButton.Width - 20, 10);
            };

            buttonPanel.Controls.Add(_beginProjectButton);
            buttonPanel.Controls.Add(_cancelButton);

            // Add to main layout
            mainLayout.Controls.Add(projectsGroup, 0, 0);
            mainLayout.Controls.Add(detailsGroup, 0, 1);
            mainLayout.Controls.Add(buttonPanel, 0, 2);

            this.Controls.Add(mainLayout);
            this.AcceptButton = _beginProjectButton;
            this.CancelButton = _cancelButton;
        }

        private void LoadProjects()
        {
            _projectsListView.Items.Clear();

            foreach (var project in _availableProjects)
            {
                string costText = project.InitialCost.Any() 
                    ? string.Join(", ", project.InitialCost.Select(c => $"{c.Amount} {c.ResourceType}"))
                    : "No cost";

                ListViewItem item = new ListViewItem(project.Name);
                item.SubItems.Add($"{project.Duration} weeks");
                item.SubItems.Add(costText);
                item.Tag = project;

                // Check if we have enough resources
                bool canAfford = true;
                foreach (var cost in project.InitialCost)
                {
                    var resource = _availableResources.Find(r => r.Type == cost.ResourceType);
                    if (resource == null || resource.Amount < cost.Amount)
                    {
                        canAfford = false;
                        break;
                    }
                }

                if (!canAfford)
                {
                    item.ForeColor = Color.Red;
                    item.ToolTipText = "Insufficient resources";
                }

                _projectsListView.Items.Add(item);
            }
        }

        private void ProjectsListView_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_projectsListView.SelectedItems.Count == 0)
            {
                _descriptionTextBox.Text = "";
                _durationLabel.Text = "Duration: ";
                _costsLabel.Text = "Initial Cost: ";
                _beginProjectButton.Enabled = false;
                return;
            }

            var selectedProject = (Project)_projectsListView.SelectedItems[0].Tag;
            
            _descriptionTextBox.Text = selectedProject.Description;
            _durationLabel.Text = $"Duration: {selectedProject.Duration} weeks";
            
            if (selectedProject.InitialCost.Any())
            {
                string costText = string.Join(", ", selectedProject.InitialCost.Select(c => $"{c.Amount} {c.ResourceType}"));
                _costsLabel.Text = $"Initial Cost: {costText}";
            }
            else
            {
                _costsLabel.Text = "Initial Cost: None";
            }

            // Check if we can afford this project
            bool canAfford = true;
            foreach (var cost in selectedProject.InitialCost)
            {
                var resource = _availableResources.Find(r => r.Type == cost.ResourceType);
                if (resource == null || resource.Amount < cost.Amount)
                {
                    canAfford = false;
                    break;
                }
            }

            _beginProjectButton.Enabled = canAfford;
            
            if (!canAfford)
            {
                _beginProjectButton.Text = "Insufficient Resources";
            }
            else
            {
                _beginProjectButton.Text = "Begin Project";
            }
        }

        private void BeginProjectButton_Click(object sender, EventArgs e)
        {
            if (_projectsListView.SelectedItems.Count == 0) return;

            var selectedProject = (Project)_projectsListView.SelectedItems[0].Tag;

            // Check if we have enough resources (double-check)
            foreach (var cost in selectedProject.InitialCost)
            {
                var resource = _availableResources.Find(r => r.Type == cost.ResourceType);
                if (resource == null || resource.Amount < cost.Amount)
                {
                    MessageBox.Show($"Insufficient {cost.ResourceType}. Required: {cost.Amount}, Available: {resource?.Amount ?? 0}",
                        "Cannot Begin Project", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }

            // Show worker assignment dialog
            var assignedWorkers = _building.AssignedWorkers
                .Select(id => _allNPCs.Find(n => n.Id == id))
                .Where(npc => npc != null)
                .ToList();

            if (!assignedWorkers.Any())
            {
                MessageBox.Show("No workers are assigned to this building. Assign workers before starting a project.",
                    "No Workers", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using (var workerDialog = new ProjectWorkerAssignmentDialog(assignedWorkers, selectedProject.Name))
            {
                if (workerDialog.ShowDialog() == DialogResult.OK)
                {
                    // Start the project
                    selectedProject.AssignedWorkers = workerDialog.SelectedWorkerIds;
                    
                    if (_building.StartProject(selectedProject, _availableResources))
                    {
                        SelectedProject = selectedProject;
                        this.DialogResult = DialogResult.OK;
                        this.Close();
                    }
                    else
                    {
                        MessageBox.Show("Failed to start project. Please check resources and building status.",
                            "Project Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }
    }
} 