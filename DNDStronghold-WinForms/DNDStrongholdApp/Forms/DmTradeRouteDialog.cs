using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using DNDStrongholdApp.Models;
using DNDStrongholdApp.Services;

namespace DNDStrongholdApp.Forms
{
    public class DmTradeRouteDialog : Form
    {
        private readonly TradeRoute _route;
        private readonly Stronghold _stronghold;

        private TextBox _nameBox;
        private ComboBox _typeCombo;
        private NumericUpDown _distance;
        private ComboBox _statusCombo;
        private ComboBox _foundingCombo;
        private TextBox _routeNotes;
        private CheckBox _lockRates;
        private CheckedListBox _specialtyList;
        private CheckedListBox _demandList;
        private DataGridView _ratesGrid;
        private CheckBox _postEvent;
        private ComboBox _eventKind;
        private ComboBox _eventResource;
        private NumericUpDown _eventWeeks;
        private TextBox _eventNotes;

        public TradeMarketEvent? CreatedEvent { get; private set; }

        public DmTradeRouteDialog(TradeRoute route, Stronghold stronghold)
        {
            _route = route;
            _stronghold = stronghold;
            TradeService.EnsureRouteStock(_route);
            InitializeComponent();
            LoadRoute();
        }

        private void InitializeComponent()
        {
            this.Text = $"Edit trade route — {_route.Name}";
            this.Size = new Size(980, 780);
            this.MinimumSize = new Size(860, 680);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.MinimizeBox = false;

            var top = new Panel { Dock = DockStyle.Top, Height = 168, Padding = new Padding(10) };
            _nameBox = new TextBox { Location = new Point(120, 8), Width = 240 };
            _typeCombo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(120, 40), Width = 240 };
            foreach (SettlementType t in Enum.GetValues<SettlementType>())
                _typeCombo.Items.Add(t.ToString());
            _distance = new NumericUpDown { Location = new Point(520, 8), Width = 80, Minimum = 1, Maximum = 20 };
            _statusCombo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(520, 40), Width = 140 };
            foreach (TradeRouteStatus s in Enum.GetValues<TradeRouteStatus>())
                _statusCombo.Items.Add(s.ToString());
            _foundingCombo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(780, 8), Width = 140 };
            foreach (ProjectResultTier t in Enum.GetValues<ProjectResultTier>())
                _foundingCombo.Items.Add(t.ToString());
            _lockRates = new CheckBox
            {
                Text = "Keep these rates as the new normal",
                Location = new Point(670, 42),
                AutoSize = true,
                Checked = true
            };
            _routeNotes = new TextBox { Location = new Point(120, 72), Width = 800, Height = 82, Multiline = true, ScrollBars = ScrollBars.Vertical };
            top.Controls.Add(new Label { Text = "Name:", Location = new Point(10, 12), AutoSize = true });
            top.Controls.Add(_nameBox);
            top.Controls.Add(new Label { Text = "Type:", Location = new Point(10, 44), AutoSize = true });
            top.Controls.Add(_typeCombo);
            top.Controls.Add(new Label { Text = "Distance (weeks):", Location = new Point(380, 12), AutoSize = true });
            top.Controls.Add(_distance);
            top.Controls.Add(new Label { Text = "Status:", Location = new Point(380, 44), AutoSize = true });
            top.Controls.Add(_statusCombo);
            top.Controls.Add(new Label { Text = "Founded:", Location = new Point(670, 12), AutoSize = true });
            top.Controls.Add(_foundingCombo);
            top.Controls.Add(_lockRates);
            top.Controls.Add(new Label { Text = "Route notes:", Location = new Point(10, 76), AutoSize = true });
            top.Controls.Add(_routeNotes);

            var lists = new Panel { Dock = DockStyle.Left, Width = 260, Padding = new Padding(10) };
            _specialtyList = new CheckedListBox { Dock = DockStyle.Top, Height = 180 };
            _demandList = new CheckedListBox { Dock = DockStyle.Fill };
            foreach (ResourceType t in Enum.GetValues<ResourceType>())
            {
                if (t == ResourceType.Gold) continue;
                _specialtyList.Items.Add(t.ToString());
                _demandList.Items.Add(t.ToString());
            }
            lists.Controls.Add(_demandList);
            lists.Controls.Add(new Label { Text = "Current demand (pays well)", Dock = DockStyle.Top, Height = 22 });
            lists.Controls.Add(_specialtyList);
            lists.Controls.Add(new Label { Text = "Specialty (cheap to buy)", Dock = DockStyle.Top, Height = 22 });

            _ratesGrid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                RowHeadersVisible = false
            };
            _ratesGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Resource", HeaderText = "Resource", ReadOnly = true });
            _ratesGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "BuyRate", HeaderText = "Buy rate" });
            _ratesGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "SellRate", HeaderText = "Sell rate" });
            _ratesGrid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "CanBuy", HeaderText = "Can buy" });
            _ratesGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Available", HeaderText = "Available" });
            _ratesGrid.DataError += (_, e) => { e.ThrowException = false; };
            _ratesGrid.CurrentCellDirtyStateChanged += (_, _) =>
            {
                if (_ratesGrid.IsCurrentCellDirty)
                    _ratesGrid.CommitEdit(DataGridViewDataErrorContexts.Commit);
            };
            _ratesGrid.CellValueChanged += RatesGrid_CellValueChanged;

            var eventBox = new GroupBox
            {
                Text = "Market event for this change",
                Dock = DockStyle.Bottom,
                Height = 168,
                Padding = new Padding(10)
            };
            _postEvent = new CheckBox
            {
                Text = "Post a market event (Trade tab + journal)",
                Location = new Point(12, 16),
                AutoSize = true,
                Checked = true
            };
            _eventKind = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(12, 58), Width = 340 };
            _eventKind.Items.Add(new KindItem(TradeMarketEventKind.Notice, "Notice (notes only)"));
            _eventKind.Items.Add(new KindItem(TradeMarketEventKind.DemandSpike, "Demand spike (pays more)"));
            _eventKind.Items.Add(new KindItem(TradeMarketEventKind.Surplus, "Surplus (cheaper to buy)"));
            _eventKind.Items.Add(new KindItem(TradeMarketEventKind.TradeCollapse, "Trade collapse (failure-tier rates)"));
            _eventKind.Items.Add(new KindItem(TradeMarketEventKind.Drought, "Drought (food scarce)"));
            _eventKind.Items.Add(new KindItem(TradeMarketEventKind.Bandits, "Bandits (riskier caravans)"));
            _eventKind.Items.Add(new KindItem(TradeMarketEventKind.GuildFavor, "Guild favor (exceptional rates)"));
            _eventKind.Items.Add(new KindItem(TradeMarketEventKind.Quarantine, "Quarantine (no new caravans)"));
            _eventKind.SelectedIndex = 0;
            _eventKind.SelectedIndexChanged += (_, _) =>
            {
                var kind = SelectedKind();
                if (kind == TradeMarketEventKind.Drought
                    || kind == TradeMarketEventKind.TradeCollapse
                    || kind == TradeMarketEventKind.GuildFavor
                    || kind == TradeMarketEventKind.Quarantine
                    || kind == TradeMarketEventKind.Bandits)
                    _eventWeeks.Value = Math.Max(_eventWeeks.Value, 4);
                UpdateEventFields();
            };
            _eventResource = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(368, 58), Width = 140 };
            foreach (ResourceType t in Enum.GetValues<ResourceType>())
            {
                if (t != ResourceType.Gold)
                    _eventResource.Items.Add(t.ToString());
            }
            if (_eventResource.Items.Count > 0)
                _eventResource.SelectedIndex = 0;
            _eventWeeks = new NumericUpDown { Location = new Point(524, 58), Width = 70, Minimum = 1, Maximum = 52, Value = 3 };
            _eventNotes = new TextBox { Location = new Point(12, 96), Width = 920, Height = 56, Multiline = true, ScrollBars = ScrollBars.Vertical };
            eventBox.Controls.Add(_postEvent);
            eventBox.Controls.Add(new Label { Text = "Kind:", Location = new Point(12, 42), AutoSize = true });
            eventBox.Controls.Add(_eventKind);
            eventBox.Controls.Add(new Label { Text = "Resource:", Location = new Point(368, 42), AutoSize = true });
            eventBox.Controls.Add(_eventResource);
            eventBox.Controls.Add(new Label { Text = "Weeks:", Location = new Point(524, 42), AutoSize = true });
            eventBox.Controls.Add(_eventWeeks);
            eventBox.Controls.Add(new Label { Text = "Notes:", Location = new Point(12, 82), AutoSize = true });
            eventBox.Controls.Add(_eventNotes);
            _postEvent.CheckedChanged += (_, _) => UpdateEventFields();

            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                FlowDirection = FlowDirection.RightToLeft,
                Height = 48,
                Padding = new Padding(8)
            };
            var save = new Button { Text = "Apply", Size = new Size(110, 32) };
            var cancel = new Button { Text = "Cancel", Size = new Size(90, 32), DialogResult = DialogResult.Cancel };
            var suggest = new Button { Text = "Suggest rates", Size = new Size(120, 32) };
            save.Click += Save_Click;
            suggest.Click += (_, _) => ApplySuggestedRates();
            buttons.Controls.Add(save);
            buttons.Controls.Add(cancel);
            buttons.Controls.Add(suggest);

            this.Controls.Add(_ratesGrid);
            this.Controls.Add(lists);
            this.Controls.Add(top);
            this.Controls.Add(eventBox);
            this.Controls.Add(buttons);
            this.AcceptButton = save;
            this.CancelButton = cancel;
            UpdateEventFields();
        }

        private void LoadRoute()
        {
            _nameBox.Text = _route.Name;
            _typeCombo.SelectedItem = _route.SettlementType.ToString();
            _distance.Value = Math.Clamp(_route.DistanceWeeks, 1, 20);
            _statusCombo.SelectedItem = _route.Status.ToString();
            _foundingCombo.SelectedItem = _route.FoundingQuality.ToString();
            _routeNotes.Text = _route.Notes ?? "";
            for (int i = 0; i < _specialtyList.Items.Count; i++)
            {
                var t = Enum.Parse<ResourceType>(_specialtyList.Items[i].ToString()!);
                _specialtyList.SetItemChecked(i, _route.Specialty.Contains(t));
            }
            for (int i = 0; i < _demandList.Items.Count; i++)
            {
                var t = Enum.Parse<ResourceType>(_demandList.Items[i].ToString()!);
                _demandList.SetItemChecked(i, _route.CurrentDemand.Contains(t));
            }

            _ratesGrid.Rows.Clear();
            foreach (var rate in _route.Rates.OrderBy(r => r.ResourceType))
            {
                _ratesGrid.Rows.Add(
                    rate.ResourceType.ToString(),
                    rate.BuyRate.ToString("0.00"),
                    rate.CanBuy || rate.ResourceType == ResourceType.Gold ? rate.SellRate.ToString("0.00") : "—",
                    rate.CanBuy,
                    rate.Available);
                ApplySellRateCell(_ratesGrid.Rows[_ratesGrid.Rows.Count - 1], rate.ResourceType, rate.CanBuy);
            }

        }

        private void UpdateEventFields()
        {
            bool post = _postEvent.Checked;
            var kind = SelectedKind();
            _eventKind.Enabled = post;
            _eventWeeks.Enabled = post;
            _eventNotes.Enabled = post;
            _eventResource.Enabled = post && KindNeedsResource(kind);
        }

        private static bool KindNeedsResource(TradeMarketEventKind kind) =>
            kind == TradeMarketEventKind.DemandSpike || kind == TradeMarketEventKind.Surplus;

        private TradeMarketEventKind SelectedKind()
        {
            return _eventKind.SelectedItem is KindItem item ? item.Kind : TradeMarketEventKind.Notice;
        }

        private void ApplySuggestedRates()
        {
            if (_typeCombo.SelectedItem == null) return;
            var settlement = Enum.Parse<SettlementType>(_typeCombo.SelectedItem.ToString()!);
            var specialties = CheckedTypes(_specialtyList);
            foreach (DataGridViewRow row in _ratesGrid.Rows)
            {
                if (!Enum.TryParse(Convert.ToString(row.Cells["Resource"].Value), out ResourceType type)) continue;
                bool specialty = specialties.Contains(type);
                bool canBuy = type == ResourceType.Gold || specialty || row.Cells["CanBuy"].Value is true;
                if (specialty) canBuy = true;
                row.Cells["CanBuy"].Value = canBuy;
                row.Cells["BuyRate"].Value = TradeService.SuggestedBuyRate(settlement, type, specialty).ToString("0.00");
                row.Cells["Available"].Value = TradeService.DefaultStock(settlement, type, canBuy, specialty).ToString();
                ApplySellRateCell(row, type, canBuy);
                if (type != ResourceType.Gold && canBuy)
                    row.Cells["SellRate"].Value = TradeService.SuggestedSellRate(settlement, type, specialty, true).ToString("0.00");
            }
        }

        private void RatesGrid_CellValueChanged(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || _ratesGrid.Columns[e.ColumnIndex].Name != "CanBuy") return;
            var row = _ratesGrid.Rows[e.RowIndex];
            if (!Enum.TryParse(Convert.ToString(row.Cells["Resource"].Value), out ResourceType type)) return;
            ApplySellRateCell(row, type, row.Cells["CanBuy"].Value is true);
        }

        private void ApplySellRateCell(DataGridViewRow row, ResourceType type, bool canBuy)
        {
            var sell = row.Cells["SellRate"];
            if (type == ResourceType.Gold)
            {
                sell.Value = "1.00";
                sell.ReadOnly = true;
                sell.Style.BackColor = Color.Gainsboro;
                row.Cells["BuyRate"].Value = "1.00";
                row.Cells["BuyRate"].ReadOnly = true;
                row.Cells["BuyRate"].Style.BackColor = Color.Gainsboro;
                row.Cells["CanBuy"].Value = true;
                row.Cells["CanBuy"].ReadOnly = true;
                return;
            }

            if (!canBuy)
            {
                sell.Value = "—";
                sell.ReadOnly = true;
                sell.Style.BackColor = Color.Gainsboro;
            }
            else
            {
                sell.ReadOnly = false;
                sell.Style.BackColor = SystemColors.Window;
                if (Convert.ToString(sell.Value) == "—" || string.IsNullOrWhiteSpace(Convert.ToString(sell.Value)))
                    sell.Value = "1.00";
            }
        }

        private void Save_Click(object? sender, EventArgs e)
        {
            string name = _nameBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                MessageBox.Show("Give the route a name.", "Edit route", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var status = Enum.Parse<TradeRouteStatus>(_statusCombo.SelectedItem?.ToString() ?? "Open");
            if (status == TradeRouteStatus.Closed && _route.IsOccupied)
            {
                MessageBox.Show("A caravan is still on this route. Wait for it to return before closing it.",
                    "Edit route", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            TradeMarketEvent? created = null;
            if (_postEvent.Checked)
            {
                var kind = SelectedKind();
                string notes = _eventNotes.Text.Trim();
                if (kind == TradeMarketEventKind.Notice && string.IsNullOrWhiteSpace(notes))
                {
                    MessageBox.Show("Write a note for the market event, or uncheck Post a market event.",
                        "Edit route", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                ResourceType resource = kind == TradeMarketEventKind.Drought
                    ? ResourceType.Food
                    : ResourceType.Gold;
                if (KindNeedsResource(kind))
                {
                    if (!Enum.TryParse(_eventResource.SelectedItem?.ToString(), out resource))
                    {
                        MessageBox.Show("Pick a resource for this event.", "Edit route",
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }
                }

                created = new TradeMarketEvent
                {
                    RouteId = _route.Id,
                    RouteName = name,
                    Kind = kind,
                    ResourceType = resource,
                    WeeksRemaining = (int)_eventWeeks.Value,
                    Notes = notes
                };
            }

            _route.Name = name;
            _route.SettlementType = Enum.Parse<SettlementType>(_typeCombo.SelectedItem?.ToString() ?? "Town");
            _route.DistanceWeeks = (int)_distance.Value;
            _route.Status = status;
            _route.FoundingQuality = Enum.Parse<ProjectResultTier>(_foundingCombo.SelectedItem?.ToString() ?? "Success");
            _route.Notes = _routeNotes.Text.Trim();
            _route.Specialty = CheckedTypes(_specialtyList);
            _route.CurrentDemand = CheckedTypes(_demandList);

            var catalog = TradeDestinationService.GetInstance().GetById(_route.DestinationId);
            _route.Rates.Clear();
            foreach (DataGridViewRow row in _ratesGrid.Rows)
            {
                if (row.Cells[0].Value == null) continue;
                if (!Enum.TryParse(row.Cells[0].Value.ToString(), out ResourceType type)) continue;
                decimal.TryParse(Convert.ToString(row.Cells["BuyRate"].Value), out decimal buy);
                decimal.TryParse(Convert.ToString(row.Cells["SellRate"].Value), out decimal sell);
                bool canBuy = type == ResourceType.Gold || row.Cells["CanBuy"].Value is true || _route.Specialty.Contains(type);
                int.TryParse(Convert.ToString(row.Cells["Available"].Value), out int available);
                buy = type == ResourceType.Gold ? 1m : (buy <= 0 ? 1m : buy);
                sell = type == ResourceType.Gold ? 1m : (canBuy ? (sell <= 0 ? 1m : sell) : 0m);

                decimal targetBuy = buy;
                decimal targetSell = sell;
                if (!_lockRates.Checked && catalog != null)
                {
                    var destRate = catalog.Resources.Find(r => r.ResourceType == type);
                    if (destRate != null)
                    {
                        if (destRate.BuyRate > 0) targetBuy = destRate.BuyRate;
                        if (canBuy && destRate.SellRate > 0) targetSell = destRate.SellRate;
                    }
                }

                _route.Rates.Add(new TradeResourceRate
                {
                    ResourceType = type,
                    BuyRate = buy,
                    SellRate = sell,
                    TargetBuyRate = type == ResourceType.Gold ? 1m : targetBuy,
                    TargetSellRate = type == ResourceType.Gold ? 1m : (canBuy ? targetSell : 0m),
                    CanBuy = canBuy,
                    Available = Math.Max(0, available)
                });
            }

            foreach (var ev in _stronghold.TradeMarketEvents ?? new List<TradeMarketEvent>())
            {
                if (ev.RouteId == _route.Id)
                    ev.RouteName = _route.Name;
            }

            if (created != null && created.Kind != TradeMarketEventKind.Notice)
            {
                _stronghold.TradeMarketEvents ??= new List<TradeMarketEvent>();
                if (TradeService.IsRouteWideEvent(created.Kind))
                {
                    _stronghold.TradeMarketEvents.RemoveAll(e =>
                        e.RouteId == _route.Id && e.Kind == created.Kind);
                }
                else
                {
                    _stronghold.TradeMarketEvents.RemoveAll(e =>
                        e.RouteId == _route.Id && e.Kind == created.Kind && e.ResourceType == created.ResourceType);
                }
            }

            CreatedEvent = created;
            this.DialogResult = DialogResult.OK;
            Close();
        }

        private static List<ResourceType> CheckedTypes(CheckedListBox list)
        {
            var result = new List<ResourceType>();
            foreach (var item in list.CheckedItems)
            {
                if (Enum.TryParse(item.ToString(), out ResourceType t))
                    result.Add(t);
            }
            return result;
        }

        private sealed class KindItem
        {
            public KindItem(TradeMarketEventKind kind, string label)
            {
                Kind = kind;
                Label = label;
            }

            public TradeMarketEventKind Kind { get; }
            public string Label { get; }
            public override string ToString() => Label;
        }
    }
}
