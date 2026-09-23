using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using DNDStrongholdApp.Models;
using DNDStrongholdApp.Services;

namespace DNDStrongholdApp.Forms
{
    // Spends a Faith caster's weekly heal points on wounded or sick NPCs. Serves both
    // the NPC tab (peacetime, one named healer) and the gap between raid rounds, where
    // any available healer can step in but sickness cannot be cured.
    public class MagicHealingDialog : Form
    {
        private readonly Stronghold _stronghold;
        private readonly bool _raidMode;
        private readonly NPC? _fixedHealer;

        private ComboBox _healerCombo = null!;
        private Label _poolLabel = null!;
        private ListView _patients = null!;
        private Button _healButton = null!;
        private TextBox _log = null!;

        public bool AnyHealingDone { get; private set; }

        public MagicHealingDialog(Stronghold stronghold, NPC? healer, bool raidMode)
        {
            _stronghold = stronghold;
            _raidMode = raidMode;
            _fixedHealer = healer;

            Text = raidMode ? "Magic Healing — Between Rounds" : "Magic Healing";
            Size = new Size(620, 520);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            BuildLayout();
            PopulateHealers();
            RefreshAll();
        }

        private void BuildLayout()
        {
            var intro = new Label
            {
                Text = _raidMode
                    ? "Healers may spend heal points before the next round. Anyone restored rejoins the defense."
                    : "Spend heal points to cure wounds and sickness.",
                Location = new Point(16, 12),
                Size = new Size(570, 32)
            };

            var healerLabel = new Label
            {
                Text = "Healer:",
                Location = new Point(16, 52),
                AutoSize = true
            };

            _healerCombo = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(75, 48),
                Width = 240
            };
            _healerCombo.SelectedIndexChanged += (_, _) => RefreshAll();

            _poolLabel = new Label
            {
                Location = new Point(330, 52),
                Size = new Size(256, 20)
            };

            _patients = new ListView
            {
                Location = new Point(16, 84),
                Size = new Size(570, 220),
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                MultiSelect = false
            };
            _patients.Columns.Add("Patient", 190);
            _patients.Columns.Add("Condition", 130);
            _patients.Columns.Add("Heal Points", 90);
            _patients.Columns.Add("Gold", 70);
            _patients.Columns.Add("Status", 80);
            _patients.SelectedIndexChanged += (_, _) => RefreshHealButton();

            _healButton = new Button
            {
                Text = "Heal",
                Location = new Point(16, 314),
                Size = new Size(90, 30),
                Enabled = false
            };
            _healButton.Click += HealClicked;

            _log = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                Location = new Point(16, 354),
                Size = new Size(570, 78),
                BackColor = SystemColors.Control
            };

            var close = new Button
            {
                Text = _raidMode ? "Continue" : "Close",
                Location = new Point(486, 442),
                Size = new Size(100, 30),
                DialogResult = DialogResult.OK
            };

