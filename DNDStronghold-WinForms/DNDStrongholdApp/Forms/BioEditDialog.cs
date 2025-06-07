using System;
using System.Windows.Forms;
using System.Drawing;
using DNDStrongholdApp.Models;
using DNDStrongholdApp.Services;

namespace DNDStrongholdApp.Forms
{
    public class BioEditDialog : Form
    {
        private TextBox _bioTextBox;
        private NPC _npc;
        public string BioText => _bioTextBox.Text;

        public BioEditDialog(string npcName, string currentBio = "", NPC npc = null)
        {
            _npc = npc;
            InitializeComponents(npcName, currentBio);
        }

        private void InitializeComponents(string npcName, string currentBio)
        {
            this.Text = $"Edit Bio - {npcName}";
            this.Size = new Size(500, 400);
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterParent;
            this.MinimumSize = new Size(400, 300);

            TableLayoutPanel layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Padding = new Padding(10)
            };
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Label
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F)); // TextBox
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Buttons

            Label promptLabel = new Label
            {
                Text = $"Enter biography for {npcName}:",
                Dock = DockStyle.Fill,
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 5)
            };

            _bioTextBox = new TextBox
            {
                Text = currentBio,
                Dock = DockStyle.Fill,
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                WordWrap = true,
                AcceptsReturn = true,
                AcceptsTab = true
            };

            FlowLayoutPanel buttonPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                AutoSize = true,
                Margin = new Padding(0, 5, 0, 0)
            };

            Button okButton = new Button
            {
                Text = "OK",
                DialogResult = DialogResult.OK,
                Width = 75,
                Height = 25,
                Margin = new Padding(5, 0, 0, 0)
            };

            Button cancelButton = new Button
            {
                Text = "Cancel",
                DialogResult = DialogResult.Cancel,
                Width = 75,
                Height = 25
            };

            Button generateButton = new Button
            {
                Text = "Generate Bio",
                Width = 100,
                Height = 25,
                Margin = new Padding(5, 0, 0, 0)
            };
            generateButton.Click += GenerateButton_Click;

            buttonPanel.Controls.Add(cancelButton);
            buttonPanel.Controls.Add(okButton);
            if (_npc != null) // Only show generate button if we have NPC data
                buttonPanel.Controls.Add(generateButton);

            layout.Controls.Add(promptLabel, 0, 0);
            layout.Controls.Add(_bioTextBox, 0, 1);
            layout.Controls.Add(buttonPanel, 0, 2);

            this.Controls.Add(layout);
            this.AcceptButton = okButton;
            this.CancelButton = cancelButton;
            
            // Focus on the text box when dialog opens
            this.Shown += (s, e) => _bioTextBox.Focus();
        }

        private void GenerateButton_Click(object sender, EventArgs e)
        {
            if (_npc == null) return;

            try
            {
                var bioGenerator = new BioGeneratorService();
                string generatedBio = bioGenerator.GenerateBio(_npc);
                
                // Ask user if they want to replace or append
                DialogResult result = MessageBox.Show(
                    "Do you want to replace the current bio or append to it?\n\nYes = Replace\nNo = Append\nCancel = Cancel",
                    "Generate Bio",
                    MessageBoxButtons.YesNoCancel,
                    MessageBoxIcon.Question);

                if (result == DialogResult.Yes)
                {
                    _bioTextBox.Text = generatedBio;
                }
                else if (result == DialogResult.No)
                {
                    if (!string.IsNullOrEmpty(_bioTextBox.Text))
                    {
                        _bioTextBox.Text += "\n\n" + generatedBio;
                    }
                    else
                    {
                        _bioTextBox.Text = generatedBio;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error generating bio: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
} 