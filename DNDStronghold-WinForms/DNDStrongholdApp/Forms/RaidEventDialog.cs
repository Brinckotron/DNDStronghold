using System;
using System.Drawing;
using System.Linq;
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
        private readonly Label _massDebuffLabel;
        private readonly Button _beginRoundButton;
        private readonly Button _resolveButton;
        private readonly Button _continueButton;
        private bool _settling;
        private bool _allowClose;

        // A round with arcane casters available is two-phase: spells are declared first
        // because Mass Debuff changes how the attacker d100 is produced.
        private bool _spellsDeclared;

        public RaidBattle Battle { get; }

        public RaidEventDialog(Stronghold stronghold, RaidingParty party, GameStateService gameState)
        {
            _stronghold = stronghold;
            _gameState = gameState;
            Battle = CombatService.BeginRaid(stronghold, party);

            Text = party.Surprise ? "Surprise Raid" : "Raid";
            Size = new Size(640, 640);
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
                Size = new Size(592, 336),
                Text = Battle.FullReport
            };

            var rollRow = new FlowLayoutPanel
            {
                Location = new Point(16, 424),
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
            atkRoll.Click += (_, _) => RollAttackerD100();

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

            _massDebuffLabel = new Label
            {
                Location = new Point(16, 462),
                Size = new Size(592, 20),
                ForeColor = Color.DarkBlue,
                Visible = false
            };

            _beginRoundButton = new Button
            {
                Text = "Begin Round",
                Location = new Point(16, 490),
                Size = new Size(120, 32)
            };
            _beginRoundButton.Click += BeginRoundClicked;

            _resolveButton = new Button
            {
                Text = "Resolve round",
                Location = new Point(146, 490),
                Size = new Size(130, 32)
            };
            _resolveButton.Click += ResolveRoundClicked;

            _continueButton = new Button
            {
                Text = "Continue",
                Location = new Point(508, 490),
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
            Controls.Add(_massDebuffLabel);
            Controls.Add(_beginRoundButton);
            Controls.Add(_resolveButton);
            Controls.Add(_continueButton);

            PrepareRound();
        }

        // Sets up the phase controls for the round that is about to be fought.
        private void PrepareRound()
        {
            _spellsDeclared = false;
            Battle.PendingDefenderSpells.Clear();
            _massDebuffLabel.Visible = false;
            _massDebuffLabel.Text = string.Empty;

            bool castersWaiting = !Battle.Ended && MagicService.AvailableArcanists(_stronghold).Count > 0;

            _beginRoundButton.Visible = castersWaiting;
            _beginRoundButton.Enabled = castersWaiting;
            SetRollRowEnabled(!castersWaiting && !Battle.Ended);
            _resolveButton.Enabled = !castersWaiting && !Battle.Ended;
            AcceptButton = castersWaiting ? _beginRoundButton : _resolveButton;

            RefreshStatus();
        }

        private void SetRollRowEnabled(bool enabled)
        {
            _attackerRoll.Enabled = enabled;
            _defenderRoll.Enabled = enabled;
        }

        private void BeginRoundClicked(object? sender, EventArgs e)
        {
            if (Battle.Ended) return;

            using (var dialog = new CastDefenderSpellsDialog(_stronghold, Battle))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;

                foreach (var cast in dialog.Casts)
                    Battle.PendingDefenderSpells.Add(cast);
            }

            _spellsDeclared = true;
            _beginRoundButton.Enabled = false;
            SetRollRowEnabled(true);
            _resolveButton.Enabled = true;
            AcceptButton = _resolveButton;

            if (MassDebuffActive())
            {
                _massDebuffLabel.Text = "Mass Debuff — attackers roll twice, take the lowest.";
                _massDebuffLabel.Visible = true;
            }

            RefreshStatus();
        }

        private bool MassDebuffActive()
        {
            return _spellsDeclared && Battle.DefenderSpellActive(DefenderSpell.MassDebuff);
        }

        private void RollAttackerD100()
        {
            if (!MassDebuffActive())
            {
                _attackerRoll.Value = Random.Shared.Next(1, 101);
                return;
            }

            int first = Random.Shared.Next(1, 101);
            int second = Random.Shared.Next(1, 101);
            int lowest = Math.Min(first, second);
            _attackerRoll.Value = lowest;
            _massDebuffLabel.Text = $"Mass Debuff — rolled {first} and {second}, taking {lowest}.";
            _massDebuffLabel.Visible = true;
        }

        private void ResolveRoundClicked(object? sender, EventArgs e)
        {
            if (Battle.Ended) return;

            string strikeLine = OfferStrikeTrue();

            var preview = CombatService.StartRoundResolution(
                Battle, _stronghold, (int)_attackerRoll.Value, (int)_defenderRoll.Value);
            if (!preview.Ready)
                return;
            if (!string.IsNullOrEmpty(strikeLine))
                preview.AddHeroicLine(strikeLine);

            OfferRally(preview);
            OfferHoldTheLine(preview);
            preview.ProjectCasualties();
            OfferCasualtyHeroics(preview);

            var result = preview.Commit();
            RefreshLog();
            FlushCancelledProjects();

            if (result.Ended)
            {
                OnRaidEnded();
                return;
            }

            OfferInterRoundHealing();
            PrepareRound();
        }

        private string OfferStrikeTrue()
        {
            var heroes = CombatService.AvailableHeroes(_stronghold, Battle);
            var living = Battle.Party.Combatants?.Where(t => !t.IsDead).ToList();
            if (heroes.Count == 0 || living == null || living.Count == 0) return string.Empty;

            using var dialog = new StrikeTrueDialog(heroes, living);
            if (dialog.ShowDialog(this) != DialogResult.OK || dialog.ChosenHero == null || dialog.ChosenTroop == null)
                return string.Empty;

            return CombatService.ApplyStrikeTrue(Battle, _stronghold, dialog.ChosenHero, dialog.ChosenTroop);
        }

        private void OfferRally(CombatService.RaidRoundPreview preview)
        {
            if (!preview.AttackerWon) return;
            var heroes = CombatService.AvailableHeroes(_stronghold, Battle);
            if (heroes.Count == 0) return;

            using var dialog = new RallyDialog(heroes, preview);
            if (dialog.ShowDialog(this) != DialogResult.OK || dialog.ChosenHero == null)
                return;

            preview.ApplyRally(dialog.NewDefenderRoll);
            _defenderRoll.Value = dialog.NewDefenderRoll;
            CombatService.SpendHeroicAct(Battle, dialog.ChosenHero);
            preview.AddHeroicLine($"{dialog.ChosenHero.Name} Rallies. Defenders reroll {dialog.NewDefenderRoll} + {preview.Defense} = {preview.Result.DefenderTotal}.");
        }

        private void OfferHoldTheLine(CombatService.RaidRoundPreview preview)
        {
            var heroes = CombatService.AvailableHeroes(_stronghold, Battle);
            if (heroes.Count == 0) return;

            using var dialog = new HoldTheLineDialog(heroes, preview);
            if (dialog.ShowDialog(this) != DialogResult.OK || dialog.ChosenHero == null)
                return;

            preview.HoldTheLine(dialog.ChosenHero);
            CombatService.SpendHeroicAct(Battle, dialog.ChosenHero);
        }

        private void OfferCasualtyHeroics(CombatService.RaidRoundPreview preview)
        {
            var heroes = CombatService.AvailableHeroes(_stronghold, Battle);
            if (heroes.Count == 0) return;
            if (!preview.DefenderCasualties.Any(c => c.IsMeaningful)) return;

            using var dialog = new CasualtyHeroicsDialog(heroes, preview);
            if (dialog.ShowDialog(this) != DialogResult.OK || dialog.ChosenHero == null)
                return;

            preview.Interpose(dialog.InterposeIndex, dialog.ChosenHero);
            CombatService.SpendHeroicAct(Battle, dialog.ChosenHero);
        }

        // Healers get a window between rounds. Defense is recalculated at the top of the
        // next round, so anyone restored here rejoins the fight.
        private void OfferInterRoundHealing()
        {
            if (Battle.Ended) return;
            if (MagicService.AvailableHealers(_stronghold).Count == 0) return;

            bool anyWounded = _stronghold.NPCs.Any(n =>
                n.IsAlive
                && n.States != null
                && MagicService.TreatableStates(n, raidMode: true).Any()
                && !CombatService.IsAway(n, _stronghold));
            if (!anyWounded) return;

            using (var dialog = new MagicHealingDialog(_stronghold, null, raidMode: true))
            {
                dialog.ShowDialog(this);
                if (dialog.AnyHealingDone)
                {
                    Battle.Log.Add("Healers tended the wounded between rounds.");
                    Battle.Log.Add(string.Empty);
                    RefreshLog();
                }
            }
        }

        private void OnRaidEnded()
        {
            SettleRaid();
            _beginRoundButton.Visible = false;
            _resolveButton.Enabled = false;
            SetRollRowEnabled(false);
            _massDebuffLabel.Visible = false;
            _continueButton.Enabled = true;
            AcceptButton = _continueButton;
            RefreshLog();
            RefreshStatus();
        }

        private void RefreshLog()
        {
            _log.Text = Battle.FullReport;
            _log.SelectionStart = _log.TextLength;
            _log.ScrollToCaret();
        }

        private void RefreshStatus()
        {
            var party = Battle.Party;
            if (Battle.Ended)
            {
                _status.Text = CombatService.DescribeEnd(Battle);
                return;
            }

            string text = $"Round {Battle.Round + 1} next. Raiders {Battle.LivingCount}/{Battle.StartingNumbers}, "
                        + $"Strength {party.Stats.Strength}, HP {party.Stats.HitPoints}. "
                        + $"Defense {CombatService.Calculate(_stronghold).TotalDefense}.";

            if (_beginRoundButton.Visible && !_spellsDeclared)
                text += " Declare spells to begin the round.";

            _status.Text = text;
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
