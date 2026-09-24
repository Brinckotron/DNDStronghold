using System;
using System.Drawing;
using System.Windows.Forms;
using DNDStrongholdApp.Models;

namespace DNDStrongholdApp.Forms
{
    /// <summary>DM tool: record a freeform journal note for the current week.</summary>
    public class CustomJournalEntryDialog : Form
    {
        private readonly TextBox _titleBox;
        private readonly TextBox _bodyBox;
        private readonly ComboBox _importanceBox;

        public string EntryTitle => _titleBox.Text.Trim();
        public string EntryDescription => _bodyBox.Text.Trim();
        public ImportanceLevel EntryImportance =>
            _importanceBox.SelectedItem is ImportanceLevel level
                ? level
                : ImportanceLevel.Medium;

        public CustomJournalEntryDialog()
        {
            Text = "Custom Journal Entry";
            Size = new Size(480, 360);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 6,
                Padding = new Padding(12)
            };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));

            root.Controls.Add(new Label
            {
                Text = "Title:",
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 4)
            }, 0, 0);

            _titleBox = new TextBox
            {
                Dock = DockStyle.Fill,
                MaxLength = 120
            };
            root.Controls.Add(_titleBox, 0, 1);

            root.Controls.Add(new Label
            {
                Text = "Description:",
                AutoSize = true,
                Margin = new Padding(0, 8, 0, 4)
            }, 0, 2);

            _bodyBox = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                AcceptsReturn = true
            };
            root.Controls.Add(_bodyBox, 0, 3);

            var importanceRow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoSize = true,
                Margin = new Padding(0, 8, 0, 0)
            };
            importanceRow.Controls.Add(new Label
            {
                Text = "Importance:",
                AutoSize = true,
                Margin = new Padding(0, 6, 8, 0)
            });
            _importanceBox = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 120
            };
            foreach (ImportanceLevel level in Enum.GetValues(typeof(ImportanceLevel)))
                _importanceBox.Items.Add(level);
            _importanceBox.SelectedItem = ImportanceLevel.Medium;
            importanceRow.Controls.Add(_importanceBox);
            root.Controls.Add(importanceRow, 0, 4);

            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false
            };
            var cancel = new Button
            {
                Text = "Cancel",
                DialogResult = DialogResult.Cancel,
                Width = 90,
                Height = 28
            };
            var ok = new Button
            {
                Text = "Add Entry",
                DialogResult = DialogResult.None,
                Width = 100,
                Height = 28
            };
            ok.Click += (_, _) =>
            {
                if (string.IsNullOrWhiteSpace(_titleBox.Text)
                    && string.IsNullOrWhiteSpace(_bodyBox.Text))
                {
                    MessageBox.Show(this,
                        "Enter a title or description.",
                        "Custom Journal Entry",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    return;
                }
                DialogResult = DialogResult.OK;
                Close();
            };
            buttons.Controls.Add(cancel);
            buttons.Controls.Add(ok);
            root.Controls.Add(buttons, 0, 5);

            Controls.Add(root);
            AcceptButton = ok;
            CancelButton = cancel;
        }
    }
}
