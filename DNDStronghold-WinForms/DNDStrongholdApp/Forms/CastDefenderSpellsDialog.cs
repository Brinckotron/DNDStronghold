using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using DNDStrongholdApp.Models;
using DNDStrongholdApp.Services;

namespace DNDStrongholdApp.Forms
{
    // Declares the defenders' spells for the round that is about to be rolled. Mass Debuff
    // changes how the attacker d100 is produced, so this has to happen before the roll.
    public class CastDefenderSpellsDialog : Form
    {
        private readonly Stronghold _stronghold;
        private readonly RaidBattle _battle;
        private readonly List<NPC> _casters;
        private readonly Dictionary<string, ComboBox> _choices = new();

        public List<DefenderSpellCast> Casts { get; } = new();

        public CastDefenderSpellsDialog(Stronghold stronghold, RaidBattle battle)
        {
            _stronghold = stronghold;
            _battle = battle;
            _casters = MagicService.AvailableArcanists(stronghold);

            Text = $"Cast Spells — Round {battle.Round + 1}";
            Size = new Size(620, 200 + Math.Max(1, _casters.Count) * 34);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            BuildLayout();
        }

        private void BuildLayout()
        {
            var intro = new Label
            {
                Text = "Each arcane spellcaster may cast one spell this round. Spell points are spent now.",
                Location = new Point(16, 12),
                Size = new Size(570, 20)
            };

            var grid = new TableLayoutPanel
            {
                Location = new Point(16, 40),
                Size = new Size(570, Math.Max(34, _casters.Count * 34)),
                ColumnCount = 2,
                RowCount = Math.Max(1, _casters.Count),
                AutoScroll = true
            };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));

            for (int i = 0; i < _casters.Count; i++)
            {
                var caster = _casters[i];
                grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));

                grid.Controls.Add(new Label
                {
                    Text = $"{caster.Name} — Arcana {caster.ArcanaLevel} ({caster.SpellPointsRemaining} pts)",
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleLeft,
                    AutoEllipsis = true
                }, 0, i);

                var combo = new ComboBox
                {
                    DropDownStyle = ComboBoxStyle.DropDownList,
                    Anchor = AnchorStyles.Left | AnchorStyles.Right,
                    Margin = new Padding(0, 4, 0, 4)
                };
                combo.Items.Add(new SpellEntry(null));
                foreach (var def in DefenderSpellCatalog.Castable(caster.ArcanaLevel, caster.SpellPointsRemaining))
                    combo.Items.Add(new SpellEntry(def));
                combo.SelectedIndex = 0;

                _choices[caster.Id] = combo;
                grid.Controls.Add(combo, 1, i);
            }

            if (_casters.Count == 0)
            {
                grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
                grid.Controls.Add(new Label
                {
                    Text = "No arcane caster has spell points left.",
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleLeft,
                    ForeColor = Color.Gray
                }, 0, 0);
            }

            var reference = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                BackColor = SystemColors.Control,
                ScrollBars = ScrollBars.Vertical,
                Location = new Point(16, 48 + grid.Height),
                Size = new Size(570, 76),
                Text = string.Join(Environment.NewLine, DefenderSpellCatalog.All.Select(s =>
                    $"{s.Name} — {s.SpellPointCost} pt, Arcana {s.RequiredArcana}: {s.Effect}"))
            };

            var confirm = new Button
            {
                Text = "Begin Round",
                Location = new Point(376, 136 + grid.Height),
                Size = new Size(110, 30)
            };
            confirm.Click += ConfirmClicked;

            var cancel = new Button
            {
                Text = "Cancel",
                Location = new Point(496, 136 + grid.Height),
                Size = new Size(90, 30),
                DialogResult = DialogResult.Cancel
            };

            Controls.Add(intro);
            Controls.Add(grid);
            Controls.Add(reference);
            Controls.Add(confirm);
            Controls.Add(cancel);
            AcceptButton = confirm;
            CancelButton = cancel;
        }

        private void ConfirmClicked(object? sender, EventArgs e)
        {
            foreach (var pair in _choices)
            {
                var def = (pair.Value.SelectedItem as SpellEntry)?.Definition;
                if (def == null) continue;

                var caster = _casters.FirstOrDefault(c => c.Id == pair.Key);
                if (caster == null) continue;

                if (caster.SpellPointsRemaining < def.SpellPointCost)
                {
                    MessageBox.Show($"{caster.Name} no longer has {def.SpellPointCost} spell point(s).",
                        "Cast Spells", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                caster.SpellPointsUsed += def.SpellPointCost;
                Casts.Add(new DefenderSpellCast
                {
                    CasterId = caster.Id,
                    CasterName = caster.Name,
                    Spell = def.Spell
                });
            }

            DialogResult = DialogResult.OK;
            Close();
        }

        private sealed class SpellEntry
        {
            public DefenderSpellDefinition? Definition { get; }

            public SpellEntry(DefenderSpellDefinition? definition) => Definition = definition;

            public override string ToString() => Definition == null
                ? "(no spell)"
                : $"{Definition.Name} — {Definition.SpellPointCost} pt";
        }
    }
}
