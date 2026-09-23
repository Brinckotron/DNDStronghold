using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using DNDStrongholdApp.Models;
using DNDStrongholdApp.Services;

namespace DNDStrongholdApp.Forms
{
    public class StrikeTrueDialog : Form
    {
        private readonly ComboBox _heroBox = new();
        private readonly ListBox _troops = new();
        private readonly List<NPC> _heroes;
        private readonly List<RaidTroop> _targets;

        public NPC? ChosenHero { get; private set; }
        public RaidTroop? ChosenTroop { get; private set; }

        public StrikeTrueDialog(List<NPC> heroes, IEnumerable<RaidTroop> livingTroops)
        {
            _heroes = heroes;
            _targets = livingTroops.Where(t => !t.IsDead).ToList();

            Text = "Heroic Act — Strike True";
            Size = new Size(460, 360);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            var intro = new Label
            {
                Text = "A hero can Strike True now, before this round is calculated. 1 damage to a chosen raider. This spends their act for the raid.",
                Location = new Point(16, 12),
                Size = new Size(410, 48)
            };

            var heroLabel = new Label { Text = "Hero:", Location = new Point(16, 68), AutoSize = true };
            _heroBox.DropDownStyle = ComboBoxStyle.DropDownList;
            _heroBox.Location = new Point(70, 64);
            _heroBox.Size = new Size(350, 24);
            foreach (var hero in _heroes)
                _heroBox.Items.Add(hero.Name);
            if (_heroBox.Items.Count > 0) _heroBox.SelectedIndex = 0;

            _troops.Location = new Point(16, 100);
            _troops.Size = new Size(404, 160);
            foreach (var troop in _targets)
                _troops.Items.Add($"{troop.Name}  —  {troop.HitPoints}/{troop.MaxHitPoints} HP, Strength {troop.Strength}");
            if (_troops.Items.Count > 0) _troops.SelectedIndex = 0;

            var strike = new Button { Text = "Strike True", Location = new Point(200, 276), Size = new Size(110, 30) };
            strike.Click += (_, _) =>
            {
                if (_heroBox.SelectedIndex < 0 || _troops.SelectedIndex < 0) return;
                ChosenHero = _heroes[_heroBox.SelectedIndex];
                ChosenTroop = _targets[_troops.SelectedIndex];
                DialogResult = DialogResult.OK;
                Close();
            };

            var skip = new Button
            {
                Text = "Not now",
                Location = new Point(320, 276),
                Size = new Size(100, 30),
                DialogResult = DialogResult.Cancel
            };

            Controls.Add(intro);
            Controls.Add(heroLabel);
            Controls.Add(_heroBox);
            Controls.Add(_troops);
            Controls.Add(strike);
            Controls.Add(skip);
            AcceptButton = strike;
            CancelButton = skip;
        }
    }

    public class RallyDialog : Form
    {
        private readonly ComboBox _heroBox = new();
        private readonly NumericUpDown _roll = new();
        private readonly List<NPC> _heroes;

        public NPC? ChosenHero { get; private set; }
        public int NewDefenderRoll => (int)_roll.Value;

        public RallyDialog(List<NPC> heroes, CombatService.RaidRoundPreview preview)
        {
            _heroes = heroes;

            Text = "Heroic Act — Rally";
            Size = new Size(480, 250);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            var intro = new Label
            {
                Text = $"The defenders have lost this comparison.\n"
                     + $"Attackers {preview.Result.AttackerRoll} + {preview.Strength} = {preview.Result.AttackerTotal}.  "
                     + $"Defenders {preview.Result.DefenderRoll} + {preview.Defense} = {preview.Result.DefenderTotal}.\n"
                     + $"{preview.ComparisonLine()}\n"
                     + "A hero can Rally: reroll the defender d100 before casualties.",
                Location = new Point(16, 12),
                Size = new Size(430, 80)
            };

            var heroLabel = new Label { Text = "Hero:", Location = new Point(16, 100), AutoSize = true };
            _heroBox.DropDownStyle = ComboBoxStyle.DropDownList;
            _heroBox.Location = new Point(70, 96);
            _heroBox.Size = new Size(370, 24);
            foreach (var hero in _heroes)
                _heroBox.Items.Add(hero.Name);
            if (_heroBox.Items.Count > 0) _heroBox.SelectedIndex = 0;

            var rollLabel = new Label { Text = "New d100:", Location = new Point(16, 138), AutoSize = true };
            _roll.Location = new Point(100, 134);
            _roll.Minimum = 1;
            _roll.Maximum = 100;
            _roll.Value = preview.Result.DefenderRoll;
            _roll.Width = 72;
            var rollButton = new Button { Text = "Roll", Location = new Point(180, 132), Size = new Size(64, 26) };
            rollButton.Click += (_, _) => _roll.Value = Random.Shared.Next(1, 101);

            var rally = new Button { Text = "Rally", Location = new Point(230, 172), Size = new Size(100, 30) };
            rally.Click += (_, _) =>
            {
                if (_heroBox.SelectedIndex < 0) return;
                ChosenHero = _heroes[_heroBox.SelectedIndex];
                DialogResult = DialogResult.OK;
                Close();
            };

            var skip = new Button
            {
                Text = "Let the loss stand",
                Location = new Point(340, 172),
                Size = new Size(110, 30),
                DialogResult = DialogResult.Cancel
            };

            Controls.Add(intro);
            Controls.Add(heroLabel);
            Controls.Add(_heroBox);
            Controls.Add(rollLabel);
            Controls.Add(_roll);
            Controls.Add(rollButton);
            Controls.Add(rally);
            Controls.Add(skip);
            AcceptButton = rally;
            CancelButton = skip;
        }
    }

