using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using DNDStrongholdApp.Models;
using DNDStrongholdApp.Services;

namespace DNDStrongholdApp.Forms
{
    public class TradeMissionCargoDialog : Form
    {
        private readonly TradeRoute _route;
        private readonly Stronghold _stronghold;
        private readonly List<NPC> _allNpcs;
        private readonly List<Resource> _resources;
        private readonly List<string> _workerIds;
        private DataGridView _grid;
        private Label _valueLabel;
        private Label _expectedLabel;
        private Button _sendButton;
        private bool _clamping;

        public List<ResourceCost> CargoOut { get; private set; } = new();
        public List<ResourceType> RequestedReturns { get; private set; } = new();
        public List<ResourceCost> ExpectedReturn { get; private set; } = new();

        public TradeMissionCargoDialog(
            TradeRoute route,
            Stronghold stronghold,
            List<NPC> allNpcs,
            List<Resource> resources,
            List<string> workerIds)
        {
            _route = route;
            _stronghold = stronghold;
            _allNpcs = allNpcs;
            _resources = resources;
            _workerIds = workerIds ?? new List<string>();
            TradeService.EnsureRouteStock(_route);
            InitializeComponent();
            LoadRows();
            Recalculate();
        }

        private void InitializeComponent()
        {
            this.Text = $"Cargo for {_route.Name}";
            this.Size = new Size(920, 560);
            this.MinimumSize = new Size(780, 440);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.MinimizeBox = false;

            string demand = TradeService.FormatDemand(_route, _stronghold);
            string specialty = _route.Specialty.Count == 0 ? "—" : string.Join(", ", _route.Specialty);
            var header = new Label
            {
                Dock = DockStyle.Top,
                Height = 72,
                Padding = new Padding(12, 8, 12, 4),
                Text =
                    $"{_route.Name}  ·  {_route.SettlementType}  ·  {_route.DistanceWeeks} week trip\n" +
                    $"Demand (pays well for what you send): {demand}\n" +
                    $"Specialty (cheap to buy): {specialty}"
            };

            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                FlowDirection = FlowDirection.RightToLeft,
                Height = 48,
                Padding = new Padding(8)
            };
            _sendButton = new Button { Text = "Send caravan", Size = new Size(130, 32) };
            var cancel = new Button { Text = "Cancel", Size = new Size(90, 32), DialogResult = DialogResult.Cancel };
            _sendButton.Click += Send_Click;
            buttons.Controls.Add(_sendButton);
            buttons.Controls.Add(cancel);

            var summary = new Panel { Dock = DockStyle.Bottom, Height = 52, Padding = new Padding(12, 4, 12, 4) };
            _valueLabel = new Label { Location = new Point(12, 4), Size = new Size(880, 18), Text = "Outbound value: —" };
            _expectedLabel = new Label { Location = new Point(12, 24), Size = new Size(880, 20), Text = "Bring back: —" };
            summary.Controls.Add(_valueLabel);
            summary.Controls.Add(_expectedLabel);

