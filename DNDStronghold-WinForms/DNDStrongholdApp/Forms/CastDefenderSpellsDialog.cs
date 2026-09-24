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
        private TextBox _description = null!;

        public List<DefenderSpellCast> Casts { get; } = new();

        public CastDefenderSpellsDialog(Stronghold stronghold, RaidBattle battle)
        {
            _stronghold = stronghold;
            _battle = battle;
            _casters = MagicService.AvailableArcanists(stronghold);

            Text = $"Cast Spells — Round {battle.Round + 1}";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            MinimumSize = new Size(640, 360);
            ClientSize = new Size(620, Math.Max(340, 160 + Math.Max(1, _casters.Count) * 36));

            BuildLayout();
            RefreshDescription();
        }

        private void BuildLayout()
        {
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                Padding = new Padding(12)
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 28F));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 72F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));

            var intro = new Label
            {
                Text = "Each arcane spellcaster may cast one spell this round. Spell points are spent now.",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft
            };

            var grid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = Math.Max(1, _casters.Count),
                AutoScroll = true
            };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45F));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55F));

            for (int i = 0; i < _casters.Count; i++)
            {
                var caster = _casters[i];
                grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));

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
                    Dock = DockStyle.Fill,
                    Margin = new Padding(0, 4, 0, 4)
                };
                combo.Items.Add(new SpellEntry(null));
                foreach (var def in DefenderSpellCatalog.Castable(caster.ArcanaLevel, caster.SpellPointsRemaining))
                    combo.Items.Add(new SpellEntry(def));
                combo.SelectedIndex = 0;
                combo.SelectedIndexChanged += (_, _) => RefreshDescription(combo);

                _choices[caster.Id] = combo;
                grid.Controls.Add(combo, 1, i);
            }

            if (_casters.Count == 0)
            {
                grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
                grid.Controls.Add(new Label
                {
                    Text = "No arcane caster has spell points left.",
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleLeft,
                    ForeColor = Color.Gray
                }, 0, 0);
            }

            _description = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                BackColor = SystemColors.Control,
                ScrollBars = ScrollBars.Vertical,
                Dock = DockStyle.Fill,
                Text = "Select a spell to see its effect."
            };

            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                Padding = new Padding(0, 4, 0, 0)
            };
            var confirm = new Button
            {
                Text = "Begin Round",
                Size = new Size(110, 30),
                Margin = new Padding(6, 0, 0, 0)
            };
            confirm.Click += ConfirmClicked;
            var cancel = new Button
            {
                Text = "Cancel",
                Size = new Size(90, 30),
                DialogResult = DialogResult.Cancel,
                Margin = new Padding(0)
            };
            buttons.Controls.Add(cancel);
            buttons.Controls.Add(confirm);

            root.Controls.Add(intro, 0, 0);
            root.Controls.Add(grid, 0, 1);
            root.Controls.Add(_description, 0, 2);
            root.Controls.Add(buttons, 0, 3);
            Controls.Add(root);
            AcceptButton = confirm;
            CancelButton = cancel;
        }

        private void RefreshDescription(ComboBox? source = null)
        {
            DefenderSpellDefinition? selected = null;
            if (source?.SelectedItem is SpellEntry fromSource)
                selected = fromSource.Definition;

            if (selected == null)
            {
                selected = _choices.Values
                    .Select(c => (c.SelectedItem as SpellEntry)?.Definition)
                    .FirstOrDefault(d => d != null);
            }

            _description.Text = selected == null
                ? "Select a spell to see its effect."
                : $"{selected.Name} — {selected.SpellPointCost} pt, Arcana {selected.RequiredArcana}\r\n{selected.Effect}";
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
