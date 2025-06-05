using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using DNDStrongholdApp.Models;
using System.Linq;

namespace DNDStrongholdApp.Forms
{
    public class ConstructionCrewAssignmentDialog : Form
    {
        private List<NPC> _availableNPCs;
        private ListView _availableNPCsListView;
        private ListView _assignedCrewListView;
        private Building _building;
        
        public List<string> AssignedCrewIds { get; private set; } = new List<string>();
        
        public ConstructionCrewAssignmentDialog(List<NPC> allNPCs, Building building, string operationType)
        {
            _building = building;
            
            // Filter NPCs to include unassigned NPCs AND currently assigned construction crew members for this building
            _availableNPCs = allNPCs.FindAll(n => 
                n.Assignment.Type == AssignmentType.Unassigned || 
                (n.Assignment.Type == AssignmentType.Building && 
                 n.Assignment.TargetId == building.Id && 
                 building.DedicatedConstructionCrew.Contains(n.Id)));
            
            // Sort by Construction skill level (descending), then by NPC type (Laborers first)
            _availableNPCs.Sort((a, b) =>
            {
                // First priority: Laborers
                if (a.Type == NPCType.Laborer && b.Type != NPCType.Laborer) return -1;
                if (b.Type == NPCType.Laborer && a.Type != NPCType.Laborer) return 1;
                
                // Second priority: Construction skill level
                var aSkill = a.Skills.Find(s => s.Name == "Construction")?.Level ?? 0;
                var bSkill = b.Skills.Find(s => s.Name == "Construction")?.Level ?? 0;
                int skillComparison = bSkill.CompareTo(aSkill); // Descending
                if (skillComparison != 0) return skillComparison;
                
                // Third priority: Labor skill level
                var aLabor = a.Skills.Find(s => s.Name == "Labor")?.Level ?? 0;
                var bLabor = b.Skills.Find(s => s.Name == "Labor")?.Level ?? 0;
                return bLabor.CompareTo(aLabor); // Descending
            });
            
            InitializeComponent(operationType);
            LoadNPCs();
        }
        
