using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using DNDStrongholdApp.Models;
using DNDStrongholdApp.Services;

namespace DNDStrongholdApp.Forms
{
    public class FactionEditor : Form
    {
        private readonly GameStateService _gameState;
        private readonly List<EnemyFaction> _factions;
        private EnemyFaction? _current;
        private ComboBox _factionCombo = null!;
        private TextBox _nameBox = null!;
        private ComboBox _frequencyCombo = null!;
        private NumericUpDown _customFrequency = null!;
        private NumericUpDown _outpostMultiplier = null!;
        private CheckBox _hasOutpost = null!;
        private TextBox _outpostName = null!;
        private Label _intelLabel = null!;
        private NumericUpDown _power = null!;
        private Label _powerHint = null!;
        private readonly Dictionary<TroopType, CheckBox> _troopChecks = new();
        private readonly Dictionary<TroopType, TextBox> _troopNames = new();
        private ListBox _goals = null!;
        private bool _loading;

        public FactionEditor(GameStateService gameState)
        {
            _gameState = gameState;
            var stronghold = gameState.GetCurrentStronghold();
            stronghold.EnsureCombatDefaults();
            _factions = stronghold.EnemyFactions.Select(Clone).ToList();
            InitializeUi();
            LoadCombo();
        }

        private void InitializeUi()
        {
            Text = "Enemy Factions";
            Size = new Size(920, 600);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            var top = new Panel { Dock = DockStyle.Top, Height = 44 };
            top.Controls.Add(new Label { Text = "Faction:", Location = new Point(12, 14), AutoSize = true });
            _factionCombo = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 280,
                Location = new Point(70, 10)
            };
            _factionCombo.SelectedIndexChanged += (_, _) => LoadSelected();
            var addBtn = new Button { Text = "Add", Location = new Point(360, 8), Size = new Size(80, 28) };
            var deleteBtn = new Button { Text = "Delete", Location = new Point(446, 8), Size = new Size(80, 28) };
            addBtn.Click += AddFaction;
            deleteBtn.Click += DeleteFaction;
            top.Controls.Add(_factionCombo);
            top.Controls.Add(addBtn);
            top.Controls.Add(deleteBtn);

