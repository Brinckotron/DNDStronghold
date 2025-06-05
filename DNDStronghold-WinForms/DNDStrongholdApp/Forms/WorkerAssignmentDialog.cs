using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using DNDStrongholdApp.Models;
using DNDStrongholdApp.Commands;
using System.IO;
using System.Text.Json;
using System.Linq;

namespace DNDStrongholdApp
{
    public class WorkerAssignmentDialog : Form
    {
        private Building _building;
        private List<NPC> _availableNPCs;
        private ListView _availableNPCsListView;
        private ListView _assignedNPCsListView;
        private BuildingInfo _buildingInfo;
        
        public List<string> AssignedWorkerIds { get; private set; } = new List<string>();
        
        public WorkerAssignmentDialog(Building building, List<NPC> allNPCs)
        {
            _building = building;
            
            // Load building info for skill relevance using command
            var loadDataCommand = new LoadBuildingDataCommand();
            var buildingData = loadDataCommand.Execute();
            _buildingInfo = buildingData.buildings.Find(b => b.type == building.Type.ToString());
            
            // Filter NPCs using command
            var filterCommand = new FilterNPCsCommand(allNPCs, n => 
                n.Assignment.Type == AssignmentType.Unassigned || 
                (n.Assignment.Type == AssignmentType.Building && n.Assignment.TargetId == building.Id && !building.DedicatedConstructionCrew.Contains(n.Id)));
            _availableNPCs = filterCommand.Execute();
            
            InitializeComponent();
            LoadNPCs();
        }
        
