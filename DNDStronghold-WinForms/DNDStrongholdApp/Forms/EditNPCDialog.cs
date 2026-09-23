using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using DNDStrongholdApp.Models;
using DNDStrongholdApp.Services;

namespace DNDStrongholdApp.Forms
{
    // DM tool for editing an NPC who already exists in the stronghold. Deliberately does
    // not reuse AddNPCDialog: that dialog treats Level as a skill-point budget and forces
    // type-mandatory skills back to 1, which would quietly demote a trained veteran.
    // Here every field is edited directly and the caps are advisory.
    public class EditNPCDialog : Form
    {
        private readonly NPC _npc;

        private TextBox _nameBox = null!;
        private TextBox _titleBox = null!;
        private ComboBox _typeCombo = null!;
        private ComboBox _genderCombo = null!;
        private NumericUpDown _age = null!;
        private NumericUpDown _level = null!;

        private CheckBox _hero = null!;
        private CheckBox _spellcaster = null!;
        private CheckBox _magicAssist = null!;

        private CheckedListBox _states = null!;
        private ComboBox _hunger = null!;
        private NumericUpDown _starvation = null!;
        private ComboBox _rations = null!;

        private Label _poolLabel = null!;
        private NumericUpDown _healUsed = null!;
        private NumericUpDown _spellUsed = null!;

        private CheckedListBox _traits = null!;
        private Label _traitHint = null!;
        private Label _skillTotalLabel = null!;
        private readonly Dictionary<string, (NumericUpDown Level, NumericUpDown Xp)> _skills = new();

        public EditNPCDialog(NPC npc)
        {
            _npc = npc;

            Text = $"Edit NPC — {npc.Name}";
            Size = new Size(940, 700);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            BuildLayout();
            LoadFromNpc();
            RefreshMagicRow();
            RefreshSkillTotal();
        }