            var body = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12, 8, 12, 8) };
            int y = 8;
            _nameBox = AddLabeledText(body, "Name", y, out y);

            body.Controls.Add(new Label { Text = "Raid frequency:", Location = new Point(12, y + 4), AutoSize = true });
            _frequencyCombo = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(180, y),
                Width = 110
            };
            foreach (RaidFrequencyTier tier in Enum.GetValues<RaidFrequencyTier>())
                _frequencyCombo.Items.Add(RaidFrequency.Label(tier));
            _frequencyCombo.SelectedIndexChanged += (_, _) => SyncFrequencyPercentBox();
            body.Controls.Add(_frequencyCombo);
            _customFrequency = new NumericUpDown
            {
                Location = new Point(296, y),
                Width = 50,
                Minimum = 0,
                Maximum = 100,
                Enabled = false
            };
            body.Controls.Add(_customFrequency);
            y += 32;

            _outpostMultiplier = AddLabeledDecimal(body, "Outpost multiplier", y, 1, 10, out y);
            _hasOutpost = new CheckBox { Text = "Has an outpost (max one)", Location = new Point(12, y), AutoSize = true };
            y += 28;
            body.Controls.Add(_hasOutpost);
            _outpostName = AddLabeledText(body, "Outpost name", y, out y);
            _intelLabel = new Label { Location = new Point(180, y + 4), AutoSize = true, ForeColor = Color.DimGray };
            body.Controls.Add(new Label { Text = "Intel / captives:", Location = new Point(12, y + 4), AutoSize = true });
            body.Controls.Add(_intelLabel);
            y += 22;
            _hasOutpost.CheckedChanged += (_, _) => _outpostName.Enabled = _hasOutpost.Checked;

            body.Controls.Add(new Label { Text = "Faction Power:", Location = new Point(12, y + 4), AutoSize = true });
            _power = new NumericUpDown { Location = new Point(180, y), Width = 60, Minimum = 0, Maximum = 5 };
            _powerHint = new Label { Location = new Point(250, y + 4), AutoSize = true };
            _power.ValueChanged += (_, _) => _powerHint.Text = FactionPower.Label((int)_power.Value);
            body.Controls.Add(_power);
            body.Controls.Add(_powerHint);
            y += 32;

            body.Controls.Add(new Label
            {
                Text = "Goal priority (auto-raid 50 / 25 / 15 / 10)",
                Location = new Point(12, y),
                AutoSize = true
            });
            y += 22;
            _goals = new ListBox { Location = new Point(12, y), Size = new Size(260, 110) };
            var upBtn = new Button { Text = "Up", Location = new Point(280, y), Size = new Size(70, 28) };
            var downBtn = new Button { Text = "Down", Location = new Point(280, y + 34), Size = new Size(70, 28) };
            upBtn.Click += (_, _) => MoveGoal(-1);
            downBtn.Click += (_, _) => MoveGoal(1);
            body.Controls.Add(_goals);
            body.Controls.Add(upBtn);
            body.Controls.Add(downBtn);

            body.Controls.Add(new Label
            {
                Text = "Available troops — check to field, optional display name",
                Location = new Point(380, 8),
                AutoSize = true,
                Font = new Font(Font, FontStyle.Bold)
            });
            var troopPanel = new Panel
            {
                Location = new Point(380, 32),
                Size = new Size(500, 430),
                AutoScroll = true,
                BorderStyle = BorderStyle.FixedSingle
            };
            int rowY = 6;
            foreach (var def in TroopCatalog.All)
            {
                var check = new CheckBox
                {
                    Location = new Point(8, rowY + 2),
                    Size = new Size(18, 18)
                };
                var nameBox = new TextBox
                {
                    Location = new Point(30, rowY),
                    Width = 160,
                    PlaceholderText = def.Name
                };
                var stats = new Label
                {
                    Text = $"{def.Name}  ({def.Strength}/{def.HitPoints}/{def.Stealth}{TroopCatalog.TagSuffix(def)})",
                    Location = new Point(196, rowY + 3),
                    AutoSize = true
                };
                troopPanel.Controls.Add(check);
                troopPanel.Controls.Add(nameBox);
                troopPanel.Controls.Add(stats);
                _troopChecks[def.Type] = check;
                _troopNames[def.Type] = nameBox;
                rowY += 28;
            }
            body.Controls.Add(troopPanel);

            var bottom = new Panel { Dock = DockStyle.Bottom, Height = 48 };
            var saveBtn = new Button { Text = "Save", Location = new Point(710, 10), Size = new Size(90, 28), DialogResult = DialogResult.OK };
            var cancelBtn = new Button { Text = "Cancel", Location = new Point(810, 10), Size = new Size(80, 28), DialogResult = DialogResult.Cancel };
            saveBtn.Click += (_, _) => SaveToStronghold();
            bottom.Controls.Add(saveBtn);
            bottom.Controls.Add(cancelBtn);
            AcceptButton = saveBtn;
            CancelButton = cancelBtn;

            Controls.Add(body);
            Controls.Add(bottom);
            Controls.Add(top);
        }

        private TextBox AddLabeledText(Panel parent, string label, int y, out int nextY)
        {
            parent.Controls.Add(new Label { Text = label + ":", Location = new Point(12, y + 4), AutoSize = true });
            var box = new TextBox { Location = new Point(180, y), Width = 180 };
            parent.Controls.Add(box);
            nextY = y + 32;
            return box;
        }

        private NumericUpDown AddLabeledDecimal(Panel parent, string label, int y, decimal min, decimal max, out int nextY)
        {
            parent.Controls.Add(new Label { Text = label + ":", Location = new Point(12, y + 4), AutoSize = true });
            var box = new NumericUpDown
            {
                Location = new Point(180, y),
                Width = 80,
                Minimum = min,
                Maximum = max,
                DecimalPlaces = 1,
                Increment = 0.5M,
                Value = 2
            };
            parent.Controls.Add(box);
            nextY = y + 32;
            return box;
        }

        private RaidFrequencyTier SelectedFrequency()
        {
            int index = _frequencyCombo.SelectedIndex;
            if (index < 0) return RaidFrequencyTier.Occasional;
            return (RaidFrequencyTier)index;
        }

        private void SyncFrequencyPercentBox()
        {
            var tier = SelectedFrequency();
            bool custom = tier == RaidFrequencyTier.Custom;
            int shown = RaidFrequency.BasePercent(tier, (int)_customFrequency.Value);
            _customFrequency.Value = Math.Clamp(shown, 0, 100);
            _customFrequency.Enabled = custom;
        }

        private void LoadCombo()
        {
            _factionCombo.Items.Clear();
            foreach (var faction in _factions)
                _factionCombo.Items.Add(faction.Name);
            if (_factions.Count > 0)
                _factionCombo.SelectedIndex = 0;
        }

        private void LoadSelected()
        {
            CommitCurrent();
            if (_factionCombo.SelectedIndex < 0 || _factionCombo.SelectedIndex >= _factions.Count)
            {
                _current = null;
                return;
            }

            _loading = true;
            _current = _factions[_factionCombo.SelectedIndex];
            _current.Normalize();
            _nameBox.Text = _current.Name;
            _frequencyCombo.SelectedIndex = (int)_current.Frequency;
            if (_current.Frequency == RaidFrequencyTier.Custom)
                _customFrequency.Value = Math.Clamp(_current.CustomFrequencyPercent, 0, 100);
            SyncFrequencyPercentBox();
            decimal multiplier = (decimal)_current.OutpostFrequencyMultiplier;
            if (multiplier < _outpostMultiplier.Minimum) multiplier = _outpostMultiplier.Minimum;
            if (multiplier > _outpostMultiplier.Maximum) multiplier = _outpostMultiplier.Maximum;
            _outpostMultiplier.Value = multiplier;
            _hasOutpost.Checked = _current.HasOutpost;
            _outpostName.Text = _current.Outpost?.Name ?? "";
            _outpostName.Enabled = _hasOutpost.Checked;
            int intel = _current.Outpost?.IntelPoints ?? 0;
            int held = _current.CapturedNpcs?.Count ?? 0;
            _intelLabel.Text = _hasOutpost.Checked
                ? $"Intel {intel} stealth, {held} captive(s) held"
                : held > 0 ? $"{held} captive(s) held (no outpost)" : "";
            _power.Value = Math.Clamp(_current.Power, 0, 5);
            _powerHint.Text = FactionPower.Label(_current.Power);
            foreach (var def in TroopCatalog.All)
            {
                _troopChecks[def.Type].Checked = _current.AvailableTroops.Contains(def.Type);
                _troopNames[def.Type].Text = _current.TroopName(def.Type) == def.Name
                    ? ""
                    : _current.TroopName(def.Type);
            }
            RefreshGoalList(_current.GoalPriority);
            _loading = false;
        }

        private void RefreshGoalList(IReadOnlyList<OperationalGoal> goals)
        {
            int selected = _goals.SelectedIndex;
            _goals.Items.Clear();
            for (int i = 0; i < goals.Count; i++)
            {
                int weight = i < EnemyFaction.GoalWeights.Length ? EnemyFaction.GoalWeights[i] : 0;
                _goals.Items.Add($"{goals[i]} — {weight}%");
            }
            if (selected >= 0 && selected < _goals.Items.Count)
                _goals.SelectedIndex = selected;
        }

        private List<OperationalGoal> GoalsFromList()
        {
            var goals = new List<OperationalGoal>();
            foreach (var item in _goals.Items)
            {
                string text = item.ToString() ?? "";
                int sep = text.IndexOf(" — ", StringComparison.Ordinal);
                string name = sep >= 0 ? text[..sep] : text;
                if (Enum.TryParse(name, out OperationalGoal goal))
                    goals.Add(goal);
            }
            return goals.Count > 0 ? goals : EnemyFaction.DefaultGoals();
        }

        private void CommitCurrent()
        {
            if (_loading || _current == null) return;
            _current.Name = string.IsNullOrWhiteSpace(_nameBox.Text) ? "Unnamed Faction" : _nameBox.Text.Trim();
            _current.Frequency = SelectedFrequency();
            _current.CustomFrequencyPercent = (int)_customFrequency.Value;
            _current.RaidFrequencyPercent = _current.BaseFrequencyPercent;
            _current.OutpostFrequencyMultiplier = (double)_outpostMultiplier.Value;
            _current.Outpost = _hasOutpost.Checked
                ? new FactionOutpost
                {
                    Name = string.IsNullOrWhiteSpace(_outpostName.Text) ? "Outpost" : _outpostName.Text.Trim(),
                    Notes = _current.Outpost?.Notes ?? string.Empty,
                    IntelPoints = _current.Outpost?.IntelPoints ?? 0
                }
                : null;
            _current.Power = (int)_power.Value;
            _current.AvailableTroops = new List<TroopType>();
            _current.TroopDisplayNames ??= new Dictionary<string, string>();
            _current.TroopDisplayNames.Clear();
            foreach (var def in TroopCatalog.All)
            {
                if (_troopChecks[def.Type].Checked)
                    _current.AvailableTroops.Add(def.Type);
                string typed = (_troopNames[def.Type].Text ?? "").Trim();
                if (!string.IsNullOrEmpty(typed)
                    && !typed.Equals(def.Name, StringComparison.OrdinalIgnoreCase))
                {
                    _current.TroopDisplayNames[def.Type.ToString()] = typed;
                }
            }
            if (_current.AvailableTroops.Count == 0)
                _current.AvailableTroops = EnemyFaction.DefaultBanditTroops();
            _current.GoalPriority = GoalsFromList();
            SyncComboName();
        }

        private void SyncComboName()
        {
            if (_current == null) return;
            int index = _factions.IndexOf(_current);
            if (index < 0 || index >= _factionCombo.Items.Count) return;
            if (Convert.ToString(_factionCombo.Items[index]) != _current.Name)
                _factionCombo.Items[index] = _current.Name;
        }

        private void MoveGoal(int delta)
        {
            int index = _goals.SelectedIndex;
            int next = index + delta;
            if (index < 0 || next < 0 || next >= _goals.Items.Count) return;
            var goals = GoalsFromList();
            var item = goals[index];
            goals.RemoveAt(index);
            goals.Insert(next, item);
            RefreshGoalList(goals);
            _goals.SelectedIndex = next;
        }

        private void AddFaction(object? sender, EventArgs e)
        {
            CommitCurrent();
            var faction = EnemyFaction.CreateBandits();
            faction.Id = Guid.NewGuid().ToString();
            faction.Name = "New Faction";
            faction.Outpost = null;
            _factions.Add(faction);
            _factionCombo.Items.Add(faction.Name);
            _factionCombo.SelectedIndex = _factions.Count - 1;
        }

        private void DeleteFaction(object? sender, EventArgs e)
        {
            if (_current == null) return;
            int index = _factions.IndexOf(_current);
            if (index < 0) return;
            _factions.RemoveAt(index);
            _current = null;
            _factionCombo.Items.RemoveAt(index);
            if (_factions.Count > 0)
                _factionCombo.SelectedIndex = Math.Min(index, _factions.Count - 1);
        }

        private void SaveToStronghold()
        {
            CommitCurrent();
            var stronghold = _gameState.GetCurrentStronghold();
            stronghold.EnemyFactions = _factions.Select(Clone).ToList();
            _gameState.OnGameStateChanged();
        }

        private static EnemyFaction Clone(EnemyFaction source)
        {
            source.Normalize();
            return new EnemyFaction
            {
                Id = source.Id,
                Name = source.Name,
                Frequency = source.Frequency,
                CustomFrequencyPercent = source.CustomFrequencyPercent,
                RaidFrequencyPercent = source.BaseFrequencyPercent,
                OutpostFrequencyMultiplier = source.OutpostFrequencyMultiplier,
                Outpost = source.Outpost == null
                    ? null
                    : new FactionOutpost
                    {
                        Name = source.Outpost.Name,
                        Notes = source.Outpost.Notes,
                        IntelPoints = source.Outpost.IntelPoints
                    },
                Power = source.Power,
                AvailableTroops = source.AvailableTroops.ToList(),
                GoalPriority = source.GoalPriority.ToList(),
                CapturedNpcs = source.CapturedNpcs?.ToList() ?? new List<NPC>(),
                TroopDisplayNames = source.TroopDisplayNames != null
                    ? new Dictionary<string, string>(source.TroopDisplayNames)
                    : new Dictionary<string, string>()
            };
        }
    }
}
