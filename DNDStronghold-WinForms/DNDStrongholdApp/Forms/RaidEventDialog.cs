using System;
using System.Drawing;
using System.Windows.Forms;
using DNDStrongholdApp.Models;
using DNDStrongholdApp.Services;

namespace DNDStrongholdApp.Forms
{
    public class RaidEventDialog : Form
    {
        private readonly Stronghold _stronghold;
        private readonly GameStateService _gameState;
        private readonly TextBox _log;
        private readonly Label _status;
        private readonly NumericUpDown _attackerRoll;
        private readonly NumericUpDown _defenderRoll;
        private readonly Button _resolveButton;
        private readonly Button _continueButton;
        private bool _settling;
        private bool _allowClose;

        public RaidBattle Battle { get; }

        public RaidEventDialog(Stronghold stronghold, RaidingParty party, GameStateService gameState)
        {
            _stronghold = stronghold;
            _gameState = gameState;
            Battle = CombatService.BeginRaid(stronghold, party);

            Text = party.Surprise ? "Surprise Raid" : "Raid";
            Size = new Size(640, 620);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ControlBox = false;
            FormClosing += RaidEventDialog_FormClosing;

            var heading = new Label
            {
                Text = party.Surprise
                    ? $"{party.FactionName} strike without warning — {party.Goal}"
                    : $"{party.FactionName} are raiding — {party.Goal}",
                Location = new Point(16, 12),
                Size = new Size(600, 24),
                Font = new Font(Font, FontStyle.Bold)
            };

            _status = new Label
            {
                Location = new Point(16, 40),
                Size = new Size(600, 36),
                AutoEllipsis = true
            };

            _log = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                Location = new Point(16, 80),
                Size = new Size(592, 360),
                Text = Battle.FullReport
            };

            var rollRow = new FlowLayoutPanel
            {
                Location = new Point(16, 448),
                Size = new Size(592, 36),
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Padding = new Padding(0),
            };

            var atkLabel = new Label
            {
                Text = "Attacker d100:",
                AutoSize = true,
                Margin = new Padding(0, 8, 6, 0),
                TextAlign = ContentAlignment.MiddleLeft
            };
            _attackerRoll = new NumericUpDown
            {
                Width = 72,
                Minimum = 1,
                Maximum = 100,
                Value = 50,
                Margin = new Padding(0, 4, 6, 0),
                TextAlign = HorizontalAlignment.Right
            };
            var atkRoll = new Button { Text = "Roll", Size = new Size(56, 28), Margin = new Padding(0, 2, 18, 0) };
            atkRoll.Click += (_, _) => _attackerRoll.Value = Random.Shared.Next(1, 101);

            var defLabel = new Label
            {
                Text = "Defender d100:",
                AutoSize = true,
                Margin = new Padding(0, 8, 6, 0),
                TextAlign = ContentAlignment.MiddleLeft
            };
            _defenderRoll = new NumericUpDown
            {
                Width = 72,
                Minimum = 1,
                Maximum = 100,
                Value = 50,
                Margin = new Padding(0, 4, 6, 0),
                TextAlign = HorizontalAlignment.Right
            };
            var defRoll = new Button { Text = "Roll", Size = new Size(56, 28), Margin = new Padding(0, 2, 0, 0) };
            defRoll.Click += (_, _) => _defenderRoll.Value = Random.Shared.Next(1, 101);

            rollRow.Controls.Add(atkLabel);
            rollRow.Controls.Add(_attackerRoll);
            rollRow.Controls.Add(atkRoll);
            rollRow.Controls.Add(defLabel);
            rollRow.Controls.Add(_defenderRoll);
            rollRow.Controls.Add(defRoll);

            _resolveButton = new Button
            {
                Text = "Resolve round",
                Location = new Point(16, 500),
                Size = new Size(130, 32)
            };
            _resolveButton.Click += ResolveRoundClicked;

            _continueButton = new Button
            {
                Text = "Continue",
                Location = new Point(508, 500),
                Size = new Size(100, 32),
                Enabled = false
            };
            _continueButton.Click += (_, _) =>
            {
                SettleRaid();
                _allowClose = true;
                DialogResult = DialogResult.OK;
                Close();
            };

            Controls.Add(heading);
            Controls.Add(_status);
            Controls.Add(_log);
            Controls.Add(rollRow);
            Controls.Add(_resolveButton);
            Controls.Add(_continueButton);
            AcceptButton = _resolveButton;

            RefreshStatus();
        }

        private void ResolveRoundClicked(object? sender, EventArgs e)
        {
            if (Battle.Ended) return;
            var result = CombatService.ResolveRound(
                Battle, _stronghold, (int)_attackerRoll.Value, (int)_defenderRoll.Value);
            _log.Text = Battle.FullReport;
            _log.SelectionStart = _log.TextLength;
            _log.ScrollToCaret();
            FlushCancelledProjects();
            RefreshStatus();

            if (result.Ended)
                OnRaidEnded();
        }

        private void OnRaidEnded()
        {
            SettleRaid();
            _resolveButton.Enabled = false;
            _attackerRoll.Enabled = false;
            _defenderRoll.Enabled = false;
            _continueButton.Enabled = true;
            AcceptButton = _continueButton;
            _log.Text = Battle.FullReport;
            _log.SelectionStart = _log.TextLength;
            _log.ScrollToCaret();
            RefreshStatus();
        }

        private void RefreshStatus()
        {
            var party = Battle.Party;
            _status.Text = Battle.Ended
                ? CombatService.DescribeEnd(Battle)
                : $"Round {Battle.Round + 1} next. Raiders {Battle.LivingCount}/{Battle.StartingNumbers}, Strength {party.Stats.Strength}, HP {party.Stats.HitPoints}. Defense {CombatService.Calculate(_stronghold).TotalDefense}.";
        }

        private void FlushCancelledProjects()
        {
            if (Battle.CancelledProjects.Count == 0) return;
            _gameState.HandleRaidCancelledProjects(Battle.CancelledProjects);
            Battle.CancelledProjects.Clear();
        }

        private void SettleRaid()
        {
            if (_settling) return;
            _settling = true;
            CombatService.FinishRaid(Battle, _stronghold);
            FlushCancelledProjects();
            foreach (var name in Battle.KilledNpcs)
                _gameState.OnNPCDeath(name);
            _gameState.OnGameStateChanged();
        }

        private void RaidEventDialog_FormClosing(object? sender, FormClosingEventArgs e)
        {
            if (!_allowClose)
                e.Cancel = true;
        }
    }
}
