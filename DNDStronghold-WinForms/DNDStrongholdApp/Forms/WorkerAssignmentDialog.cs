using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using DNDStrongholdApp.Models;

namespace DNDStrongholdApp
{
    public class WorkerAssignmentDialog : Form
    {
        private Building _building;
        private List<NPC> _availableNPCs;
        private ListView _availableNPCsListView;
        private ListView _assignedNPCsListView;
        
        public List<string> AssignedWorkerIds { get; private set; } = new List<string>();
        
        public WorkerAssignmentDialog(Building building, List<NPC> allNPCs)
        {
            _building = building;
            
            // Filter NPCs to only include those that are unassigned or assigned to this building
            _availableNPCs = allNPCs.FindAll(n => 
                n.Assignment.Type == AssignmentType.Unassigned || 
                (n.Assignment.Type == AssignmentType.Building && n.Assignment.TargetId == building.Id));
            
            InitializeComponent();
            LoadNPCs();
        }
        
        private void InitializeComponent()
        {
            this.Text = $"Assign Workers to {_building.Name}";
            this.Size = new Size(700, 500);
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
            _availableNPCsListView.Columns.Add("Skills", 100);
            
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
            _assignedNPCsListView.Columns.Add("Skills", 100);
            
            assignedGroup.Controls.Add(_assignedNPCsListView);
            
            // Button panel
            Panel buttonPanel = new Panel();
            buttonPanel.Dock = DockStyle.Fill;
            
            Button assignButton = new Button();
            assignButton.Text = "Assign >";
            assignButton.Size = new Size(80, 30);
            assignButton.Location = new Point(10, 20);
            assignButton.Click += AssignButton_Click;
            
            Button unassignButton = new Button();
            unassignButton.Text = "< Unassign";
            unassignButton.Size = new Size(80, 30);
            unassignButton.Location = new Point(10, 60);
            unassignButton.Click += UnassignButton_Click;
            
            buttonPanel.Controls.Add(assignButton);
            buttonPanel.Controls.Add(unassignButton);
            
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
                item.SubItems.Add(GetTopSkills(npc));
                item.Tag = npc.Id;
                
                // Add to appropriate list
                if (_building.AssignedWorkers.Contains(npc.Id))
                {
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
        
        private string GetTopSkills(NPC npc)
        {
            // Get top 2 skills
            string skills = "";
            int count = 0;
            
            foreach (var skill in npc.Skills)
            {
                skills += $"{skill.Name}: {skill.Level}, ";
                count++;
                
                if (count >= 2) break;
            }
            
            return skills.TrimEnd(',', ' ');
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
                // Move selected NPC from assigned to available
                ListViewItem selectedItem = _assignedNPCsListView.SelectedItems[0];
                _assignedNPCsListView.Items.Remove(selectedItem);
                _availableNPCsListView.Items.Add(selectedItem);
                
                // Update assigned workers count in group box title
                ((GroupBox)_assignedNPCsListView.Parent).Text = $"Assigned Workers ({_assignedNPCsListView.Items.Count}/{_building.WorkerSlots})";
            }
        }
        
        private void OkButton_Click(object sender, EventArgs e)
        {
            // Save assigned worker IDs
            AssignedWorkerIds.Clear();
            
            foreach (ListViewItem item in _assignedNPCsListView.Items)
            {
                AssignedWorkerIds.Add((string)item.Tag);
            }
        }
    }
} 