        private void InitializeComponent()
        {
            this.Text = $"Assign Workers to {_building.Name}";
            this.Size = new Size(800, 500);
            this.StartPosition = FormStartPosition.CenterParent;
            this.MinimizeBox = false;
            this.MaximizeBox = false;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.ShowInTaskbar = false;
            
            // Create layout
            TableLayoutPanel layout = new TableLayoutPanel();
            layout.Dock = DockStyle.Fill;
            layout.ColumnCount = 3;
            layout.RowCount = 2;
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 10F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45F));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 85F));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 15F));
            
            // Available NPCs group
            GroupBox availableGroup = new GroupBox();
            availableGroup.Text = "Available Workers";
            availableGroup.Dock = DockStyle.Fill;
            
            _availableNPCsListView = new ListView();
            _availableNPCsListView.Dock = DockStyle.Fill;
            _availableNPCsListView.View = View.Details;
            _availableNPCsListView.FullRowSelect = true;
            _availableNPCsListView.MultiSelect = false;
            
            // Add columns
            _availableNPCsListView.Columns.Add("Name", 120);
            _availableNPCsListView.Columns.Add("Type", 80);
            _availableNPCsListView.Columns.Add("Level", 50);
            _availableNPCsListView.Columns.Add("Skills", 300);
            
            availableGroup.Controls.Add(_availableNPCsListView);
            
            // Assigned NPCs group
            GroupBox assignedGroup = new GroupBox();
            assignedGroup.Text = $"Assigned Workers ({_building.AssignedWorkers.Count}/{_building.WorkerSlots})";
            assignedGroup.Dock = DockStyle.Fill;
            
            _assignedNPCsListView = new ListView();
            _assignedNPCsListView.Dock = DockStyle.Fill;
            _assignedNPCsListView.View = View.Details;
            _assignedNPCsListView.FullRowSelect = true;
            _assignedNPCsListView.MultiSelect = false;
            
            // Add columns
            _assignedNPCsListView.Columns.Add("Name", 120);
            _assignedNPCsListView.Columns.Add("Type", 80);
            _assignedNPCsListView.Columns.Add("Level", 50);
            _assignedNPCsListView.Columns.Add("Skills", 300);
            
            assignedGroup.Controls.Add(_assignedNPCsListView);
            
            // Button panel
            Panel buttonPanel = new Panel();
            buttonPanel.Dock = DockStyle.Fill;
            buttonPanel.Padding = new Padding(0);
            
            Button assignButton = new Button();
            assignButton.Text = ">";
            assignButton.Size = new Size(40, 30);
            assignButton.Anchor = AnchorStyles.None;
            assignButton.Click += AssignButton_Click;
            
            Button unassignButton = new Button();
            unassignButton.Text = "<";
            unassignButton.Size = new Size(40, 30);
            unassignButton.Anchor = AnchorStyles.None;
            unassignButton.Click += UnassignButton_Click;
            
            // Use a TableLayoutPanel to center the buttons vertically
            TableLayoutPanel buttonLayout = new TableLayoutPanel();
            buttonLayout.Dock = DockStyle.Fill;
            buttonLayout.RowCount = 2;
            buttonLayout.ColumnCount = 1;
            buttonLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            buttonLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            buttonLayout.Controls.Add(assignButton, 0, 0);
            buttonLayout.Controls.Add(unassignButton, 0, 1);
            buttonPanel.Controls.Add(buttonLayout);
            
            // OK/Cancel buttons
            Panel bottomPanel = new TableLayoutPanel();
            bottomPanel.Dock = DockStyle.Fill;
            
            Button okButton = new Button();
            okButton.Text = "OK";
            okButton.DialogResult = DialogResult.OK;
            okButton.Size = new Size(80, 30);
            okButton.Location = new Point(bottomPanel.Width - 180, 10);
            okButton.Click += OkButton_Click;
            
            Button cancelButton = new Button();
            cancelButton.Text = "Cancel";
            cancelButton.DialogResult = DialogResult.Cancel;
            cancelButton.Size = new Size(80, 30);
            cancelButton.Location = new Point(bottomPanel.Width - 90, 10);
            
            bottomPanel.Controls.Add(okButton);
            bottomPanel.Controls.Add(cancelButton);
            
            // Add controls to layout
            layout.Controls.Add(availableGroup, 0, 0);
            layout.Controls.Add(buttonPanel, 1, 0);
            layout.Controls.Add(assignedGroup, 2, 0);
            layout.Controls.Add(bottomPanel, 0, 1);
            layout.SetColumnSpan(bottomPanel, 3);
            
            this.Controls.Add(layout);
            this.AcceptButton = okButton;
            this.CancelButton = cancelButton;
        }
        
        private void LoadNPCs()
        {
            // Clear lists
            _availableNPCsListView.Items.Clear();
            _assignedNPCsListView.Items.Clear();
            
            foreach (var npc in _availableNPCs)
            {
                ListViewItem item = new ListViewItem(npc.Name);
                item.SubItems.Add(npc.Type.ToString());
                item.SubItems.Add(npc.Level.ToString());
                
                // Get relevant skills
                var relevantSkills = new List<string>();
                if (_buildingInfo != null)
                {
                    // Check primary skill first
                                var primarySkill = npc.Skills.FirstOrDefault(s => s.Name == _buildingInfo.primarySkill);
                                if (primarySkill != null)
                                    relevantSkills.Add($"{primarySkill.Name} ({primarySkill.Level})");
                                
                                // Then secondary skill
                                var secondarySkill = npc.Skills.FirstOrDefault(s => s.Name == _buildingInfo.secondarySkill);
                                if (secondarySkill != null)
                                    relevantSkills.Add($"{secondarySkill.Name} ({secondarySkill.Level})");
                                
                                // Finally tertiary skill
                                var tertiarySkill = npc.Skills.FirstOrDefault(s => s.Name == _buildingInfo.tertiarySkill);
                                if (tertiarySkill != null)
                                    relevantSkills.Add($"{tertiarySkill.Name} ({tertiarySkill.Level})");
                }
                item.SubItems.Add(string.Join(", ", relevantSkills));
                item.Tag = npc.Id;
                
                // Add to appropriate list
                if (_building.AssignedWorkers.Contains(npc.Id))
                {
                    // If worker is assigned to a project, make them unselectable
                    if (_building.CurrentProject?.AssignedWorkers.Contains(npc.Id) ?? false)
                    {
                        item.ForeColor = Color.Gray;
                        item.SubItems[0].Text += " (Project)";
                    }
                    _assignedNPCsListView.Items.Add(item);
                }
                else
                {
                    _availableNPCsListView.Items.Add(item);
                }
            }
            
            // Update assigned workers count in group box title
            ((GroupBox)_assignedNPCsListView.Parent).Text = $"Assigned Workers ({_assignedNPCsListView.Items.Count}/{_building.WorkerSlots})";
        }
        
        private void AssignButton_Click(object sender, EventArgs e)
        {
            if (_availableNPCsListView.SelectedItems.Count > 0)
            {
                // Check if we're at max capacity
                if (_assignedNPCsListView.Items.Count >= _building.WorkerSlots)
                {
                    MessageBox.Show($"This building can only have {_building.WorkerSlots} workers assigned.", 
                        "Maximum Workers Reached", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                
                // Move selected NPC from available to assigned
                ListViewItem selectedItem = _availableNPCsListView.SelectedItems[0];
                _availableNPCsListView.Items.Remove(selectedItem);
                _assignedNPCsListView.Items.Add(selectedItem);
                
                // Update assigned workers count in group box title
                ((GroupBox)_assignedNPCsListView.Parent).Text = $"Assigned Workers ({_assignedNPCsListView.Items.Count}/{_building.WorkerSlots})";
            }
        }
        
        private void UnassignButton_Click(object sender, EventArgs e)
        {
            if (_assignedNPCsListView.SelectedItems.Count > 0)
            {
                ListViewItem selectedItem = _assignedNPCsListView.SelectedItems[0];
                
                // Check if worker is assigned to a project
                if (_building.CurrentProject?.AssignedWorkers.Contains((string)selectedItem.Tag) ?? false)
                {
                    MessageBox.Show("Cannot unassign workers that are assigned to a project.",
                        "Worker Assigned to Project",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                }
                
                // Move selected NPC from assigned to available
                _assignedNPCsListView.Items.Remove(selectedItem);
                _availableNPCsListView.Items.Add(selectedItem);
                
                // Update assigned workers count in group box title
                ((GroupBox)_assignedNPCsListView.Parent).Text = $"Assigned Workers ({_assignedNPCsListView.Items.Count}/{_building.WorkerSlots})";
            }
        }
        
        private void OkButton_Click(object sender, EventArgs e)
        {
            // Save assigned worker IDs, preserving project workers
            AssignedWorkerIds.Clear();
            
            // First add project workers if any
            if (_building.CurrentProject != null)
            {
                AssignedWorkerIds.AddRange(_building.CurrentProject.AssignedWorkers);
            }
            
            // Then add other assigned workers
            foreach (ListViewItem item in _assignedNPCsListView.Items)
            {
                string workerId = (string)item.Tag;
                if (!AssignedWorkerIds.Contains(workerId))
                {
                    AssignedWorkerIds.Add(workerId);
                }
            }
        }
    }
} 