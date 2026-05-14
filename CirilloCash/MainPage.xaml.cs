using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using CirilloCash.Services;

namespace CirilloCash
{
    public partial class MainPage : ContentPage
    {
        private readonly ThermalPrinterService thermalPrinterService = new();
        private readonly EthernetPrinterService ethernetPrinterService = new();

        private readonly ObservableCollection<OrderItemRow> orderRows = new();
        private readonly ObservableCollection<BillItemRow> drinkBillRows = new();
        private readonly ObservableCollection<BillItemRow> foodBillRows = new();
        private readonly Dictionary<string, OrderItemRow> rowsById = new();

        private MenuCategory currentCategory = MenuCategory.Drink;
        private double totalBill;
        private double totalMoney;
        private bool transactionAlreadySaved;

        public MainPage()
        {
            InitializeComponent();
            DrinkBillView.ItemsSource = drinkBillRows;
            FoodBillView.ItemsSource = foodBillRows;
            OrderItemsView.ItemsSource = orderRows;
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            ReloadCatalog();
            UpdateTabHighlight();
            UpdateBill();
        }

        private void ReloadCatalog()
        {
            var existingQuantities = rowsById.Values.ToDictionary(r => r.Id, r => r.Quantity);

            orderRows.Clear();
            rowsById.Clear();

            var items = CatalogService.Instance.GetByCategory(currentCategory);
            foreach (var item in items)
            {
                existingQuantities.TryGetValue(item.Id, out var qty);
                var row = new OrderItemRow(item) { Quantity = qty };
                orderRows.Add(row);
                rowsById[row.Id] = row;
            }

            foreach (var billRow in drinkBillRows.Concat(foodBillRows))
            {
                if (!rowsById.ContainsKey(billRow.Id))
                {
                    rowsById[billRow.Id] = new OrderItemRow(billRow.Snapshot()) { Quantity = billRow.Quantity };
                }
            }
        }

        private void OnDrinksTabClicked(object sender, EventArgs e)
        {
            currentCategory = MenuCategory.Drink;
            ReloadCatalog();
            UpdateTabHighlight();
        }

        private void OnFoodTabClicked(object sender, EventArgs e)
        {
            currentCategory = MenuCategory.Food;
            ReloadCatalog();
            UpdateTabHighlight();
        }

        private void UpdateTabHighlight()
        {
            DrinksTabBtn.FontAttributes = currentCategory == MenuCategory.Drink ? FontAttributes.Bold : FontAttributes.None;
            FoodTabBtn.FontAttributes   = currentCategory == MenuCategory.Food  ? FontAttributes.Bold : FontAttributes.None;
        }

        private void OnAddItemClicked(object sender, EventArgs e)
        {
            if (sender is Button btn && btn.CommandParameter is string id && rowsById.TryGetValue(id, out var row))
            {
                row.Quantity++;
                UpdateBill();
            }
        }

        private void OnRemoveItemClicked(object sender, EventArgs e)
        {
            if (sender is Button btn && btn.CommandParameter is string id && rowsById.TryGetValue(id, out var row) && row.Quantity > 0)
            {
                row.Quantity--;
                UpdateBill();
            }
        }

        private void OnCleanClicked(object sender, EventArgs e) => CleanBill();

        private async void OnSaveClicked(object sender, EventArgs e)
        {
            if (!await TrySaveCurrentTransactionAsync("Salvataggio", "Nessuna transazione da salvare."))
            {
                return;
            }

            CleanBill();
        }

        private async void OnPrintClicked(object sender, EventArgs e)
        {
            if (!HasPendingTransaction())
            {
                await DisplayAlert("Stampa", "Nessun conto da stampare.", "OK");
                return;
            }

            var activePrinter = PrinterSettings.ActivePrinter;

            if (activePrinter == ActivePrinter.X5 && !await EnsureBluetoothPermissionAsync())
            {
                await DisplayAlert("Stampa", "Permesso Bluetooth negato. Abilita 'Dispositivi nelle vicinanze'.", "OK");
                return;
            }

            if (!await TrySaveCurrentTransactionAsync("Stampa", "Nessun conto da stampare."))
            {
                return;
            }

            PrinterResult printResult;
            if (activePrinter == ActivePrinter.Ethernet)
            {
                printResult = await PrintEthernetReceiptsAsync();
            }
            else
            {
                printResult = await PrintBluetoothReceiptsAsync();
            }

            if (printResult.Success)
            {
                CleanBill();
            }
            else
            {
                printResult = printResult with
                {
                    Message = $"Transazione salvata, ma la stampa non e riuscita: {printResult.Message}"
                };
            }

            await DisplayAlert("Stampa", printResult.Message, "OK");
        }

