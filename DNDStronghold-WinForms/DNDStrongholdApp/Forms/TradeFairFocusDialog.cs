using System;
using System.Drawing;
using System.Windows.Forms;
using DNDStrongholdApp.Models;
using DNDStrongholdApp.Services;

namespace DNDStrongholdApp.Forms
{
    public class TradeFairFocusDialog : Form
    {
        private readonly int _officeLevel;
        private ListView _list;

        public ResourceType SelectedResource { get; private set; } = ResourceType.Food;

        public TradeFairFocusDialog(int officeLevel)
        {
            _officeLevel = Math.Max(1, officeLevel);
            InitializeComponent();
            LoadChoices();
        }

        private void InitializeComponent()
        {
            this.Text = "Trade Fair focus";
            this.Size = new Size(420, 360);
            this.MinimumSize = new Size(380, 320);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.MinimizeBox = false;

            var hint = new Label
            {
                Dock = DockStyle.Top,
                Height = 64,
                Padding = new Padding(10, 8, 10, 0),
                Text = "Pick the good this fair is known for. You'll receive a small amount of it when the week ends. Iron from office level 2, Luxury from level 3."
            };

            _list = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                MultiSelect = false
            };
            _list.Columns.Add("Resource", 140);
            _list.Columns.Add("Typical take", 180);
            _list.DoubleClick += (_, _) => TryAccept();

            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                FlowDirection = FlowDirection.RightToLeft,
                Height = 48,
                Padding = new Padding(8)
            };
            var ok = new Button { Text = "Use this focus", Size = new Size(130, 32) };
            var cancel = new Button { Text = "Cancel", Size = new Size(90, 32), DialogResult = DialogResult.Cancel };
            ok.Click += (_, _) => TryAccept();
            buttons.Controls.Add(ok);
            buttons.Controls.Add(cancel);

            this.Controls.Add(_list);
            this.Controls.Add(hint);
            this.Controls.Add(buttons);
            this.AcceptButton = ok;
            this.CancelButton = cancel;
        }

        private void LoadChoices()
        {
            foreach (ResourceType type in Enum.GetValues<ResourceType>())
            {
                if (type == ResourceType.Gold) continue;
                if (!ProjectResolutionService.CanChooseTradeFairFocus(type, _officeLevel)) continue;
                var (min, max) = ProjectResolutionService.TradeFairFocusRange(type);
                if (max <= 0 && min <= 0) continue;
                var item = new ListViewItem(type.ToString());
                item.SubItems.Add($"{min}–{max}");
                item.Tag = type;
                _list.Items.Add(item);
            }
            if (_list.Items.Count > 0)
            {
                _list.Items[0].Selected = true;
                _list.Items[0].Focused = true;
            }
        }

        private void TryAccept()
        {
            if (_list.SelectedItems.Count == 0 || _list.SelectedItems[0].Tag is not ResourceType type)
            {
                MessageBox.Show("Select a resource for the fair.", "Trade Fair",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            SelectedResource = type;
            this.DialogResult = DialogResult.OK;
            Close();
        }
    }
}
