using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using DNDStrongholdApp.Models;
using DNDStrongholdApp.Services;

namespace DNDStrongholdApp.Forms
{
    public class TradeDataEditor : Form
    {
        private List<TradeDestination> _destinations = new();
        private TradeDestination? _current;
        private ComboBox _destinationCombo;
        private TextBox _nameBox;
        private ComboBox _typeCombo;
        private NumericUpDown _distance;
        private NumericUpDown _minRep;
        private TextBox _notesBox;
        private CheckedListBox _specialtyList;
        private CheckedListBox _demandList;
        private DataGridView _ratesGrid;
        private readonly TradeDestinationService _service = TradeDestinationService.GetInstance();

        public TradeDataEditor()
        {
            _destinations = _service.GetDestinations().Select(Clone).ToList();
            InitializeUi();
            LoadCombo();
        }

        private static TradeDestination Clone(TradeDestination d)
        {
            TradeDestinationService.EnsureDefaultRates(d);
            return new TradeDestination
            {
                Id = d.Id,
                Name = d.Name,
                SettlementType = d.SettlementType,
                DistanceWeeks = d.DistanceWeeks,
                MinReputation = d.MinReputation,
                Specialty = d.Specialty?.ToList() ?? new List<ResourceType>(),
                DefaultDemand = d.DefaultDemand?.ToList() ?? new List<ResourceType>(),
                Notes = d.Notes,
                Resources = d.Resources.Select(r => new TradeResourceRate
                {
                    ResourceType = r.ResourceType,
                    BuyRate = r.BuyRate,
                    SellRate = r.SellRate,
                    CanBuy = r.CanBuy,
                    Available = r.Available
                }).ToList()
            };
        }

        private void InitializeUi()
        {
            this.Text = "Trade Data Editor";
            this.Size = new Size(980, 720);
            this.StartPosition = FormStartPosition.CenterParent;

            var top = new Panel { Dock = DockStyle.Top, Height = 40, Padding = new Padding(10, 8, 10, 0) };
            _destinationCombo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 280, Location = new Point(120, 4) };
            _destinationCombo.SelectedIndexChanged += (_, _) => LoadSelected();
            top.Controls.Add(new Label { Text = "Destination:", Location = new Point(0, 8), AutoSize = true });
            top.Controls.Add(_destinationCombo);
            var addBtn = new Button { Text = "Add", Location = new Point(420, 3), Size = new Size(80, 28) };
            var deleteBtn = new Button { Text = "Delete", Location = new Point(510, 3), Size = new Size(80, 28) };
            addBtn.Click += AddDestination;
            deleteBtn.Click += DeleteDestination;
            top.Controls.Add(addBtn);
            top.Controls.Add(deleteBtn);

            var props = new Panel { Dock = DockStyle.Top, Height = 160, Padding = new Padding(10) };
            _nameBox = new TextBox { Location = new Point(120, 8), Width = 220 };
            _nameBox.Leave += (_, _) =>
            {
                if (_current == null) return;
                _current.Name = _nameBox.Text.Trim();
                SyncComboItemName();
            };
            _typeCombo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(120, 40), Width = 220 };
            foreach (SettlementType t in Enum.GetValues<SettlementType>())
                _typeCombo.Items.Add(t.ToString());
            _distance = new NumericUpDown { Location = new Point(480, 8), Width = 80, Minimum = 1, Maximum = 20 };
            _minRep = new NumericUpDown { Location = new Point(480, 40), Width = 80, Minimum = 0, Maximum = 100 };
            _notesBox = new TextBox { Location = new Point(120, 72), Width = 440, Height = 70, Multiline = true, ScrollBars = ScrollBars.Vertical };
            props.Controls.Add(new Label { Text = "Name:", Location = new Point(10, 12), AutoSize = true });
            props.Controls.Add(_nameBox);
            props.Controls.Add(new Label { Text = "Type:", Location = new Point(10, 44), AutoSize = true });
            props.Controls.Add(_typeCombo);
            props.Controls.Add(new Label { Text = "Distance (weeks):", Location = new Point(340, 12), AutoSize = true });
            props.Controls.Add(_distance);
            props.Controls.Add(new Label { Text = "Min Reputation:", Location = new Point(340, 44), AutoSize = true });
            props.Controls.Add(_minRep);
            props.Controls.Add(new Label { Text = "Notes:", Location = new Point(10, 76), AutoSize = true });
            props.Controls.Add(_notesBox);

