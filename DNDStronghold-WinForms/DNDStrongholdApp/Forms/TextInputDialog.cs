using System;
using System.Windows.Forms;
using System.Drawing;

namespace DNDStrongholdApp.Forms
{
    public class TextInputDialog : Form
    {
        private TextBox _inputTextBox;
        public string InputText => _inputTextBox.Text;

        public TextInputDialog(string title, string prompt, string defaultText = "")
        {
            InitializeComponents(title, prompt, defaultText);
        }

        private void InitializeComponents(string title, string prompt, string defaultText)
        {
            this.Text = title;
            this.Size = new Size(400, 150);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterParent;

            TableLayoutPanel layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Padding = new Padding(10)
            };

            Label promptLabel = new Label
            {
                Text = prompt,
                Dock = DockStyle.Fill,
                AutoSize = true
            };

            _inputTextBox = new TextBox
            {
                Text = defaultText,
                Dock = DockStyle.Fill,
                Width = 350
            };

            FlowLayoutPanel buttonPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft
            };

            Button okButton = new Button
            {
                Text = "OK",
                DialogResult = DialogResult.OK,
                Width = 75
            };

            Button cancelButton = new Button
            {
                Text = "Cancel",
                DialogResult = DialogResult.Cancel,
                Width = 75
            };

            buttonPanel.Controls.Add(cancelButton);
            buttonPanel.Controls.Add(okButton);

            layout.Controls.Add(promptLabel, 0, 0);
            layout.Controls.Add(_inputTextBox, 0, 1);
            layout.Controls.Add(buttonPanel, 0, 2);

            this.Controls.Add(layout);
            this.AcceptButton = okButton;
            this.CancelButton = cancelButton;
        }
    }
} 