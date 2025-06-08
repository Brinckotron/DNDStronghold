using System;
using System.Collections.Generic;
using System.Linq;
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
        private GameStateService _gameStateService;
        private BioGeneratorService _bioGenerator;
        
        // View mode controls
        private RadioButton _regularViewRadio;
        private RadioButton _expandedViewRadio;
        private Panel _contentPanel;
        private bool _isExpandedView = false;
        
        // Expanded view controls
        private Panel _dmModePanel;
        private Dictionary<string, SectionControls> _sectionControls;
        private Dictionary<string, List<BioOption>> _sectionData;
        private TextBox _previewTextBox;

        // Bio data
        private Bio _currentBio;
        private bool _isUpdatingProgrammatically = false;

        public BioEditDialog(string npcName, Bio currentBio, NPC npc = null)
        {
            _npc = npc;
            _gameStateService = GameStateService.GetInstance();
            _bioGenerator = new BioGeneratorService();
            _currentBio = new Bio
            {
                Text = currentBio?.Text ?? "",
                IsCustom = currentBio?.IsCustom ?? false,
                SectionIndices = currentBio?.SectionIndices != null 
                    ? new Dictionary<string, int>(currentBio.SectionIndices)
                    : new Dictionary<string, int>()
            };
            
            InitializeComponents(npcName);
        }

        public Bio GetUpdatedBio()
        {
            return _currentBio;
        }

        private void InitializeComponents(string npcName)
        {
            this.Text = $"Edit Bio - {npcName}";
            this.Size = new Size(900, 700);
            this.MinimumSize = new Size(500, 400);
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterParent;

            // Main layout
            TableLayoutPanel mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                Padding = new Padding(10)
            };
            mainLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Header
            mainLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // View mode selection
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F)); // Content
            mainLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Buttons

            // Header
            Label headerLabel = new Label
            {
                Text = $"Bio Editor for {npcName}",
                Dock = DockStyle.Fill,
                AutoSize = true,
                Font = new Font(SystemFonts.DefaultFont, FontStyle.Bold),
                Margin = new Padding(0, 0, 0, 10)
            };

            // View mode selection panel
            Panel viewModePanel = new Panel
            {
                Height = 35,
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 0, 0, 10)
            };

            _regularViewRadio = new RadioButton
            {
                Text = "Regular Dialog",
                Location = new Point(10, 5),
                AutoSize = true,
                Checked = true
            };
            _regularViewRadio.CheckedChanged += ViewModeRadio_CheckedChanged;

            _expandedViewRadio = new RadioButton
            {
                Text = "Expanded Dialog (Section-by-Section)",
                Location = new Point(150, 5),
                AutoSize = true
            };
            _expandedViewRadio.CheckedChanged += ViewModeRadio_CheckedChanged;

            viewModePanel.Controls.Add(_regularViewRadio);
            viewModePanel.Controls.Add(_expandedViewRadio);

            // Content panel (will be populated based on view mode)
            _contentPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.FixedSingle
            };

            // Buttons
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

            buttonPanel.Controls.Add(cancelButton);
            buttonPanel.Controls.Add(okButton);

            mainLayout.Controls.Add(headerLabel, 0, 0);
            mainLayout.Controls.Add(viewModePanel, 0, 1);
            mainLayout.Controls.Add(_contentPanel, 0, 2);
            mainLayout.Controls.Add(buttonPanel, 0, 3);

            this.Controls.Add(mainLayout);
            this.AcceptButton = okButton;
            this.CancelButton = cancelButton;

            // Initialize with regular view
            CreateRegularView();
        }

        private void ViewModeRadio_CheckedChanged(object sender, EventArgs e)
        {
            if (sender == _regularViewRadio && _regularViewRadio.Checked)
            {
                if (_isExpandedView)
                {
                    SwitchToRegularView();
                }
            }
            else if (sender == _expandedViewRadio && _expandedViewRadio.Checked)
            {
                if (!_isExpandedView)
                {
                    SwitchToExpandedView();
                }
            }
        }

        private void SwitchToRegularView()
        {
            // Save current state from expanded view if applicable
            if (_isExpandedView && !_currentBio.IsCustom)
            {
                UpdateBioFromSections();
            }

            _isExpandedView = false;
            _contentPanel.Controls.Clear();
            CreateRegularView();
        }

        private void SwitchToExpandedView()
        {
            _isExpandedView = true;
            _contentPanel.Controls.Clear();
            
            if (_npc != null)
            {
                // Check if we should warn about custom content
                if (_currentBio.IsCustom && !string.IsNullOrWhiteSpace(_currentBio.Text))
                {
                    DialogResult result = MessageBox.Show(
                        "The current bio content is custom text that will be replaced with section-based content.\n\n" +
                        "Switching to Expanded view will replace it with section-based content.\n\n" +
                        "Do you want to continue?\n\n" +
                        "Yes = Switch to sections (custom text will be replaced)\n" +
                        "No = Stay in regular view",
                        "Custom Content Warning",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Warning);
                        
                    if (result == DialogResult.No)
                    {
                        // User chose to stay in regular view
                        _regularViewRadio.Checked = true;
                        _isExpandedView = false;
                        SwitchToRegularView();
                        return;
                    }
                    else
                    {
                        // User wants to proceed - generate new bio
                        _currentBio.GenerateFromService(_bioGenerator, _npc);
                    }
                }
                else if (!_currentBio.IsCustom && _currentBio.SectionIndices.Values.All(v => v == 0))
                {
                    // If it's generated content but no specific sections are set, generate new one
                    _currentBio.GenerateFromService(_bioGenerator, _npc);
                }

                LoadSectionData();
                CreateExpandedView();
            }
            else
            {
                // Fallback if no NPC data
                Label noDataLabel = new Label
                {
                    Text = "Expanded view requires NPC data to be available.",
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleCenter,
                    ForeColor = Color.Red
                };
                _contentPanel.Controls.Add(noDataLabel);
            }
        }

        private void CreateRegularView()
        {
            TableLayoutPanel layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Padding = new Padding(10)
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F)); // TextBox
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Generate button

            _bioTextBox = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                WordWrap = true,
                AcceptsReturn = true,
                AcceptsTab = true
            };

            // Set initial text programmatically
            _isUpdatingProgrammatically = true;
            _bioTextBox.Text = _currentBio.Text;
            _isUpdatingProgrammatically = false;

            // Add event handler to update bio when text changes (only user changes, not programmatic)
            _bioTextBox.TextChanged += (s, e) => 
            {
                if (!_isUpdatingProgrammatically)
                {
                    _currentBio.SetAsCustom(_bioTextBox.Text);
                }
            };

            FlowLayoutPanel generatePanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                AutoSize = true,
                Margin = new Padding(0, 5, 0, 0)
            };

            if (_npc != null)
            {
                Button generateButton = new Button
                {
                    Text = "Generate Bio",
                    Width = 100,
                    Height = 25
                };
                generateButton.Click += GenerateButton_Click;
                generatePanel.Controls.Add(generateButton);
            }

            layout.Controls.Add(_bioTextBox, 0, 0);
            layout.Controls.Add(generatePanel, 0, 1);

            _contentPanel.Controls.Add(layout);
            
            this.Shown += (s, e) => _bioTextBox?.Focus();
        }

        private void CreateExpandedView()
        {
            TableLayoutPanel layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Padding = new Padding(10)
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 70F)); // Sections
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 30F)); // Preview
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Randomize button

            // Sections panel
            _dmModePanel = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BorderStyle = BorderStyle.FixedSingle
            };
            CreateSectionControls();

            // Preview panel
            GroupBox previewGroup = new GroupBox
            {
                Text = "Bio Preview",
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 10, 0, 0)
            };

            _previewTextBox = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                BackColor = SystemColors.Control,
                Margin = new Padding(5)
            };
            previewGroup.Controls.Add(_previewTextBox);

            // Update preview initially and when sections change
            UpdatePreview();

            // Randomize button
            FlowLayoutPanel randomizePanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                AutoSize = true,
                Margin = new Padding(0, 5, 0, 0)
            };

            Button randomizeAllButton = new Button
            {
                Text = "Randomize All Sections",
                Width = 150,
                Height = 25
            };
            randomizeAllButton.Click += (s, e) => RandomizeAllSections();
            randomizePanel.Controls.Add(randomizeAllButton);

            layout.Controls.Add(_dmModePanel, 0, 0);
            layout.Controls.Add(previewGroup, 0, 1);
            layout.Controls.Add(randomizePanel, 0, 2);

            _contentPanel.Controls.Add(layout);
        }

        private void LoadSectionData()
        {
            // Get filtered section data from the bio generator
            _sectionData = _bioGenerator.GetFilteredSectionData(_npc);
        }

        private void CreateSectionControls()
        {
            _sectionControls = new Dictionary<string, SectionControls>();
            var sections = new[] { "Background", "Personality", "Appearance", "Motivation", "Quirk", "Secret" };
            
            int yPos = 10;
            foreach (var section in sections)
            {
                var sectionPanel = CreateSectionPanel(section, yPos);
                _dmModePanel.Controls.Add(sectionPanel);
                yPos += 150;
            }
        }

        private Panel CreateSectionPanel(string sectionName, int y)
        {
            var mainPanel = new Panel
            {
                Location = new Point(10, y),
                Size = new Size(840, 130),
                BorderStyle = BorderStyle.FixedSingle,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            // Section label
            var label = new Label
            {
                Text = sectionName,
                Location = new Point(5, 5),
                Font = new Font(SystemFonts.DefaultFont, FontStyle.Bold),
                AutoSize = true
            };

            // Text box for current selection
            var textBox = new TextBox
            {
                Location = new Point(5, 25),
                Size = new Size(550, 95),
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                ReadOnly = true,
                Anchor = AnchorStyles.Top | AnchorStyles.Left
            };

            // Previous button
            var prevButton = new Button
            {
                Text = "◀ Previous",
                Location = new Point(565, 25),
                Size = new Size(85, 35),
                BackColor = Color.LightBlue,
                Anchor = AnchorStyles.Top | AnchorStyles.Left
            };

            // Next button
            var nextButton = new Button
            {
                Text = "Next ▶",
                Location = new Point(565, 65),
                Size = new Size(85, 35),
                BackColor = Color.LightGreen,
                Anchor = AnchorStyles.Top | AnchorStyles.Left
            };

            // Random button
            var randomButton = new Button
            {
                Text = "🎲 Random",
                Location = new Point(660, 25),
                Size = new Size(85, 75),
                BackColor = Color.LightYellow,
                Anchor = AnchorStyles.Top | AnchorStyles.Left
            };

            // Event handlers
            prevButton.Click += (s, e) => NavigateSection(sectionName, -1);
            nextButton.Click += (s, e) => NavigateSection(sectionName, 1);
            randomButton.Click += (s, e) => RandomizeSection(sectionName);

            // Store controls for easy access
            _sectionControls[sectionName] = new SectionControls
            {
                Panel = mainPanel,
                TextBox = textBox,
                PrevButton = prevButton,
                NextButton = nextButton,
                RandomButton = randomButton
            };

            // Add controls to panel
            mainPanel.Controls.Add(label);
            mainPanel.Controls.Add(textBox);
            mainPanel.Controls.Add(prevButton);
            mainPanel.Controls.Add(nextButton);
            mainPanel.Controls.Add(randomButton);

            // Initialize with stored index
            UpdateSectionDisplay(sectionName);

            return mainPanel;
        }

        private void NavigateSection(string sectionName, int direction)
        {
            if (!_sectionData.ContainsKey(sectionName) || !_sectionData[sectionName].Any())
                return;

            var options = _sectionData[sectionName];
            int currentIndex = _currentBio.SectionIndices.ContainsKey(sectionName) ? _currentBio.SectionIndices[sectionName] : 0;
            int newIndex = (currentIndex + direction + options.Count) % options.Count;
            
            _currentBio.SectionIndices[sectionName] = newIndex;
            UpdateSectionDisplay(sectionName);
            UpdateBioFromSections();
            UpdatePreview();
        }

        private void RandomizeSection(string sectionName)
        {
            if (!_sectionData.ContainsKey(sectionName) || !_sectionData[sectionName].Any())
                return;

            var options = _sectionData[sectionName];
            _currentBio.SectionIndices[sectionName] = new Random().Next(options.Count);
            UpdateSectionDisplay(sectionName);
            UpdateBioFromSections();
            UpdatePreview();
        }

        private void RandomizeAllSections()
        {
            var random = new Random();
            foreach (var section in _sectionData.Keys)
            {
                if (_sectionData[section].Any())
                {
                    _currentBio.SectionIndices[section] = random.Next(_sectionData[section].Count);
                    UpdateSectionDisplay(section);
                }
            }
            UpdateBioFromSections();
            UpdatePreview();
        }

        private void UpdateSectionDisplay(string sectionName)
        {
            if (!_sectionControls.ContainsKey(sectionName) || !_sectionData.ContainsKey(sectionName))
                return;

            var controls = _sectionControls[sectionName];
            var options = _sectionData[sectionName];

            if (!options.Any())
            {
                controls.TextBox.Text = $"No {sectionName.ToLower()} options available for this NPC";
                controls.PrevButton.Enabled = false;
                controls.NextButton.Enabled = false;
                controls.RandomButton.Enabled = false;
                return;
            }

            int currentIndex = _currentBio.SectionIndices.ContainsKey(sectionName) ? _currentBio.SectionIndices[sectionName] : 0;
            if (currentIndex >= options.Count) currentIndex = 0;

            var currentOption = options[currentIndex];
            controls.TextBox.Text = ProcessPlaceholders(currentOption.Text, _npc);
            
            controls.PrevButton.Enabled = options.Count > 1;
            controls.NextButton.Enabled = options.Count > 1;
            controls.RandomButton.Enabled = options.Count > 1;
        }

        private void UpdateBioFromSections()
        {
            // Use the BioGeneratorService to generate text from the current indices
            // This ensures consistency with the bio generation logic
            string generatedText = _bioGenerator.GenerateBioFromIndices(_npc, _currentBio.SectionIndices);
            _currentBio.SetAsGenerated(generatedText, new Dictionary<string, int>(_currentBio.SectionIndices));
        }

        private void UpdatePreview()
        {
            if (_previewTextBox != null)
            {
                _previewTextBox.Text = _currentBio.Text;
            }
        }

        private string ProcessPlaceholders(string text, NPC npc)
        {
            string result = text;

            result = result.Replace("{name}", npc.Name);
            result = result.Replace("{Name}", npc.Name);

            if (npc.Gender == NPCGender.Male)
            {
                result = result.Replace("{he/she}", "he");
                result = result.Replace("{He/She}", "He");
                result = result.Replace("{his/her}", "his");
                result = result.Replace("{His/Her}", "His");
                result = result.Replace("{him/her}", "him");
                result = result.Replace("{Him/Her}", "Him");
                result = result.Replace("{himself/herself}", "himself");
                result = result.Replace("{Himself/Herself}", "Himself");
            }
            else
            {
                result = result.Replace("{he/she}", "she");
                result = result.Replace("{He/She}", "She");
                result = result.Replace("{his/her}", "her");
                result = result.Replace("{His/Her}", "Her");
                result = result.Replace("{him/her}", "her");
                result = result.Replace("{Him/Her}", "Her");
                result = result.Replace("{himself/herself}", "herself");
                result = result.Replace("{Himself/Herself}", "Herself");
            }

            return result;
        }

        private void GenerateButton_Click(object sender, EventArgs e)
        {
            if (_npc == null) return;

            try
            {
                DialogResult result = MessageBox.Show(
                    "Do you want to replace the current bio or append to it?\n\nYes = Replace\nNo = Append\nCancel = Cancel",
                    "Generate Bio",
                    MessageBoxButtons.YesNoCancel,
                    MessageBoxIcon.Question);

                if (result == DialogResult.Yes)
                {
                    _currentBio.GenerateFromService(_bioGenerator, _npc);
                    _isUpdatingProgrammatically = true;
                    _bioTextBox.Text = _currentBio.Text;
                    _isUpdatingProgrammatically = false;
                }
                else if (result == DialogResult.No)
                {
                    var newBio = new Bio();
                    newBio.GenerateFromService(_bioGenerator, _npc);
                    
                    string appendedText = !string.IsNullOrEmpty(_bioTextBox.Text) 
                        ? _bioTextBox.Text + "\n\n" + newBio.Text
                        : newBio.Text;
                    
                    _currentBio.SetAsCustom(appendedText);
                    _isUpdatingProgrammatically = true;
                    _bioTextBox.Text = _currentBio.Text;
                    _isUpdatingProgrammatically = false;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error generating bio: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private class SectionControls
        {
            public Panel Panel { get; set; }
            public TextBox TextBox { get; set; }
            public Button PrevButton { get; set; }
            public Button NextButton { get; set; }
            public Button RandomButton { get; set; }
        }
    }
} 