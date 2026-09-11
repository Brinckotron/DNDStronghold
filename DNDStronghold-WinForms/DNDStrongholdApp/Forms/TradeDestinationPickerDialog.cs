using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using DNDStrongholdApp.Models;
using DNDStrongholdApp.Services;

namespace DNDStrongholdApp.Forms
{
    public class TradeDestinationPickerDialog : Form
    {
        private readonly Stronghold _stronghold;
        private ListView _list;
        private Button _useButton;

        public TradeDestination? SelectedDestination { get; private set; }
        public bool IsCustom { get; private set; }

        public TradeDestinationPickerDialog(Stronghold stronghold)
        {
            _stronghold = stronghold;
            InitializeComponent();
            LoadDestinations();
        }

        private void InitializeComponent()
        {
            this.Text = "Choose a trade destination";
            this.Size = new Size(640, 420);
            this.MinimumSize = new Size(560, 360);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.MinimizeBox = false;

            var hint = new Label
            {
                Dock = DockStyle.Top,
                Height = 40,
                Padding = new Padding(10, 8, 10, 0),
                Text = "Pick a catalog destination, or create a custom route with its own rates, demand, and specialty."
            };

            _list = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                MultiSelect = false
            };
            _list.Columns.Add("Destination", 160);
            _list.Columns.Add("Type", 80);
            _list.Columns.Add("Weeks", 70);
            _list.Columns.Add("Status", 220);
            _list.DoubleClick += (_, _) => TryAcceptSelected();

            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                FlowDirection = FlowDirection.RightToLeft,
                Height = 48,
                Padding = new Padding(8)
            };
            _useButton = new Button { Text = "Use selected", Size = new Size(120, 32) };
            var custom = new Button { Text = "Custom route...", Size = new Size(130, 32) };
            var cancel = new Button { Text = "Cancel", Size = new Size(90, 32), DialogResult = DialogResult.Cancel };
            _useButton.Click += (_, _) => TryAcceptSelected();
            custom.Click += Custom_Click;
            buttons.Controls.Add(_useButton);
            buttons.Controls.Add(custom);
            buttons.Controls.Add(cancel);

            this.Controls.Add(_list);
            this.Controls.Add(hint);
            this.Controls.Add(buttons);
            this.AcceptButton = _useButton;
            this.CancelButton = cancel;
        }

        private void LoadDestinations()
        {
            _list.Items.Clear();
            foreach (var dest in TradeDestinationService.GetInstance().GetDestinations())
            {
                bool open = TradeService.HasOpenRouteTo(_stronghold, dest.Id);
                bool pending = TradeService.HasPendingEstablish(_stronghold, dest.Id);
                bool locked = _stronghold.Reputation < dest.MinReputation;
                string status = open
                    ? "Already established"
                    : pending
                        ? "Being established"
                    : locked
                        ? $"Need Reputation {dest.MinReputation}"
                        : "Available";
                var item = new ListViewItem(dest.Name);
                item.SubItems.Add(dest.SettlementType.ToString());
                item.SubItems.Add(dest.DistanceWeeks.ToString());
                item.SubItems.Add(status);
                item.Tag = dest;
                if (open || pending || locked)
                    item.ForeColor = Color.Gray;
                _list.Items.Add(item);
            }

            if (_list.Items.Count > 0)
            {
                var firstAvailable = _list.Items.Cast<ListViewItem>().FirstOrDefault(i => i.ForeColor != Color.Gray);
                (firstAvailable ?? _list.Items[0]).Selected = true;
            }
        }

        private void TryAcceptSelected()
        {
            if (_list.SelectedItems.Count == 0)
            {
                MessageBox.Show("Select a destination, or click Custom route.", "Destination",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var dest = _list.SelectedItems[0].Tag as TradeDestination;
            if (dest == null) return;

            if (TradeService.HasOpenRouteTo(_stronghold, dest.Id))
            {
                MessageBox.Show($"A route to {dest.Name} is already open.", "Destination",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (TradeService.HasPendingEstablish(_stronghold, dest.Id))
            {
                MessageBox.Show($"Another Trade Office is already establishing a route to {dest.Name}.",
                    "Destination", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (_stronghold.Reputation < dest.MinReputation)
            {
                MessageBox.Show($"Reputation {dest.MinReputation} is required for {dest.Name}.", "Destination",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            SelectedDestination = dest;
            IsCustom = false;
            this.DialogResult = DialogResult.OK;
            Close();
        }

        private void Custom_Click(object? sender, EventArgs e)
        {
            using var custom = new CustomTradeRouteDialog();
            if (custom.ShowDialog(this) != DialogResult.OK || custom.Destination == null)
                return;

            if (custom.SaveToCatalog)
                TradeDestinationService.GetInstance().AddOrUpdate(custom.Destination);

            SelectedDestination = custom.Destination;
            IsCustom = true;
            this.DialogResult = DialogResult.OK;
            Close();
        }
    }
}
