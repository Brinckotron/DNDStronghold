using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using DNDStrongholdApp.Models;
using DNDStrongholdApp.Services;

namespace DNDStrongholdApp.Forms
{
    public class CustomTradeRouteDialog : Form
    {
        private TextBox _nameBox;
        private ComboBox _typeCombo;
        private NumericUpDown _distance;
        private CheckedListBox _specialtyList;
        private CheckedListBox _demandList;
        private DataGridView _ratesGrid;
        private CheckBox _saveToCatalog;
        private TextBox _notesBox;

        public TradeDestination? Destination { get; private set; }
        public bool SaveToCatalog => _saveToCatalog.Checked;

        public CustomTradeRouteDialog()
        {
            InitializeComponent();
            SeedRates();
        }

        private void InitializeComponent()
        {
            this.Text = "Custom Trade Route";
            this.Size = new Size(860, 640);
            this.MinimumSize = new Size(760, 560);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.MinimizeBox = false;

            var top = new Panel { Dock = DockStyle.Top, Height = 120, Padding = new Padding(10) };
            _nameBox = new TextBox { Location = new Point(120, 8), Width = 240 };
            _typeCombo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(120, 40), Width = 240 };
            foreach (SettlementType t in Enum.GetValues<SettlementType>())
                _typeCombo.Items.Add(t.ToString());
            _typeCombo.SelectedIndex = 1;
            _distance = new NumericUpDown { Location = new Point(520, 8), Width = 80, Minimum = 1, Maximum = 20, Value = 2 };
            _notesBox = new TextBox { Location = new Point(120, 72), Width = 480, Height = 36 };
            _saveToCatalog = new CheckBox
            {
                Text = "Also save this destination to the catalog",
                Location = new Point(520, 40),
                AutoSize = true,
                Checked = true
            };
            top.Controls.Add(new Label { Text = "Name:", Location = new Point(10, 12), AutoSize = true });
            top.Controls.Add(_nameBox);
            top.Controls.Add(new Label { Text = "Type:", Location = new Point(10, 44), AutoSize = true });
            top.Controls.Add(_typeCombo);
            top.Controls.Add(new Label { Text = "Distance (weeks):", Location = new Point(380, 12), AutoSize = true });
            top.Controls.Add(_distance);
            top.Controls.Add(new Label { Text = "Notes:", Location = new Point(10, 76), AutoSize = true });
            top.Controls.Add(_notesBox);
            top.Controls.Add(_saveToCatalog);

            var lists = new Panel { Dock = DockStyle.Left, Width = 260, Padding = new Padding(10) };
            _specialtyList = new CheckedListBox { Dock = DockStyle.Top, Height = 180 };
            _demandList = new CheckedListBox { Dock = DockStyle.Fill };
            foreach (ResourceType t in Enum.GetValues<ResourceType>())
            {
                if (t == ResourceType.Gold) continue;
                _specialtyList.Items.Add(t.ToString());
                _demandList.Items.Add(t.ToString());
            }
            _specialtyList.ItemCheck += (_, _) => BeginInvoke(RefreshSuggestedRates);
            _typeCombo.SelectedIndexChanged += (_, _) => RefreshSuggestedRates();
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

            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                FlowDirection = FlowDirection.RightToLeft,
                Height = 48,
                Padding = new Padding(8)
            };
            var ok = new Button { Text = "Use this route", Size = new Size(140, 32) };
            var cancel = new Button { Text = "Cancel", Size = new Size(90, 32), DialogResult = DialogResult.Cancel };
            ok.Click += Ok_Click;
            buttons.Controls.Add(ok);
            buttons.Controls.Add(cancel);

            this.Controls.Add(_ratesGrid);
            this.Controls.Add(lists);
            this.Controls.Add(top);
            this.Controls.Add(buttons);
            this.AcceptButton = ok;
            this.CancelButton = cancel;
        }

        private void SeedRates()
        {
            _ratesGrid.Rows.Clear();
            foreach (ResourceType type in Enum.GetValues<ResourceType>())
            {
                _ratesGrid.Rows.Add(type.ToString(), "1.00", type == ResourceType.Gold ? "1.00" : "—", type == ResourceType.Gold, "0");
                ApplySellRateCell(_ratesGrid.Rows[_ratesGrid.Rows.Count - 1], type, type == ResourceType.Gold);
            }
            RefreshSuggestedRates();
        }

        private void RefreshSuggestedRates()
        {
            if (_ratesGrid == null || _typeCombo.SelectedItem == null) return;
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
                int stock = TradeService.DefaultStock(settlement, type, canBuy, specialty);
                row.Cells["Available"].Value = stock.ToString();
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

        private static void ApplySellRateCell(DataGridViewRow row, ResourceType type, bool canBuy)
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

        private void Ok_Click(object? sender, EventArgs e)
        {
            string name = _nameBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                MessageBox.Show("Give the destination a name.", "Custom route", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var dest = new TradeDestination
            {
                Id = Guid.NewGuid().ToString("N")[..8],
                Name = name,
                SettlementType = Enum.Parse<SettlementType>(_typeCombo.SelectedItem?.ToString() ?? "Town"),
                DistanceWeeks = (int)_distance.Value,
                MinReputation = 0,
                Notes = _notesBox.Text.Trim(),
                Specialty = CheckedTypes(_specialtyList),
                DefaultDemand = CheckedTypes(_demandList)
            };

            foreach (DataGridViewRow row in _ratesGrid.Rows)
            {
                if (row.Cells[0].Value == null) continue;
                if (!Enum.TryParse(row.Cells[0].Value.ToString(), out ResourceType type)) continue;
                decimal.TryParse(Convert.ToString(row.Cells["BuyRate"].Value), out decimal buy);
                decimal.TryParse(Convert.ToString(row.Cells["SellRate"].Value), out decimal sell);
                bool canBuy = row.Cells["CanBuy"].Value is true;
                int.TryParse(Convert.ToString(row.Cells["Available"].Value), out int available);
                dest.Resources.Add(new TradeResourceRate
                {
                    ResourceType = type,
                    BuyRate = type == ResourceType.Gold ? 1m : (buy <= 0 ? 1m : buy),
                    SellRate = type == ResourceType.Gold ? 1m : (canBuy ? (sell <= 0 ? 1m : sell) : 0m),
                    CanBuy = type == ResourceType.Gold || canBuy || dest.Specialty.Contains(type),
                    Available = Math.Max(0, available)
                });
            }
            TradeDestinationService.EnsureDefaultRates(dest);

            Destination = dest;
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
    }
}
