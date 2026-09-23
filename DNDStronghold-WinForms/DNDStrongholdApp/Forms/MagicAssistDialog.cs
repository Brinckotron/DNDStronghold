using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using DNDStrongholdApp.Models;
using DNDStrongholdApp.Services;

namespace DNDStrongholdApp.Forms
{
    // Lets assigned spellcasters buy construction progress for the coming week. The
    // points land on Next Turn, so the pledge is recorded on the building rather than
    // applied immediately.
    public class MagicAssistDialog : Form
    {
        private readonly Building _building;
        private readonly Stronghold _stronghold;
        private readonly List<NPC> _casters;
        private readonly int _maxIncrements;

        private NumericUpDown _increments = null!;
        private Label _progressLabel = null!;
        private Label _costLabel = null!;
        private Button _confirmButton = null!;

        public MagicAssistDialog(Building building, Stronghold stronghold)
        {
            _building = building;
            _stronghold = stronghold;
            _casters = MagicService.AssistCasters(building, stronghold);
            _maxIncrements = MagicService.MaxAssistIncrements(building, stronghold);

            Text = "Magic Assist";
            Size = new Size(520, 400);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            BuildLayout();
            RefreshCost();
        }

        private void BuildLayout()
        {
            string names = _casters.Count == 0
                ? "No one"
                : string.Join(", ", _casters.Select(c => c.Name));

            var prompt = new Label
            {
                Text = $"{names} can assist in this construction work with magic for a cost.",
                Location = new Point(16, 16),
                Size = new Size(480, 40)
            };

            var casterList = new Label
            {
                Text = _casters.Count == 0
                    ? string.Empty
                    : string.Join(Environment.NewLine,
                        _casters.Select(c => $"    {c.Name} — spellcasting {c.SpellcastingLevel}")),
                Location = new Point(16, 60),
                Size = new Size(480, 60)
            };

            _progressLabel = new Label
            {
                Location = new Point(16, 124),
                Size = new Size(480, 40)
            };

            var detail = new Label
            {
                Text = $"Each increment adds {MagicService.PointsPerIncrement} construction points "
                     + $"for {MagicService.MagicAssistGoldPerIncrement} gold. "
                     + $"Up to {_maxIncrements} increment(s) available this week.",
                Location = new Point(16, 168),
                Size = new Size(480, 36)
            };

            var incrementLabel = new Label
            {
                Text = "Increments:",
                Location = new Point(16, 212),
                AutoSize = true
            };

            _increments = new NumericUpDown
            {
                Location = new Point(100, 208),
                Width = 70,
                Minimum = 0,
                Maximum = Math.Max(0, _maxIncrements),
                Value = _maxIncrements > 0 ? 1 : 0,
                TextAlign = HorizontalAlignment.Right
            };
            _increments.ValueChanged += (_, _) => RefreshCost();

            _costLabel = new Label
            {
                Location = new Point(186, 212),
                Size = new Size(310, 20)
            };

            var warning = new Label
            {
                Text = "The gold is spent now. If a caster leaves this building before the week "
                     + "advances, their share of the boost is lost and not refunded.",
                Location = new Point(16, 244),
                Size = new Size(480, 40),
                ForeColor = Color.DarkRed
            };

            _confirmButton = new Button
            {
                Text = "Confirm",
                Location = new Point(300, 304),
                Size = new Size(90, 30)
            };
            _confirmButton.Click += ConfirmClicked;

            var cancel = new Button
            {
                Text = "Cancel",
                Location = new Point(400, 304),
                Size = new Size(90, 30),
                DialogResult = DialogResult.Cancel
            };

            Controls.Add(prompt);
            Controls.Add(casterList);
            Controls.Add(_progressLabel);
            Controls.Add(detail);
            Controls.Add(incrementLabel);
            Controls.Add(_increments);
            Controls.Add(_costLabel);
            Controls.Add(warning);
            Controls.Add(_confirmButton);
            Controls.Add(cancel);
            AcceptButton = _confirmButton;
            CancelButton = cancel;
        }

        private void RefreshCost()
        {
            int count = (int)_increments.Value;
            int gold = count * MagicService.MagicAssistGoldPerIncrement;
            int points = count * MagicService.PointsPerIncrement;
            int treasury = MagicService.GetGold(_stronghold);

            _costLabel.Text = $"+{points} points for {gold} gold (have {treasury}).";
            bool affordable = count > 0 && gold <= treasury;
            _costLabel.ForeColor = count > 0 && !affordable ? Color.DarkRed : SystemColors.ControlText;
            _confirmButton.Enabled = affordable;
            RefreshProgress(count);
        }

        private void RefreshProgress(int extraIncrements)
        {
            int current = Math.Max(0, _building.CurrentConstructionPoints);
            int total = Math.Max(0, _building.RequiredConstructionPoints);
            int workers = MagicService.WorkerConstructionPoints(_building, _stronghold);
            int magic = MagicService.PledgedPoints(_building) + extraIncrements * MagicService.PointsPerIncrement;
            int projected = Math.Min(total, current + workers + magic);
            string finishes = current + workers + magic >= total && total > 0 ? " — finishes" : string.Empty;

            _progressLabel.Text = $"Progress: {current} / {total}"
                + Environment.NewLine
                + $"Next turn: {projected} / {total}  (workers +{workers}, magic +{magic}){finishes}";
        }

        private void ConfirmClicked(object? sender, EventArgs e)
        {
            int requested = (int)_increments.Value;
            if (requested <= 0) return;

            int gold = requested * MagicService.MagicAssistGoldPerIncrement;
            if (!MagicService.SpendGold(_stronghold, gold))
            {
                MessageBox.Show($"Not enough gold ({gold} needed).", "Magic Assist",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                RefreshCost();
                return;
            }

            // Spread the increments across the casters, filling each up to their own
            // spellcasting level. Per-caster pledges let a single departure void only
            // that caster's share.
            _building.PendingMagicAssist ??= new List<MagicAssistPledge>();
            int remaining = requested;
            foreach (var caster in _casters)
            {
                if (remaining <= 0) break;

                int alreadyPledged = _building.PendingMagicAssist
                    .Where(p => p.NpcId == caster.Id)
                    .Sum(p => p.Increments);
                int room = Math.Max(0, caster.SpellcastingLevel - alreadyPledged);
                int share = Math.Min(room, remaining);
                if (share <= 0) continue;

                _building.PendingMagicAssist.Add(new MagicAssistPledge
                {
                    NpcId = caster.Id,
                    NpcName = caster.Name,
                    Increments = share,
                    GoldPaid = share * MagicService.MagicAssistGoldPerIncrement
                });
                remaining -= share;
            }

            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