        private void InitializeComponent(string operationType)
        {
            this.Text = $"Assign Dedicated Construction Crew - {operationType}";
            this.Size = new Size(500, 600);
            this.StartPosition = FormStartPosition.CenterParent;
            this.MinimizeBox = false;
            this.MaximizeBox = false;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.ShowInTaskbar = false;
            
            // Main layout
            TableLayoutPanel mainLayout = new TableLayoutPanel();
            mainLayout.Dock = DockStyle.Fill;
            mainLayout.ColumnCount = 1;
            mainLayout.RowCount = 4;
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60F)); // Info panel
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 80F)); // Available workers
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 50F)); // Assigned crew
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50F)); // Buttons
            
            // Info panel
            Panel infoPanel = new Panel();
            infoPanel.Dock = DockStyle.Fill;
            infoPanel.BackColor = Color.LightYellow;
            infoPanel.BorderStyle = BorderStyle.FixedSingle;
            
            Label infoLabel = new Label();
            infoLabel.Text = "Assign up to 3 workers to a dedicated construction crew. They work on top of the normal worker slots limit and focus solely on construction.";
            infoLabel.Dock = DockStyle.Fill;
            infoLabel.TextAlign = ContentAlignment.MiddleLeft;
            infoLabel.Padding = new Padding(0);
            infoPanel.Controls.Add(infoLabel);
            
            // Available workers group
            GroupBox availableGroup = new GroupBox();
            availableGroup.Text = "Available Workers";
            availableGroup.Dock = DockStyle.Fill;
            
            _availableNPCsListView = new ListView();
            _availableNPCsListView.Dock = DockStyle.Fill;
            _availableNPCsListView.View = View.Details;
            _availableNPCsListView.FullRowSelect = true;
            _availableNPCsListView.MultiSelect = false;
            _availableNPCsListView.DoubleClick += AvailableWorker_DoubleClick;
            
            // Add columns
            _availableNPCsListView.Columns.Add("Name", 150);
            _availableNPCsListView.Columns.Add("Type", 100);
            _availableNPCsListView.Columns.Add("Construction Skill", 100);
            
            availableGroup.Controls.Add(_availableNPCsListView);
            
            // Construction crew group
            GroupBox crewGroup = new GroupBox();
            crewGroup.Text = "Construction Crew (0/3)";
            crewGroup.Dock = DockStyle.Fill;
            
            _assignedCrewListView = new ListView();
            _assignedCrewListView.Dock = DockStyle.Fill;
            _assignedCrewListView.View = View.Details;
            _assignedCrewListView.FullRowSelect = true;
            _assignedCrewListView.MultiSelect = false;
            _assignedCrewListView.DoubleClick += AssignedWorker_DoubleClick;
            
            // Add columns
            _assignedCrewListView.Columns.Add("Name", 180);
            _assignedCrewListView.Columns.Add("Type", 100);
            _assignedCrewListView.Columns.Add("Construction Skill", 100);
            
            crewGroup.Controls.Add(_assignedCrewListView);
            
            // Button panel
            FlowLayoutPanel buttonPanel = new FlowLayoutPanel();
            buttonPanel.Dock = DockStyle.Fill;
            buttonPanel.FlowDirection = FlowDirection.RightToLeft;
            buttonPanel.Padding = new Padding(10);
            
            Button cancelButton = new Button();
            cancelButton.Text = "Cancel";
            cancelButton.DialogResult = DialogResult.Cancel;
            cancelButton.Width = 75;
            
            Button okButton = new Button();
            okButton.Text = "OK";
            okButton.DialogResult = DialogResult.OK;
            okButton.Width = 75;
            okButton.Click += OkButton_Click;
            
            Button skipButton = new Button();
            skipButton.Text = "No Crew";
            skipButton.DialogResult = DialogResult.OK;
            skipButton.Width = 150;
            skipButton.Click += SkipButton_Click;
            
            buttonPanel.Controls.Add(cancelButton);
            buttonPanel.Controls.Add(okButton);
            buttonPanel.Controls.Add(skipButton);
            
            // Add controls to main layout
            mainLayout.Controls.Add(infoPanel, 0, 0);
            mainLayout.Controls.Add(availableGroup, 0, 1);
            mainLayout.Controls.Add(crewGroup, 0, 2);
            mainLayout.Controls.Add(buttonPanel, 0, 3);
            
            this.Controls.Add(mainLayout);
            this.AcceptButton = okButton;
            this.CancelButton = cancelButton;
        }
        
        private void LoadNPCs()
        {
            _availableNPCsListView.Items.Clear();
            _assignedCrewListView.Items.Clear();
            
            foreach (var npc in _availableNPCs)
            {
                ListViewItem item = new ListViewItem(npc.Name);
                item.SubItems.Add(npc.Type.ToString());
                
                var constructionSkill = npc.Skills.Find(s => s.Name == "Construction");
                item.SubItems.Add((constructionSkill?.Level ?? 0).ToString());
                
                item.Tag = npc.Id;
                
                // Highlight Laborers
                if (npc.Type == NPCType.Laborer)
                {
                    item.BackColor = Color.LightBlue;
                }
                
                // Check if this NPC is currently assigned to the construction crew
                if (_building.DedicatedConstructionCrew.Contains(npc.Id))
                {
                    _assignedCrewListView.Items.Add(item);
                }
                else
                {
                    _availableNPCsListView.Items.Add(item);
                }
            }
            
            UpdateCrewGroupText();
        }
        
        private void AvailableWorker_DoubleClick(object sender, EventArgs e)
        {
            if (_availableNPCsListView.SelectedItems.Count > 0 && _assignedCrewListView.Items.Count < 3)
            {
                AssignWorkerToCrew();
            }
        }
        
        private void AssignedWorker_DoubleClick(object sender, EventArgs e)
        {
            if (_assignedCrewListView.SelectedItems.Count > 0)
            {
                UnassignWorkerFromCrew();
            }
        }
        
        private void AssignWorkerToCrew()
        {
            if (_assignedCrewListView.Items.Count >= 3)
            {
                MessageBox.Show("Maximum of 3 workers can be assigned to the construction crew.", 
                    "Maximum Crew Size Reached", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            
            if (_availableNPCsListView.SelectedItems.Count == 0) return;
            
            ListViewItem selectedItem = _availableNPCsListView.SelectedItems[0];
            string workerId = (string)selectedItem.Tag;
            var npc = _availableNPCs.Find(n => n.Id == workerId);
            
            if (npc != null)
            {
                // Create new item for crew list
                ListViewItem crewItem = new ListViewItem(npc.Name);
                crewItem.SubItems.Add(npc.Type.ToString());
                
                var constructionSkill = npc.Skills.Find(s => s.Name == "Construction");
                int constructionLevel = constructionSkill?.Level ?? 0;
                crewItem.SubItems.Add(constructionLevel.ToString());
                
                crewItem.Tag = workerId;
                
                if (npc.Type == NPCType.Laborer)
                {
                    crewItem.BackColor = Color.LightBlue;
                }
                
                _assignedCrewListView.Items.Add(crewItem);
                _availableNPCsListView.Items.Remove(selectedItem);
                
                UpdateCrewGroupText();
            }
        }
        
        private void UnassignWorkerFromCrew()
        {
            if (_assignedCrewListView.SelectedItems.Count == 0) return;
            
            ListViewItem selectedItem = _assignedCrewListView.SelectedItems[0];
            string workerId = (string)selectedItem.Tag;
            var npc = _availableNPCs.Find(n => n.Id == workerId);
            
            if (npc != null)
            {
                // Create new item for available list (re-insert in sorted position)
                ListViewItem availableItem = new ListViewItem(npc.Name);
                availableItem.SubItems.Add(npc.Type.ToString());
                
                var constructionSkill = npc.Skills.Find(s => s.Name == "Construction");
                availableItem.SubItems.Add((constructionSkill?.Level ?? 0).ToString());
                
                availableItem.Tag = workerId;
                
                if (npc.Type == NPCType.Laborer)
                {
                    availableItem.BackColor = Color.LightBlue;
                }
                
                // Find correct insertion point to maintain sort order
                int insertIndex = 0;
                for (int i = 0; i < _availableNPCsListView.Items.Count; i++)
                {
                    string existingId = (string)_availableNPCsListView.Items[i].Tag;
                    var existingNpc = _availableNPCs.Find(n => n.Id == existingId);
                    if (existingNpc != null && ShouldInsertBefore(npc, existingNpc))
                    {
                        insertIndex = i;
                        break;
                    }
                    insertIndex = i + 1;
                }
                
                _availableNPCsListView.Items.Insert(insertIndex, availableItem);
                _assignedCrewListView.Items.Remove(selectedItem);
                
                UpdateCrewGroupText();
            }
        }
        
        private bool ShouldInsertBefore(NPC npcToInsert, NPC existingNpc)
        {
            // Same sorting logic as in constructor
            if (npcToInsert.Type == NPCType.Laborer && existingNpc.Type != NPCType.Laborer) return true;
            if (existingNpc.Type == NPCType.Laborer && npcToInsert.Type != NPCType.Laborer) return false;
            
            var insertSkill = npcToInsert.Skills.Find(s => s.Name == "Construction")?.Level ?? 0;
            var existingSkill = existingNpc.Skills.Find(s => s.Name == "Construction")?.Level ?? 0;
            if (insertSkill > existingSkill) return true;
            if (insertSkill < existingSkill) return false;
            
            var insertLabor = npcToInsert.Skills.Find(s => s.Name == "Labor")?.Level ?? 0;
            var existingLabor = existingNpc.Skills.Find(s => s.Name == "Labor")?.Level ?? 0;
            return insertLabor > existingLabor;
        }
        
        private void UpdateCrewGroupText()
        {
            ((GroupBox)_assignedCrewListView.Parent).Text = $"Construction Crew ({_assignedCrewListView.Items.Count}/3)";
        }
        
        private void OkButton_Click(object sender, EventArgs e)
        {
            // Save assigned crew member IDs
            AssignedCrewIds.Clear();
            foreach (ListViewItem item in _assignedCrewListView.Items)
            {
                AssignedCrewIds.Add((string)item.Tag);
            }
        }
        
        private void SkipButton_Click(object sender, EventArgs e)
        {
            // Don't assign any crew members
            AssignedCrewIds.Clear();
        }
    }
} 