            var lists = new Panel { Dock = DockStyle.Left, Width = 280, Padding = new Padding(10) };
            _specialtyList = new CheckedListBox { Dock = DockStyle.Top, Height = 160 };
            _demandList = new CheckedListBox { Dock = DockStyle.Fill };
            foreach (ResourceType t in Enum.GetValues<ResourceType>())
            {
                if (t == ResourceType.Gold) continue;
                _specialtyList.Items.Add(t.ToString());
                _demandList.Items.Add(t.ToString());
            }
            lists.Controls.Add(_demandList);
            lists.Controls.Add(new Label { Text = "Default demand", Dock = DockStyle.Top, Height = 20 });
            lists.Controls.Add(_specialtyList);
            lists.Controls.Add(new Label { Text = "Specialty (can buy cheap)", Dock = DockStyle.Top, Height = 20 });

            _ratesGrid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
            };
            _ratesGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Resource", HeaderText = "Resource", ReadOnly = true });
            _ratesGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "BuyRate", HeaderText = "Buy rate" });
            _ratesGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "SellRate", HeaderText = "Sell rate" });
            _ratesGrid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "CanBuy", HeaderText = "Can buy" });
            _ratesGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Available", HeaderText = "Available" });
            _ratesGrid.CurrentCellDirtyStateChanged += (_, _) =>
            {
                if (_ratesGrid.IsCurrentCellDirty)
                    _ratesGrid.CommitEdit(DataGridViewDataErrorContexts.Commit);
            };
            _ratesGrid.CellValueChanged += RatesGrid_CellValueChanged;

            var save = new Button { Text = "Save Changes", Dock = DockStyle.Bottom, Height = 36 };
            save.Click += Save;
            var suggest = new Button { Text = "Suggest rates from type & specialty", Dock = DockStyle.Bottom, Height = 32 };
            suggest.Click += (_, _) => ApplySuggestedRates();

            this.Controls.Add(_ratesGrid);
            this.Controls.Add(lists);
            this.Controls.Add(props);
            this.Controls.Add(top);
            this.Controls.Add(suggest);
            this.Controls.Add(save);
        }

        private void LoadCombo()
        {
            _destinationCombo.Items.Clear();
            foreach (var d in _destinations)
                _destinationCombo.Items.Add(d.Name);
            if (_destinationCombo.Items.Count > 0)
                _destinationCombo.SelectedIndex = 0;
        }

        private void LoadSelected()
        {
            CommitCurrent();
            if (_destinationCombo.SelectedIndex < 0 || _destinationCombo.SelectedIndex >= _destinations.Count)
            {
                _current = null;
                return;
            }
            _current = _destinations[_destinationCombo.SelectedIndex];
            TradeDestinationService.EnsureDefaultRates(_current);
            _nameBox.Text = _current.Name;
            _typeCombo.SelectedItem = _current.SettlementType.ToString();
            _distance.Value = Math.Clamp(_current.DistanceWeeks, 1, 20);
            _minRep.Value = Math.Clamp(_current.MinReputation, 0, 100);
            _notesBox.Text = _current.Notes;
            for (int i = 0; i < _specialtyList.Items.Count; i++)
            {
                var t = Enum.Parse<ResourceType>(_specialtyList.Items[i].ToString()!);
                _specialtyList.SetItemChecked(i, _current.Specialty.Contains(t));
            }
            for (int i = 0; i < _demandList.Items.Count; i++)
            {
                var t = Enum.Parse<ResourceType>(_demandList.Items[i].ToString()!);
                _demandList.SetItemChecked(i, _current.DefaultDemand.Contains(t));
            }
            _ratesGrid.Rows.Clear();
            foreach (var rate in _current.Resources.OrderBy(r => r.ResourceType))
            {
                _ratesGrid.Rows.Add(rate.ResourceType.ToString(), rate.BuyRate.ToString("0.00"), rate.SellRate.ToString("0.00"), rate.CanBuy, rate.Available);
                ApplySellRateCell(_ratesGrid.Rows[_ratesGrid.Rows.Count - 1], rate.ResourceType, rate.CanBuy);
            }
        }

        private SettlementType CurrentSettlement()
        {
            if (_typeCombo.SelectedItem != null &&
                Enum.TryParse(_typeCombo.SelectedItem.ToString(), out SettlementType settlement))
                return settlement;
            return _current?.SettlementType ?? SettlementType.Town;
        }

        private void ApplySuggestedRates()
        {
            if (_ratesGrid.Rows.Count == 0) return;
            var settlement = CurrentSettlement();
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
            bool canBuy = row.Cells["CanBuy"].Value is true;
            ApplySellRateCell(row, type, canBuy);
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
                {
                    var settlement = CurrentSettlement();
                    bool specialty = false;
                    if (Enum.TryParse(Convert.ToString(row.Cells["Resource"].Value), out ResourceType rowType))
                        specialty = CheckedTypes(_specialtyList).Contains(rowType);
                    sell.Value = TradeService.SuggestedSellRate(settlement, type, specialty, true).ToString("0.00");
                }
            }
        }

        private void CommitCurrent()
        {
            if (_current == null) return;
            _current.Name = _nameBox.Text.Trim();
            if (_typeCombo.SelectedItem != null)
                _current.SettlementType = Enum.Parse<SettlementType>(_typeCombo.SelectedItem.ToString()!);
            _current.DistanceWeeks = (int)_distance.Value;
            _current.MinReputation = (int)_minRep.Value;
            _current.Notes = _notesBox.Text.Trim();
            _current.Specialty = CheckedTypes(_specialtyList);
            _current.DefaultDemand = CheckedTypes(_demandList);
            _current.Resources.Clear();
            foreach (DataGridViewRow row in _ratesGrid.Rows)
            {
                if (row.Cells[0].Value == null) continue;
                if (!Enum.TryParse(row.Cells[0].Value.ToString(), out ResourceType type)) continue;
                decimal.TryParse(Convert.ToString(row.Cells["BuyRate"].Value), out decimal buy);
                decimal.TryParse(Convert.ToString(row.Cells["SellRate"].Value), out decimal sell);
                bool canBuy = row.Cells["CanBuy"].Value is true;
                int.TryParse(Convert.ToString(row.Cells["Available"].Value), out int available);
                _current.Resources.Add(new TradeResourceRate
                {
                    ResourceType = type,
                    BuyRate = type == ResourceType.Gold ? 1m : (buy <= 0 ? 1m : buy),
                    SellRate = type == ResourceType.Gold ? 1m : (canBuy ? (sell <= 0 ? 1m : sell) : 0m),
                    CanBuy = type == ResourceType.Gold || canBuy,
                    Available = Math.Max(0, available)
                });
            }
            SyncComboItemName();
        }

        private void SyncComboItemName()
        {
            if (_current == null) return;
            int index = _destinations.IndexOf(_current);
            if (index < 0 || index >= _destinationCombo.Items.Count) return;
            string name = string.IsNullOrWhiteSpace(_current.Name) ? "New Destination" : _current.Name;
            if (Convert.ToString(_destinationCombo.Items[index]) != name)
                _destinationCombo.Items[index] = name;
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

        private void AddDestination(object? sender, EventArgs e)
        {
            CommitCurrent();
            var dest = new TradeDestination
            {
                Id = Guid.NewGuid().ToString("N")[..8],
                Name = "New Destination",
                DistanceWeeks = 2,
                MinReputation = 0
            };
            TradeDestinationService.EnsureDefaultRates(dest);
            _destinations.Add(dest);
            _destinationCombo.Items.Add(dest.Name);
            _destinationCombo.SelectedIndex = _destinations.Count - 1;
        }

        private void DeleteDestination(object? sender, EventArgs e)
        {
            if (_current == null) return;
            if (MessageBox.Show($"Delete {_current.Name}?", "Delete", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;
            _destinations.Remove(_current);
            _current = null;
            _service.Save(_destinations);
            LoadCombo();
            if (_destinationCombo.Items.Count == 0)
            {
                _nameBox.Text = "";
                _notesBox.Text = "";
                _ratesGrid.Rows.Clear();
            }
        }

        private void Save(object? sender, EventArgs e)
        {
            CommitCurrent();
            _service.Save(_destinations);
            MessageBox.Show("Trade destinations saved.", "Saved", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}
