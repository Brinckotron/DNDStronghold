using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using DNDStrongholdApp.Models;
using DNDStrongholdApp.Services;

namespace DNDStrongholdApp.Forms
{
    public class TriggerRaidDialog : Form
    {
        private readonly Stronghold _stronghold;
        private ComboBox _factionCombo = null!;
        private ComboBox _goalCombo = null!;
        private Label _powerLabel = null!;
        private Label _statsLabel = null!;
        private readonly Dictionary<TroopType, NumericUpDown> _counts = new();
        private readonly Dictionary<TroopType, Label> _troopLabels = new();
        private bool _loading;

        public RaidingParty? Party { get; private set; }

        public TriggerRaidDialog(Stronghold stronghold)
        {
            _stronghold = stronghold;
            Text = "Trigger Raid";
            Size = new Size(520, 620);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            var top = new Panel { Dock = DockStyle.Top, Height = 150, Padding = new Padding(12) };
            top.Controls.Add(new Label { Text = "Faction:", Location = new Point(0, 8), AutoSize = true });
            _factionCombo = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(110, 4),
                Width = 360
            };
            foreach (var faction in stronghold.EnemyFactions)
                _factionCombo.Items.Add(faction.Name);
            top.Controls.Add(_factionCombo);

            _powerLabel = new Label { Location = new Point(110, 36), AutoSize = true };
            top.Controls.Add(_powerLabel);

            top.Controls.Add(new Label { Text = "Goal:", Location = new Point(0, 64), AutoSize = true });
            _goalCombo = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(110, 60),
                Width = 220
            };
            foreach (OperationalGoal goal in Enum.GetValues<OperationalGoal>())
                _goalCombo.Items.Add(goal.ToString());
            top.Controls.Add(_goalCombo);

            var generate = new Button
            {
                Text = "Generate roster",
                Location = new Point(340, 58),
                Size = new Size(130, 28)
            };
            generate.Click += (_, _) => GenerateRoster();
            top.Controls.Add(generate);

            _statsLabel = new Label
            {
                Location = new Point(0, 100),
                Size = new Size(480, 36),
                AutoEllipsis = true
            };
            top.Controls.Add(_statsLabel);