        private async Task<PrinterResult> PrintBluetoothReceiptsAsync()
        {
            var timestamp = DateTime.Now;
            var docs = new List<ReceiptDocument>();
            var labels = new List<string>();

            if (drinkBillRows.Count > 0)
            {
                docs.Add(BuildReceiptDocument(drinkBillRows, "DRINK", timestamp));
                labels.Add("DRINK");
            }

            if (foodBillRows.Count > 0)
            {
                docs.Add(BuildReceiptDocument(foodBillRows, "FOOD", timestamp));
                labels.Add("FOOD");
            }

            var result = await thermalPrinterService.PrintReceiptsAsync(
                docs,
                PrinterSettings.PrinterNameHint,
                PrinterSettings.PrinterMacAddress);

            if (result.Success)
            {
                return PrinterResult.Ok($"Scontrini inviati: {string.Join(" + ", labels)}.");
            }

            return result;
        }

        private async Task<PrinterResult> PrintEthernetReceiptsAsync()
        {
            var host = PrinterSettings.EthernetHost;
            var port = PrinterSettings.EthernetPort;
            var timestamp = DateTime.Now;
            var docs = new List<ReceiptDocument>();
            var labels = new List<string>();

            if (drinkBillRows.Count > 0)
            {
                docs.Add(BuildReceiptDocument(drinkBillRows, "DRINK", timestamp));
                labels.Add("DRINK");
            }

            if (foodBillRows.Count > 0)
            {
                docs.Add(BuildReceiptDocument(foodBillRows, "FOOD", timestamp));
                labels.Add("FOOD");
            }

            var result = await ethernetPrinterService.PrintReceiptsAsync(docs, host, port);
            if (result.Success)
            {
                return PrinterResult.Ok($"Scontrini inviati: {string.Join(" + ", labels)}.");
            }

            return result;
        }

        private static async Task<bool> EnsureBluetoothPermissionAsync()
        {
#if ANDROID
            if (BluetoothPermissionHelper.AreBluetoothPermissionsGranted())
            {
                return true;
            }

            BluetoothPermissionHelper.RequestBluetoothPermissions();
            return await BluetoothPermissionHelper.WaitForPermissionResult();
#else
            await Task.CompletedTask;
            return true;
#endif
        }

        private bool HasPendingTransaction() => (drinkBillRows.Count + foodBillRows.Count) > 0 && totalBill > 0;

        private async Task<bool> TrySaveCurrentTransactionAsync(string emptyAlertTitle, string emptyAlertMessage)
        {
            if (!HasPendingTransaction())
            {
                await DisplayAlert(emptyAlertTitle, emptyAlertMessage, "OK");
                return false;
            }

            if (transactionAlreadySaved)
            {
                return true;
            }

            try
            {
                await TransactionsStorage.AppendLineAsync(BuildSerializedTransaction());
                transactionAlreadySaved = true;
                return true;
            }
            catch (Exception ex)
            {
                await DisplayAlert("Salvataggio", $"Errore durante il salvataggio della transazione: {ex.Message}", "OK");
                return false;
            }
        }

        private string BuildSerializedTransaction()
        {
            var sb = new StringBuilder();
            sb.Append($"DATA={DateTime.Now:yyyy-MM-dd HH:mm:ss};");
            double total = 0;
            foreach (var row in drinkBillRows.Concat(foodBillRows))
            {
                sb.Append($"{row.Name}={row.Quantity};");
                total += row.LineTotal;
            }
            sb.Append(string.Create(CultureInfo.InvariantCulture, $"TOTALE={total:0.00}"));
            return sb.ToString();
        }

        private static ReceiptDocument BuildReceiptDocument(IEnumerable<BillItemRow> rows, string sectionLabel, DateTime timestamp)
        {
            var items = rows
                .Select(r => new ReceiptLineItem(r.Name, r.Quantity, r.Price, r.LineTotal))
                .ToList();

            return new ReceiptDocument
            {
                Title = "POLO ZEROSEI",
                Subtitle = "DON CIRILLO PIZIO",
                SectionLabel = sectionLabel,
                Items = items,
                Total = items.Sum(i => i.LineTotal),
                Timestamp = timestamp
            };
        }