    public class HoldTheLineDialog : Form
    {
        private readonly ComboBox _heroBox = new();
        private readonly List<NPC> _heroes;

        public NPC? ChosenHero { get; private set; }

        public HoldTheLineDialog(List<NPC> heroes, CombatService.RaidRoundPreview preview)
        {
            _heroes = heroes;

            Text = "Heroic Act — Hold the Line";
            Size = new Size(500, 250);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            var intro = new Label
            {
                Text = $"{preview.ComparisonLine()}\n"
                     + "A hero can Hold the Line before casualties are rolled: defender dice "
                     + "step down (a→b, b→c, c ignored; aa→bb, bb→cc, cc ignored), and the "
                     + "raiders gain no ground this round. People can still die.",
                Location = new Point(16, 12),
                Size = new Size(450, 90)
            };

            var heroLabel = new Label { Text = "Hero:", Location = new Point(16, 112), AutoSize = true };
            _heroBox.DropDownStyle = ComboBoxStyle.DropDownList;
            _heroBox.Location = new Point(70, 108);
            _heroBox.Size = new Size(390, 24);
            foreach (var hero in _heroes)
                _heroBox.Items.Add(hero.Name);
            if (_heroBox.Items.Count > 0) _heroBox.SelectedIndex = 0;

            var hold = new Button { Text = "Hold the Line", Location = new Point(230, 168), Size = new Size(130, 30) };
            hold.Click += (_, _) =>
            {
                if (_heroBox.SelectedIndex < 0) return;
                ChosenHero = _heroes[_heroBox.SelectedIndex];
                DialogResult = DialogResult.OK;
                Close();
            };

            var skip = new Button
            {
                Text = "Not now",
                Location = new Point(370, 168),
                Size = new Size(90, 30),
                DialogResult = DialogResult.Cancel
            };

            Controls.Add(intro);
            Controls.Add(heroLabel);
            Controls.Add(_heroBox);
            Controls.Add(hold);
            Controls.Add(skip);
            AcceptButton = hold;
            CancelButton = skip;
        }
    }

    public class CasualtyHeroicsDialog : Form
    {
        private readonly ComboBox _heroBox = new();
        private readonly ListBox _hits = new();
        private readonly List<NPC> _heroes;

        public NPC? ChosenHero { get; private set; }
        public int InterposeIndex { get; private set; } = -1;

        public CasualtyHeroicsDialog(List<NPC> heroes, CombatService.RaidRoundPreview preview)
        {
            _heroes = heroes;

            Text = "Heroic Act — Interpose";
            Size = new Size(520, 400);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            var intro = new Label
            {
                Text = preview.ComparisonLine()
                     + " Casualties have been worked out but not yet applied.\n"
                     + "A hero may Interpose: the chosen blow lands on them instead, at the same severity.",
                Location = new Point(16, 12),
                Size = new Size(470, 56)
            };

            var heroLabel = new Label { Text = "Hero:", Location = new Point(16, 76), AutoSize = true };
            _heroBox.DropDownStyle = ComboBoxStyle.DropDownList;
            _heroBox.Location = new Point(70, 72);
            _heroBox.Size = new Size(410, 24);
            foreach (var hero in _heroes)
                _heroBox.Items.Add(hero.Name);
            if (_heroBox.Items.Count > 0) _heroBox.SelectedIndex = 0;

            _hits.Location = new Point(16, 108);
            _hits.Size = new Size(464, 180);
            foreach (var hit in preview.DefenderCasualties)
                _hits.Items.Add(hit.Note);
            int firstSaveable = preview.DefenderCasualties.FindIndex(h => h.IsMeaningful);
            if (firstSaveable >= 0) _hits.SelectedIndex = firstSaveable;

            var interpose = new Button { Text = "Interpose", Location = new Point(16, 304), Size = new Size(110, 30) };
            interpose.Click += (_, _) =>
            {
                if (_heroBox.SelectedIndex < 0 || _hits.SelectedIndex < 0) return;
                var hit = preview.DefenderCasualties[_hits.SelectedIndex];
                var hero = _heroes[_heroBox.SelectedIndex];
                if (!hit.IsMeaningful || hit.Npc.Id == hero.Id)
                {
                    MessageBox.Show("Pick someone else's hit to take.", "Interpose",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                ChosenHero = hero;
                InterposeIndex = _hits.SelectedIndex;
                DialogResult = DialogResult.OK;
                Close();
            };

            var skip = new Button
            {
                Text = "Stand aside",
                Location = new Point(380, 304),
                Size = new Size(100, 30),
                DialogResult = DialogResult.Cancel
            };

            Controls.Add(intro);
            Controls.Add(heroLabel);
            Controls.Add(_heroBox);
            Controls.Add(_hits);
            Controls.Add(interpose);
            Controls.Add(skip);
            CancelButton = skip;
        }
    }
}