            var rosterBox = new GroupBox
            {
                Text = "Raid roster",
                Dock = DockStyle.Fill,
                Padding = new Padding(10)
            };
            var grid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                AutoScroll = true
            };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 70));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));
            grid.RowCount = TroopCatalog.All.Count;
            for (int i = 0; i < TroopCatalog.All.Count; i++)
            {
                var def = TroopCatalog.All[i];
                grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
                var label = new Label
                {
                    Text = TroopLabel(def, null),
                    TextAlign = ContentAlignment.MiddleLeft,
                    Dock = DockStyle.Fill,
                    AutoEllipsis = true
                };
                _troopLabels[def.Type] = label;
                grid.Controls.Add(label, 0, i);
                var count = new NumericUpDown
                {
                    Minimum = 0,
                    Maximum = 99,
                    Width = 70,
                    Anchor = AnchorStyles.Left
                };
                count.ValueChanged += (_, _) => RefreshStats();
                _counts[def.Type] = count;
                grid.Controls.Add(count, 1, i);
            }
            rosterBox.Controls.Add(grid);

            var bottom = new Panel { Dock = DockStyle.Bottom, Height = 48 };
            var start = new Button { Text = "Start Raid", Location = new Point(300, 10), Size = new Size(100, 28) };
            start.Click += StartRaid;
            var cancel = new Button
            {
                Text = "Cancel",
                Location = new Point(410, 10),
                Size = new Size(80, 28),
                DialogResult = DialogResult.Cancel
            };
            bottom.Controls.Add(start);
            bottom.Controls.Add(cancel);

            _factionCombo.SelectedIndexChanged += (_, _) =>
            {
                SyncFromFaction();
                GenerateRoster();
            };
            _goalCombo.SelectedIndexChanged += (_, _) => RefreshStats();

            Controls.Add(rosterBox);
            Controls.Add(bottom);
            Controls.Add(top);
            AcceptButton = start;
            CancelButton = cancel;

            if (_factionCombo.Items.Count > 0)
                _factionCombo.SelectedIndex = 0;
        }

        private EnemyFaction? SelectedFaction()
        {
            if (_factionCombo.SelectedIndex < 0) return null;
            return _stronghold.EnemyFactions[_factionCombo.SelectedIndex];
        }

        private OperationalGoal SelectedGoal()
        {
            if (_goalCombo.SelectedItem != null
                && Enum.TryParse(_goalCombo.SelectedItem.ToString(), out OperationalGoal goal))
            {
                return goal;
            }
            return OperationalGoal.Pillage;
        }

        private void SyncFromFaction()
        {
            var faction = SelectedFaction();
            if (faction == null) return;
            faction.Normalize();
            _powerLabel.Text = faction.Power <= 0
                ? "Power 0 — Inactive (cannot raid)"
                : $"Power {faction.Power} — {FactionPower.Label(faction.Power)}";
            foreach (var def in TroopCatalog.All)
            {
                if (_troopLabels.TryGetValue(def.Type, out var label))
                    label.Text = TroopLabel(def, faction);
            }
            _loading = true;
            _goalCombo.SelectedItem = faction.GoalPriority.FirstOrDefault().ToString();
            _loading = false;
        }

        private void GenerateRoster()
        {
            var faction = SelectedFaction();
            if (faction == null || faction.Power <= 0)
            {
                ClearRoster();
                RefreshStats();
                return;
            }

            var generated = CombatService.AssembleTroops(faction, SelectedGoal());
            _loading = true;
            foreach (var box in _counts.Values)
                box.Value = 0;
            foreach (var group in generated.GroupBy(t => t))
            {
                if (_counts.TryGetValue(group.Key, out var box))
                    box.Value = Math.Min(box.Maximum, group.Count());
            }
            _loading = false;
            RefreshStats();
        }

        private void ClearRoster()
        {
            _loading = true;
            foreach (var box in _counts.Values)
                box.Value = 0;
            _loading = false;
        }

        private List<TroopType> RosterFromUi()
        {
            var troops = new List<TroopType>();
            foreach (var def in TroopCatalog.All)
            {
                int n = (int)_counts[def.Type].Value;
                for (int i = 0; i < n; i++)
                    troops.Add(def.Type);
            }
            return troops;
        }

        private void RefreshStats()
        {
            if (_loading) return;
            var troops = RosterFromUi();
            var stats = CombatService.SumTroopStats(troops);
            int divine = troops.Count(t => TroopCatalog.Get(t).Magic == MagicKind.Divine);
            int arcane = troops.Count(t => TroopCatalog.Get(t).Magic == MagicKind.Arcane);
            string casters = "";
            if (divine > 0 || arcane > 0)
            {
                var bits = new List<string>();
                if (divine > 0) bits.Add($"{divine} divine");
                if (arcane > 0) bits.Add($"{arcane} arcane");
                casters = $", {string.Join(", ", bits)}";
            }
            _statsLabel.Text = troops.Count == 0
                ? "Roster is empty."
                : $"{stats.Numbers} troops — Strength {stats.Strength}, HP {stats.HitPoints}, {CombatService.FormatStealth(stats)}{casters}";
        }

        private void StartRaid(object? sender, EventArgs e)
        {
            var faction = SelectedFaction();
            if (faction == null) return;
            if (faction.Power <= 0)
            {
                MessageBox.Show($"{faction.Name} is inactive (Power 0) and cannot field a raiding party.",
                    "Trigger Raid", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var roster = RosterFromUi();
            if (roster.Count == 0)
            {
                MessageBox.Show("Add at least one troop to the roster.", "Trigger Raid",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            Party = CombatService.CreateParty(faction, SelectedGoal(), roster);
            CombatService.ApplyDetection(Party, CombatService.Calculate(_stronghold));
            DialogResult = DialogResult.OK;
            Close();
        }

        private static string TroopLabel(TroopDefinition def, EnemyFaction? faction)
        {
            string name = faction?.TroopName(def.Type) ?? def.Name;
            return $"{name}  ({def.Strength}/{def.HitPoints}/{def.Stealth}{TroopCatalog.TagSuffix(def)})";
        }
    }
}