        public void UpdateBill()
        {
            transactionAlreadySaved = false;

            drinkBillRows.Clear();
            foodBillRows.Clear();

            double total = 0;
            foreach (var row in rowsById.Values
                         .Where(r => r.Quantity > 0)
                         .OrderBy(r => r.SortOrder)
                         .ThenBy(r => r.Name, StringComparer.OrdinalIgnoreCase))
            {
                var billRow = new BillItemRow(row);
                if (row.Category == MenuCategory.Drink)
                {
                    drinkBillRows.Add(billRow);
                }
                else
                {
                    foodBillRows.Add(billRow);
                }
                total += row.LineTotal;
            }

            totalBill = total;
            TotaleLb.Text = $"{total:0.00} €";

            DrinkBillSection.IsVisible = drinkBillRows.Count > 0;
            FoodBillSection.IsVisible = foodBillRows.Count > 0;
            EmptyBillLb.IsVisible = drinkBillRows.Count == 0 && foodBillRows.Count == 0;
        }

        public void CleanBill()
        {
            transactionAlreadySaved = false;

            foreach (var row in rowsById.Values)
            {
                row.Quantity = 0;
            }

            drinkBillRows.Clear();
            foodBillRows.Clear();
            DrinkBillSection.IsVisible = false;
            FoodBillSection.IsVisible = false;
            EmptyBillLb.IsVisible = true;

            totalBill = 0;
            totalMoney = 0;

            ReminderLb.Text = "";
            MoneyLb.Text = "";
            TotaleLb.Text = "0,00 €";
        }

        public void OnM100Clicked(object sender, EventArgs e) { totalMoney += 100; UpdateMoney(); }
        public void OnM50Clicked(object sender, EventArgs e)  { totalMoney += 50;  UpdateMoney(); }
        public void OnM20Clicked(object sender, EventArgs e)  { totalMoney += 20;  UpdateMoney(); }
        public void OnM10Clicked(object sender, EventArgs e)  { totalMoney += 10;  UpdateMoney(); }
        public void OnM5Clicked(object sender, EventArgs e)   { totalMoney += 5;   UpdateMoney(); }
        public void OnM2Clicked(object sender, EventArgs e)   { totalMoney += 2;   UpdateMoney(); }
        public void OnM1Clicked(object sender, EventArgs e)   { totalMoney += 1;   UpdateMoney(); }
        public void OnM05Clicked(object sender, EventArgs e)  { totalMoney += 0.5; UpdateMoney(); }

        public void UpdateMoney() => MoneyLb.Text = $"TOTALE: {totalMoney:0.00} €";

        public void OnCleanMoneyClicked(object sender, EventArgs e)
        {
            totalMoney = 0;
            ReminderLb.Text = "";
            MoneyLb.Text = "";
        }

        public void OnComputeReminderClicked(object sender, EventArgs e)
        {
            ReminderLb.Text = $"RESTO: {totalMoney - totalBill:0.00} €";
        }
    }

    public sealed class OrderItemRow : INotifyPropertyChanged
    {
        private int quantity;

        public OrderItemRow(CatalogItem item)
        {
            Id = item.Id;
            Name = item.Name;
            Price = item.Price;
            Category = item.Category;
            SortOrder = item.SortOrder;
        }

        public string Id { get; }
        public string Name { get; }
        public double Price { get; }
        public MenuCategory Category { get; }
        public int SortOrder { get; }

        public int Quantity
        {
            get => quantity;
            set
            {
                if (quantity == value)
                {
                    return;
                }
                quantity = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(QuantityText));
                OnPropertyChanged(nameof(LineTotal));
            }
        }

        public string ButtonLabel => Name;
        public string QuantityText => Quantity > 0 ? Quantity.ToString() : "—";
        public double LineTotal => Price * Quantity;

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public sealed class BillItemRow
    {
        public BillItemRow(OrderItemRow row)
        {
            Id = row.Id;
            Name = row.Name;
            Price = row.Price;
            Quantity = row.Quantity;
            Category = row.Category;
            SortOrder = row.SortOrder;
        }

        public string Id { get; }
        public string Name { get; }
        public double Price { get; }
        public int Quantity { get; }
        public MenuCategory Category { get; }
        public int SortOrder { get; }

        public string QuantityXPrice => $"{Quantity} x {Price:0.00} €";
        public double LineTotal => Price * Quantity;
        public string LineTotalText => $"{LineTotal:0.00} €";

        public CatalogItem Snapshot() => new()
        {
            Id = Id,
            Name = Name,
            Price = Price,
            Category = Category,
            SortOrder = SortOrder
        };
    }
}
