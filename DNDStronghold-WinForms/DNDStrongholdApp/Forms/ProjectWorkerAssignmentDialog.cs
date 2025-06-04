using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using DNDStrongholdApp.Models;

namespace DNDStrongholdApp.Forms
{
    public class ProjectWorkerAssignmentDialog : Form
    {
        private readonly List<NPC> _availableWorkers;
        private readonly string _projectName;
        
        private ListView _workersListView;
        private Button _assignButton;
        private Button _cancelButton;
        private Label _instructionLabel;
        
        public List<string> SelectedWorkerIds { get; private set; } = new List<string>();

        public ProjectWorkerAssignmentDialog(List<NPC> availableWorkers, string projectName)
        {
            _availableWorkers = availableWorkers;
            _projectName = projectName;
            
            InitializeComponent();
            LoadWorkers();
        }

        private void InitializeComponent()
        {
            this.Text = $"Assign Workers to Project: {_projectName}";
            this.Size = new Size(500, 400);
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
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50F)); // Instructions
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F)); // Workers list
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50F)); // Buttons

            // Instructions
            _instructionLabel = new Label
            {
                Text = "Select which workers you want to assign to this project.\nAssigned workers will not contribute to building production while working on the project.",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.TopLeft
            };

            // Workers list view
            GroupBox workersGroup = new GroupBox
            {
                Text = "Available Workers",
                Dock = DockStyle.Fill,
                Padding = new Padding(10)
            };

            _workersListView = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                CheckBoxes = true
            };

            _workersListView.Columns.Add("Worker", 150);
            _workersListView.Columns.Add("Type", 100);
            _workersListView.Columns.Add("Level", 60);
            _workersListView.Columns.Add("Skills", 150);
            _workersListView.ItemChecked += WorkersListView_ItemChecked;

            workersGroup.Controls.Add(_workersListView);

            // Buttons
            Panel buttonPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Height = 50
            };

            _assignButton = new Button
            {
                Text = "Assign Workers",
                Size = new Size(120, 35),
                Anchor = AnchorStyles.Right | AnchorStyles.Top,
                Enabled = false
            };
            _assignButton.Location = new Point(buttonPanel.Width - _assignButton.Width - 140, 10);
            _assignButton.Click += AssignButton_Click;

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
                _assignButton.Location = new Point(buttonPanel.Width - _assignButton.Width - 140, 10);
                _cancelButton.Location = new Point(buttonPanel.Width - _cancelButton.Width - 20, 10);
            };

            buttonPanel.Controls.Add(_assignButton);
            buttonPanel.Controls.Add(_cancelButton);

            // Add to main layout
            mainLayout.Controls.Add(_instructionLabel, 0, 0);
            mainLayout.Controls.Add(workersGroup, 0, 1);
            mainLayout.Controls.Add(buttonPanel, 0, 2);

            this.Controls.Add(mainLayout);
            this.AcceptButton = _assignButton;
            this.CancelButton = _cancelButton;
        }

        private void LoadWorkers()
        {
            _workersListView.Items.Clear();

            foreach (var worker in _availableWorkers)
            {
                var item = new ListViewItem(worker.Name);
                item.SubItems.Add(worker.Type.ToString());
                item.SubItems.Add(worker.Level.ToString());
                
                // Get worker's skills
                var skillsText = worker.Skills.Any() 
                    ? string.Join(", ", worker.Skills.Select(s => $"{s.Name} ({s.Level})"))
                    : "No skills";
                item.SubItems.Add(skillsText);
                
                item.Tag = worker.Id;
                _workersListView.Items.Add(item);
            }
        }

        private void WorkersListView_ItemChecked(object sender, ItemCheckedEventArgs e)
        {
            UpdateSelectedWorkers();
            _assignButton.Enabled = SelectedWorkerIds.Any();
            
            // Update button text to show count
            if (SelectedWorkerIds.Any())
            {
                _assignButton.Text = $"Assign {SelectedWorkerIds.Count} Worker{(SelectedWorkerIds.Count == 1 ? "" : "s")}";
            }
            else
            {
                _assignButton.Text = "Assign Workers";
            }
        }

        private void UpdateSelectedWorkers()
        {
            SelectedWorkerIds.Clear();
            
            foreach (ListViewItem item in _workersListView.Items)
            {
                if (item.Checked)
                {
                    SelectedWorkerIds.Add((string)item.Tag);
                }
            }
        }

        private void AssignButton_Click(object sender, EventArgs e)
        {
            if (!SelectedWorkerIds.Any())
            {
                MessageBox.Show("Please select at least one worker to assign to the project.",
                    "No Workers Selected", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            UpdateSelectedWorkers();
            this.DialogResult = DialogResult.OK;
            this.Close();
        }
    }
} 