            Controls.Add(intro);
            Controls.Add(healerLabel);
            Controls.Add(_healerCombo);
            Controls.Add(_poolLabel);
            Controls.Add(_patients);
            Controls.Add(_healButton);
            Controls.Add(_log);
            Controls.Add(close);
            AcceptButton = close;
            CancelButton = close;
        }

        private void PopulateHealers()
        {
            _healerCombo.Items.Clear();

            var healers = _fixedHealer != null
                ? new List<NPC> { _fixedHealer }
                : MagicService.AvailableHealers(_stronghold);

            foreach (var healer in healers)
                _healerCombo.Items.Add(new HealerEntry(healer));

            _healerCombo.Enabled = _fixedHealer == null && healers.Count > 1;
            if (_healerCombo.Items.Count > 0)
                _healerCombo.SelectedIndex = 0;
        }

        private NPC? SelectedHealer()
        {
            return (_healerCombo.SelectedItem as HealerEntry)?.Npc;
        }

        private void RefreshAll()
        {
            var healer = SelectedHealer();

            _poolLabel.Text = healer == null
                ? "No healer available."
                : $"{healer.HealPointsRemaining}/{healer.FaithLevel} heal points — {MagicService.GetGold(_stronghold)} gold";

            _patients.BeginUpdate();
            _patients.Items.Clear();

            foreach (var patient in PatientPool())
            {
                foreach (var state in MagicService.TreatableStates(patient, _raidMode))
                {
                    var (points, gold) = MagicService.HealCost(state);
                    string status;
                    if (healer == null) status = "No healer";
                    else if (healer.HealPointsRemaining < points) status = "No points";
                    else if (gold > 0 && MagicService.GetGold(_stronghold) < gold) status = "No gold";
                    else status = "Ready";

                    var item = new ListViewItem(patient.Name);
                    item.SubItems.Add(MagicService.DescribeState(state));
                    item.SubItems.Add(points.ToString());
                    item.SubItems.Add(gold > 0 ? gold.ToString() : "-");
                    item.SubItems.Add(status);
                    item.Tag = new PatientEntry(patient, state);
                    if (status != "Ready")
                        item.ForeColor = Color.Gray;
                    _patients.Items.Add(item);
                }
            }

            if (_patients.Items.Count == 0)
            {
                var item = new ListViewItem(_raidMode ? "No treatable wounds" : "No wounded or sick NPCs");
                item.ForeColor = Color.Gray;
                _patients.Items.Add(item);
            }

            _patients.EndUpdate();
            RefreshHealButton();
        }

        // In a raid only NPCs actually at the stronghold can be reached.
        private List<NPC> PatientPool()
        {
            return _stronghold.NPCs
                .Where(n => n.IsAlive && n.States != null && n.States.Count > 0)
                .Where(n => !_raidMode || !CombatService.IsAway(n, _stronghold))
                .OrderBy(n => n.Name)
                .ToList();
        }

        private void RefreshHealButton()
        {
            var healer = SelectedHealer();
            var entry = SelectedPatient();
            _healButton.Enabled = healer != null
                && entry != null
                && MagicService.CanAffordHeal(healer, _stronghold, entry.State);
        }

        private PatientEntry? SelectedPatient()
        {
            if (_patients.SelectedItems.Count == 0) return null;
            return _patients.SelectedItems[0].Tag as PatientEntry;
        }

        private void HealClicked(object? sender, EventArgs e)
        {
            var healer = SelectedHealer();
            var entry = SelectedPatient();
            if (healer == null || entry == null) return;

            bool ok = MagicService.TryHeal(healer, entry.Patient, _stronghold, entry.State, _raidMode,
                out string message);
            if (ok) AnyHealingDone = true;

            AppendLog(message);

            // A spent-out healer drops off the raid list; rebuild so the DM can switch.
            if (_fixedHealer == null)
            {
                var previous = healer;
                PopulateHealers();
                for (int i = 0; i < _healerCombo.Items.Count; i++)
                {
                    if (_healerCombo.Items[i] is HealerEntry candidate && candidate.Npc.Id == previous.Id)
                    {
                        _healerCombo.SelectedIndex = i;
                        break;
                    }
                }
            }

            RefreshAll();
        }

        private void AppendLog(string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return;
            _log.Text = string.IsNullOrEmpty(_log.Text)
                ? message
                : _log.Text + Environment.NewLine + message;
            _log.SelectionStart = _log.TextLength;
            _log.ScrollToCaret();
        }

        private sealed class HealerEntry
        {
            public NPC Npc { get; }

            public HealerEntry(NPC npc) => Npc = npc;

            public override string ToString() =>
                $"{Npc.Name} — Faith {Npc.FaithLevel} ({Npc.HealPointsRemaining} left)";
        }

        private sealed class PatientEntry
        {
            public NPC Patient { get; }
            public NPCStateType State { get; }

            public PatientEntry(NPC patient, NPCStateType state)
            {
                Patient = patient;
                State = state;
            }
        }
    }
}
