using System;
using System.Collections.Generic;
using System.Windows.Forms;
using DNDStrongholdApp.Models;

namespace DNDStrongholdApp.Forms
{
    public class SelectNPCDialog : Form
    {
        public NPC? SelectedNPC { get; private set; }
        private ListBox _npcList;

        public SelectNPCDialog(List<NPC> npcs)
        {
            InitializeComponents(npcs);
        }

        private void InitializeComponents(List<NPC> npcs)
        {
            this.Text = "Select Worker";
            this.Width = 400;
            this.Height = 300;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.StartPosition = FormStartPosition.CenterParent;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            var label = new Label
            {
                Text = "Multiple workers found with this name.\nPlease select the specific worker:",
                AutoSize = true,
                Location = new System.Drawing.Point(10, 10)
            };

            _npcList = new ListBox
            {
                Location = new System.Drawing.Point(10, 50),
                Width = 360,
                Height = 160
            };

            // Add NPCs to the list with distinguishing information
            foreach (var npc in npcs)
            {
                _npcList.Items.Add($"{npc.Name} - Level {npc.Level} {npc.Type}");
                _npcList.Tag = npc;
            }

            var okButton = new Button
            {
                Text = "OK",
                DialogResult = DialogResult.OK,
                Location = new System.Drawing.Point(200, 220),
                Width = 80
            };
            okButton.Click += (s, e) =>
            {
                if (_npcList.SelectedIndex >= 0)
                {
                    SelectedNPC = npcs[_npcList.SelectedIndex];
                    this.DialogResult = DialogResult.OK;
                    this.Close();
                }
                else
                {
                    MessageBox.Show("Please select a worker.", "Selection Required",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            };

            var cancelButton = new Button
            {
                Text = "Cancel",
                DialogResult = DialogResult.Cancel,
                Location = new System.Drawing.Point(290, 220),
                Width = 80
            };

            this.Controls.AddRange(new Control[] { label, _npcList, okButton, cancelButton });
            this.AcceptButton = okButton;
            this.CancelButton = cancelButton;
        }
    }
} 