        private void BuildLayout()
        {
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 2,
                Padding = new Padding(12)
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 48F));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 52F));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var left = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                Margin = new Padding(0, 0, 8, 0)
            };
            left.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            left.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            left.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            left.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            left.Controls.Add(BuildIdentityGroup(), 0, 0);
            left.Controls.Add(BuildTagsGroup(), 0, 1);
            left.Controls.Add(BuildMagicGroup(), 0, 2);
            left.Controls.Add(BuildConditionGroup(), 0, 3);

            var right = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Margin = new Padding(8, 0, 0, 0)
            };
            right.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            right.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            right.Controls.Add(BuildSkillsGroup(), 0, 0);
            right.Controls.Add(BuildTraitsGroup(), 0, 1);

            var buttons = BuildButtonRow();
            root.Controls.Add(left, 0, 0);
            root.Controls.Add(right, 1, 0);
            root.Controls.Add(buttons, 0, 1);
            root.SetColumnSpan(buttons, 2);

            Controls.Add(root);
        }

        private GroupBox BuildIdentityGroup()
        {
            var grid = NewGrid(6);

            _nameBox = new TextBox { Dock = DockStyle.Fill };
            _titleBox = new TextBox { Dock = DockStyle.Fill };

            _typeCombo = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
            _typeCombo.Items.AddRange(Enum.GetNames(typeof(NPCType)));

            _genderCombo = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
            _genderCombo.Items.AddRange(Enum.GetNames(typeof(NPCGender)));

            _age = new NumericUpDown { Dock = DockStyle.Fill, Minimum = 1, Maximum = 200 };
            _level = new NumericUpDown { Dock = DockStyle.Fill, Minimum = 0, Maximum = 20 };

            AddRow(grid, 0, "Name:", _nameBox);
            AddRow(grid, 1, "Title:", _titleBox);
            AddRow(grid, 2, "Type:", _typeCombo);
            AddRow(grid, 3, "Gender:", _genderCombo);
            AddRow(grid, 4, "Age:", _age);
            AddRow(grid, 5, "Level:", _level);

            return Wrap("Identity", grid);
        }

        private GroupBox BuildTagsGroup()
        {
            var flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true
            };

            _hero = new CheckBox { Text = "Hero", AutoSize = true, Margin = new Padding(4, 6, 14, 6) };
            _spellcaster = new CheckBox { Text = "Spellcaster", AutoSize = true, Margin = new Padding(0, 6, 14, 6) };
            _magicAssist = new CheckBox { Text = "Magic Assist", AutoSize = true, Margin = new Padding(0, 6, 0, 6) };
            _spellcaster.CheckedChanged += (_, _) => RefreshMagicRow();

            flow.Controls.Add(_hero);
            flow.Controls.Add(_spellcaster);
            flow.Controls.Add(_magicAssist);

            var note = new Label
            {
                Text = "Hero survives raids more often and may act once per raid. Magic Assist requires Spellcaster.",
                AutoSize = false,
                Height = 18,
                Dock = DockStyle.Bottom,
                ForeColor = Color.DimGray
            };

            var host = new Panel { Dock = DockStyle.Fill, AutoSize = true, Height = 56 };
            host.Controls.Add(flow);
            host.Controls.Add(note);

            return Wrap("Tags", host);
        }

        private GroupBox BuildMagicGroup()
        {
            var grid = NewGrid(3);

            _poolLabel = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
            _healUsed = new NumericUpDown { Dock = DockStyle.Fill, Minimum = 0, Maximum = 20 };
            _spellUsed = new NumericUpDown { Dock = DockStyle.Fill, Minimum = 0, Maximum = 20 };

            AddRow(grid, 0, "Pools:", _poolLabel);
            AddRow(grid, 1, "Heal pts used:", _healUsed);
            AddRow(grid, 2, "Spell pts used:", _spellUsed);

            return Wrap("Magic (this week)", grid);
        }

        private GroupBox BuildConditionGroup()
        {
            // Deliberately no life/death toggle: death is permanent, and flipping the flag
            // here would skip whatever cleanup the death path performs.
            var grid = NewGrid(4);

            _states = new CheckedListBox
            {
                Dock = DockStyle.Fill,
                Height = 62,
                CheckOnClick = true,
                IntegralHeight = false
            };
            foreach (var name in Enum.GetNames(typeof(NPCStateType)))
                _states.Items.Add(name);

            _hunger = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
            _hunger.Items.AddRange(Enum.GetNames(typeof(HungerStatus)));

            _starvation = new NumericUpDown { Dock = DockStyle.Fill, Minimum = 0, Maximum = 10 };

            _rations = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
            _rations.Items.AddRange(Enum.GetNames(typeof(RationLevel)));

            AddRow(grid, 0, "Conditions:", _states);
            AddRow(grid, 1, "Hunger:", _hunger);
            AddRow(grid, 2, "Starvation:", _starvation);
            AddRow(grid, 3, "Rations:", _rations);

            return Wrap("Condition", grid);
        }

        private GroupBox BuildSkillsGroup()
        {
            var host = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2
            };
            host.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            host.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var scroller = new Panel { Dock = DockStyle.Fill, AutoScroll = true };

            var grid = new TableLayoutPanel
            {
                ColumnCount = 3,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Dock = DockStyle.Top,
                Padding = new Padding(4)
            };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));

            grid.Controls.Add(Header("Skill"), 0, 0);
            grid.Controls.Add(Header("Level"), 1, 0);
            grid.Controls.Add(Header("XP"), 2, 0);

            int row = 1;
            foreach (var skill in OrderedSkills())
            {
                grid.Controls.Add(new Label
                {
                    Text = skill.Name,
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleLeft,
                    ForeColor = skill.Type == SkillType.Advanced ? Color.DarkSlateBlue : SystemColors.ControlText
                }, 0, row);

                var levelBox = new NumericUpDown
                {
                    Dock = DockStyle.Fill,
                    Minimum = 0,
                    Maximum = 5,
                    Value = Math.Clamp(skill.Level, 0, 5),
                    Margin = new Padding(0, 2, 4, 2)
                };
                levelBox.ValueChanged += (_, _) => RefreshSkillTotal();

                var xpBox = new NumericUpDown
                {
                    Dock = DockStyle.Fill,
                    Minimum = 0,
                    Maximum = 10000,
                    Value = Math.Clamp(skill.Experience, 0, 10000),
                    Margin = new Padding(0, 2, 0, 2)
                };

                grid.Controls.Add(levelBox, 1, row);
                grid.Controls.Add(xpBox, 2, row);
                _skills[skill.Name] = (levelBox, xpBox);
                row++;
            }

            scroller.Controls.Add(grid);

            _skillTotalLabel = new Label
            {
                Dock = DockStyle.Fill,
                Height = 20,
                TextAlign = ContentAlignment.MiddleLeft
            };

            host.Controls.Add(scroller, 0, 0);
            host.Controls.Add(_skillTotalLabel, 0, 1);

            return Wrap("Skills", host);
        }

        private GroupBox BuildTraitsGroup()
        {
            var host = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2
            };
            host.RowStyles.Add(new RowStyle(SizeType.Absolute, 112));
            host.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            _traits = new CheckedListBox
            {
                Dock = DockStyle.Fill,
                CheckOnClick = true,
                MultiColumn = true,
                ColumnWidth = 150,
                IntegralHeight = false
            };
            foreach (var name in Enum.GetNames(typeof(NPCTraitType)))
                _traits.Items.Add(name);

            _traitHint = new Label
            {
                Dock = DockStyle.Fill,
                Height = 32,
                ForeColor = Color.DimGray,
                Text = "Select a trait to see what it does."
            };
            _traits.SelectedIndexChanged += (_, _) => RefreshTraitHint();

            host.Controls.Add(_traits, 0, 0);
            host.Controls.Add(_traitHint, 0, 1);

            return Wrap("Traits", host);
        }

        private Panel BuildButtonRow()
        {
            var panel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                Height = 46,
                Padding = new Padding(0, 8, 0, 0),
                WrapContents = false
            };

            var cancel = new Button
            {
                Text = "Cancel",
                Size = new Size(90, 30),
                Margin = new Padding(0, 0, 0, 0),
                DialogResult = DialogResult.Cancel
            };

            var save = new Button
            {
                Text = "Save",
                Size = new Size(100, 30),
                Margin = new Padding(0, 0, 10, 0)
            };
            save.Click += SaveClicked;

            panel.Controls.Add(cancel);
            panel.Controls.Add(save);
            AcceptButton = save;
            CancelButton = cancel;
            return panel;
        }

        private IEnumerable<Skill> OrderedSkills()
        {
            _npc.EnsureSkillsInitialized();
            return _npc.Skills
                .OrderBy(s => s.Type == SkillType.Advanced)
                .ThenBy(s => s.Name);
        }

        private void LoadFromNpc()
        {
            _nameBox.Text = _npc.Name;
            _titleBox.Text = _npc.Title;
            _typeCombo.SelectedItem = _npc.Type.ToString();
            _genderCombo.SelectedItem = _npc.Gender.ToString();
            _age.Value = Math.Clamp(_npc.Age, (int)_age.Minimum, (int)_age.Maximum);
            _level.Value = Math.Clamp(_npc.Level, (int)_level.Minimum, (int)_level.Maximum);

            _hero.Checked = _npc.Hero;
            _spellcaster.Checked = _npc.Spellcaster;
            _magicAssist.Checked = _npc.MagicAssist;

            for (int i = 0; i < _states.Items.Count; i++)
            {
                string name = _states.Items[i].ToString() ?? string.Empty;
                _states.SetItemChecked(i,
                    _npc.States != null && _npc.States.Any(s => s.Type.ToString() == name));
            }

            _hunger.SelectedItem = _npc.HungerState.ToString();
            _starvation.Value = Math.Clamp(_npc.StarvationProgress, 0, (int)_starvation.Maximum);
            _rations.SelectedItem = _npc.RationLevel.ToString();

            _healUsed.Value = Math.Clamp(_npc.HealPointsUsed, 0, (int)_healUsed.Maximum);
            _spellUsed.Value = Math.Clamp(_npc.SpellPointsUsed, 0, (int)_spellUsed.Maximum);

            for (int i = 0; i < _traits.Items.Count; i++)
            {
                string name = _traits.Items[i].ToString() ?? string.Empty;
                _traits.SetItemChecked(i,
                    _npc.Traits != null && _npc.Traits.Any(t => t.Type.ToString() == name));
            }
        }

        private void RefreshMagicRow()
        {
            bool isCaster = _spellcaster.Checked;
            _magicAssist.Enabled = isCaster;
            if (!isCaster) _magicAssist.Checked = false;

            _healUsed.Enabled = isCaster;
            _spellUsed.Enabled = isCaster;

            int faith = SkillValue("Faith");
            int arcana = SkillValue("Arcana");
            _poolLabel.Text = isCaster
                ? $"Faith {faith} heal pts, Arcana {arcana} spell pts"
                : "Not a spellcaster";
        }

        private List<NPCTraitType> CheckedTraits()
        {
            var result = new List<NPCTraitType>();
            for (int i = 0; i < _traits.Items.Count; i++)
            {
                if (!_traits.GetItemChecked(i)) continue;
                if (Enum.TryParse<NPCTraitType>(_traits.Items[i].ToString(), out var trait))
                    result.Add(trait);
            }
            return result;
        }

        private void RefreshTraitHint()
        {
            if (_traits.SelectedItem == null ||
                !Enum.TryParse<NPCTraitType>(_traits.SelectedItem.ToString(), out var trait))
            {
                _traitHint.Text = "Select a trait to see what it does.";
                return;
            }

            string text = new NPCTrait { Type = trait }.GetDescription();
            var opposite = TraitService.Opposite(trait);
            if (opposite != null)
                text += $"  (Cannot be combined with {opposite.Value}.)";
            _traitHint.Text = text;
        }

        private int SkillValue(string name)
        {
            return _skills.TryGetValue(name, out var pair) ? (int)pair.Level.Value : 0;
        }

        private void RefreshSkillTotal()
        {
            int total = _skills.Values.Sum(p => (int)p.Level.Value);
            _skillTotalLabel.Text = $"Total skill levels: {total} / 10 soft cap";
            _skillTotalLabel.ForeColor = total > 10 ? Color.DarkRed : SystemColors.ControlText;
            RefreshMagicRow();
        }

        private void SaveClicked(object? sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_nameBox.Text))
            {
                MessageBox.Show("The NPC needs a name.", "Edit NPC",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var checkedTraits = CheckedTraits();
            var conflict = TraitService.FindConflict(checkedTraits);
            if (conflict != null)
            {
                MessageBox.Show(
                    $"{conflict.Value.First} and {conflict.Value.Second} contradict each other. Pick one.",
                    "Edit NPC", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            int total = _skills.Values.Sum(p => (int)p.Level.Value);
            if (total > 10)
            {
                var answer = MessageBox.Show(
                    $"Total skill levels come to {total}. Past 10 the NPC can no longer earn skill XP through play. Save anyway?",
                    "Edit NPC", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (answer != DialogResult.Yes) return;
            }

            _npc.Name = _nameBox.Text.Trim();
            _npc.Title = _titleBox.Text.Trim();
            if (_typeCombo.SelectedItem != null)
                _npc.Type = Enum.Parse<NPCType>(_typeCombo.SelectedItem.ToString()!);
            if (_genderCombo.SelectedItem != null)
                _npc.Gender = Enum.Parse<NPCGender>(_genderCombo.SelectedItem.ToString()!);
            _npc.Age = (int)_age.Value;
            _npc.Level = (int)_level.Value;

            _npc.Hero = _hero.Checked;
            _npc.Spellcaster = _spellcaster.Checked;
            _npc.MagicAssist = _spellcaster.Checked && _magicAssist.Checked;

            // Skills
            foreach (var pair in _skills)
            {
                var skill = _npc.Skills.Find(s => s.Name == pair.Key);
                if (skill == null) continue;
                skill.Level = (int)pair.Value.Level.Value;
                skill.Experience = (int)pair.Value.Xp.Value;
                if (skill.Type == SkillType.Advanced && skill.Level > 0)
                    skill.IsLearned = true;
            }

            // Health conditions, rebuilt from the checklist while keeping existing start dates
            var wanted = new List<NPCStateType>();
            for (int i = 0; i < _states.Items.Count; i++)
            {
                if (!_states.GetItemChecked(i)) continue;
                if (Enum.TryParse<NPCStateType>(_states.Items[i].ToString(), out var state))
                    wanted.Add(state);
            }
            _npc.States.RemoveAll(s => !wanted.Contains(s.Type));
            foreach (var state in wanted)
            {
                if (!_npc.States.Any(s => s.Type == state))
                    _npc.States.Add(new NPCState { Type = state });
            }

            if (_hunger.SelectedItem != null)
                _npc.HungerState = Enum.Parse<HungerStatus>(_hunger.SelectedItem.ToString()!);
            _npc.StarvationProgress = (int)_starvation.Value;
            if (_rations.SelectedItem != null)
                _npc.RationLevel = Enum.Parse<RationLevel>(_rations.SelectedItem.ToString()!);

            _npc.HealPointsUsed = (int)_healUsed.Value;
            _npc.SpellPointsUsed = (int)_spellUsed.Value;

            // Traits
            var wantedTraits = checkedTraits;
            _npc.Traits.RemoveAll(t => !wantedTraits.Contains(t.Type));
            foreach (var trait in wantedTraits)
            {
                if (!_npc.Traits.Any(t => t.Type == trait))
                    _npc.Traits.Add(new NPCTrait { Type = trait });
            }

            // Upkeep reads skills and traits; status reads health conditions.
            _npc.UpdateUpkeepCosts();
            _npc.UpdateStatus();

            DialogResult = DialogResult.OK;
            Close();
        }

        // ---- small layout helpers ----

        private static Label Header(string text) => new Label
        {
            Text = text,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font(SystemFonts.DefaultFont, FontStyle.Bold)
        };

        private static TableLayoutPanel NewGrid(int rows)
        {
            var grid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = rows,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(4)
            };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 105));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            for (int i = 0; i < rows; i++)
                grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            return grid;
        }

        private static void AddRow(TableLayoutPanel grid, int row, string label, Control control)
        {
            grid.Controls.Add(new Label
            {
                Text = label,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleRight,
                Margin = new Padding(0, 0, 6, 0)
            }, 0, row);
            grid.Controls.Add(control, 1, row);
        }

        private static GroupBox Wrap(string title, Control content)
        {
            var box = new GroupBox
            {
                Text = title,
                Dock = DockStyle.Fill,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(6),
                Margin = new Padding(0, 0, 0, 8)
            };
            box.Controls.Add(content);
            return box;
        }
    }
}