            _grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                RowHeadersVisible = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                SelectionMode = DataGridViewSelectionMode.CellSelect
            };
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Resource", HeaderText = "Resource", ReadOnly = true, FillWeight = 90 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Buy", HeaderText = "Buy rate", ReadOnly = true, FillWeight = 70 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Stock", HeaderText = "On hand", ReadOnly = true, FillWeight = 70 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Send", HeaderText = "Send", FillWeight = 70 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Sell", HeaderText = "Sell rate", ReadOnly = true, FillWeight = 70 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "ForSale", HeaderText = "For sale", ReadOnly = true, FillWeight = 70 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "BringBack", HeaderText = "Bring back", FillWeight = 80 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Notes", HeaderText = "Notes", ReadOnly = true, FillWeight = 110 });
            _grid.DataError += (_, e) => { e.ThrowException = false; };
            _grid.CellValueChanged += Grid_CellValueChanged;
            _grid.CellEndEdit += (_, _) => Recalculate();

            this.Controls.Add(_grid);
            this.Controls.Add(summary);
            this.Controls.Add(header);
            this.Controls.Add(buttons);
            this.AcceptButton = _sendButton;
            this.CancelButton = cancel;
        }

        private void LoadRows()
        {
            var scratch = ScratchProject();
            _grid.Rows.Clear();
            foreach (ResourceType type in Enum.GetValues<ResourceType>())
            {
                int stock = _resources.Find(r => r.Type == type)?.Amount ?? 0;
                int forSale = TradeService.StockFor(_route, type);
                decimal buy = TradeService.EffectiveBuyRate(_route, type, _stronghold, scratch, _allNpcs);
                decimal sell = TradeService.EffectiveSellRate(_route, type, _stronghold);
                bool sells = type == ResourceType.Gold || _route.CanBuy(type);
                var notes = new List<string>();
                if (type != ResourceType.Gold)
                {
                    if (TradeService.HasDemand(_route, type, _stronghold)) notes.Add("Demand");
                    if (_route.Specialty.Contains(type)) notes.Add("Specialty");
                    if (!sells) notes.Add("Not sold here");
                }

                bool gold = type == ResourceType.Gold;
                int idx = _grid.Rows.Add(
                    type.ToString(),
                    gold ? "" : buy.ToString("0.00"),
                    stock.ToString(),
                    "0",
                    gold ? "" : (sells ? sell.ToString("0.00") : "—"),
                    sells ? forSale.ToString() : "—",
                    "0",
                    gold ? "" : (notes.Count == 0 ? "—" : string.Join(", ", notes)));
                _grid.Rows[idx].Tag = type;
                if (gold)
                {
                    foreach (string col in new[] { "Buy", "Sell", "Notes" })
                        _grid.Rows[idx].Cells[col].Style.BackColor = Color.Gainsboro;
                }
                if (stock <= 0)
                {
                    _grid.Rows[idx].Cells["Send"].ReadOnly = true;
                    _grid.Rows[idx].Cells["Send"].Style.BackColor = Color.Gainsboro;
                }
                if (!sells)
                {
                    _grid.Rows[idx].Cells["Sell"].Style.BackColor = Color.Gainsboro;
                    _grid.Rows[idx].Cells["ForSale"].Style.BackColor = Color.Gainsboro;
                    _grid.Rows[idx].Cells["BringBack"].ReadOnly = true;
                    _grid.Rows[idx].Cells["BringBack"].Style.BackColor = Color.Gainsboro;
                }
            }
        }

        private void Grid_CellValueChanged(object? sender, DataGridViewCellEventArgs e)
        {
            if (_clamping || e.RowIndex < 0) return;
            var row = _grid.Rows[e.RowIndex];
            if (row.Tag is not ResourceType type) return;

            string col = _grid.Columns[e.ColumnIndex].Name;
            if (col == "Send")
            {
                int stock = _resources.Find(r => r.Type == type)?.Amount ?? 0;
                ClampCell(row, "Send", 0, stock);
            }
            else if (col == "BringBack")
            {
                int forSale = TradeService.StockFor(_route, type);
                ClampCell(row, "BringBack", 0, forSale);
            }

            Recalculate();
        }

        private void ClampCell(DataGridViewRow row, string column, int min, int max)
        {
            int.TryParse(Convert.ToString(row.Cells[column].Value), out int amount);
            int clamped = Math.Clamp(amount, min, Math.Max(min, max));
            if (amount == clamped) return;
            _clamping = true;
            row.Cells[column].Value = clamped.ToString();
            _clamping = false;
        }

        private Project ScratchProject()
        {
            var cargo = ReadAmounts("Send");
            var bringBack = ReadAmounts("BringBack");
            return new Project
            {
                AssignedWorkers = _workerIds.ToList(),
                CargoOut = cargo,
                ExpectedReturn = bringBack,
                RequestedReturns = bringBack.Select(c => c.ResourceType).ToList()
            };
        }

        private List<ResourceCost> ReadAmounts(string column)
        {
            var list = new List<ResourceCost>();
            if (_grid == null) return list;
            foreach (DataGridViewRow row in _grid.Rows)
            {
                if (row.Tag is not ResourceType type) continue;
                int.TryParse(Convert.ToString(row.Cells[column].Value), out int amount);
                if (amount > 0)
                    list.Add(new ResourceCost { ResourceType = type, Amount = amount });
            }
            return list;
        }

        private void Recalculate()
        {
            if (_expectedLabel == null) return;
            var project = ScratchProject();
            int outbound = TradeService.RoundTotal(TradeService.OutboundValue(_route, _stronghold, project, _allNpcs));
            int returnCost = TradeService.RoundTotal(TradeService.ReturnCost(_route, _stronghold, project.ExpectedReturn));
            int difference = outbound - returnCost;

            _valueLabel.Text = $"Sent value: {outbound}    Purchase value: {returnCost}";
            if (project.CargoOut.Count == 0 || project.ExpectedReturn.Count == 0)
            {
                _expectedLabel.Text = "Send goods and enter what to buy. The two sides must be equal.";
                _sendButton.Enabled = false;
                return;
            }

            _expectedLabel.Text = $"Bring back: {ProjectResolutionService.FormatCosts(project.ExpectedReturn)}";
            bool balanced = difference == 0;
            _sendButton.Enabled = balanced;
            if (!balanced)
            {
                if (difference > 0)
                    _expectedLabel.Text += $"  —  unbalanced by {difference}. Bring back that much gold, or send less.";
                else
                    _expectedLabel.Text += $"  —  unbalanced by {Math.Abs(difference)}. Send that much gold, or buy less.";
            }
        }

        private void Send_Click(object? sender, EventArgs e)
        {
            var cargo = ReadAmounts("Send");
            var bringBack = ReadAmounts("BringBack");
            if (cargo.Count == 0)
            {
                MessageBox.Show("Enter how much of at least one resource to send.", "Cargo",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (bringBack.Count == 0)
            {
                MessageBox.Show("Enter how much of at least one resource to bring back.", "Cargo",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            foreach (var cost in cargo)
            {
                int stock = _resources.Find(r => r.Type == cost.ResourceType)?.Amount ?? 0;
                if (cost.Amount > stock)
                {
                    MessageBox.Show($"You only have {stock} {cost.ResourceType} on hand.", "Cargo",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
            }
            foreach (var cost in bringBack)
            {
                int forSale = TradeService.StockFor(_route, cost.ResourceType);
                if (cost.Amount > forSale)
                {
                    MessageBox.Show($"{_route.Name} only has {forSale} {cost.ResourceType} for sale.", "Cargo",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
            }

            var project = ScratchProject();
            decimal outbound = TradeService.OutboundValue(_route, _stronghold, project, _allNpcs);
            decimal returnCost = TradeService.ReturnCost(_route, _stronghold, bringBack);
            if (!TradeService.TransactionIsBalanced(outbound, returnCost))
            {
                int difference = TradeService.RoundTotal(outbound) - TradeService.RoundTotal(returnCost);
                string hint = difference > 0
                    ? $"Sent goods are worth {difference} more. Bring back that much gold, or send less."
                    : $"The purchase costs {Math.Abs(difference)} more. Send that much gold, or buy less.";
                MessageBox.Show(hint, "Unbalanced transaction", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            CargoOut = cargo;
            RequestedReturns = bringBack.Select(c => c.ResourceType).ToList();
            ExpectedReturn = bringBack;
            this.DialogResult = DialogResult.OK;
            Close();
        }
    }
}
