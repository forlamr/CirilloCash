using System.ComponentModel;
using System.IO;
using System.Text;
using CirilloCash.Services;
#if ANDROID
using Android.Content;
using Android.Provider;
#endif

namespace CirilloCash
{
    public partial class MainPage : ContentPage
    {
        public enum DrinkType
        {
            [Description("Bionda")]
            BlondeBeer,
            [Description("Rossa")]
            RedBeer,
            [Description("Spritz")]
            Spritz,
            [Description("Acqua")]
            Water,
            [Description("Bibita")]
            SoftDrink,
            [Description("Anguria")]
            WarterMelon
        }

        Label[,] labelGrid;

        Dictionary<DrinkType, int> drinks = new();

        Dictionary<DrinkType, double> prices = new()
        {
            { DrinkType.BlondeBeer , 4 },
            { DrinkType.RedBeer, 4.5 },
            { DrinkType.Spritz, 4 },
            { DrinkType.Water, 1 },
            { DrinkType.SoftDrink, 2 },
            { DrinkType.WarterMelon, 3 }
        };

        double totalBill = 0;
        double totalMoney = 0;
        readonly ThermalPrinterService thermalPrinterService = new();
        (string SerializedTransaction, string ReceiptText)? savedTransactionData;

        public MainPage()
        {
            InitializeComponent();
            labelGrid = new Label[9, 3]
            {
                { Label00, Label01, Label02 },
                { Label10, Label11, Label12 },
                { Label20, Label21, Label22 },
                { Label30, Label31, Label32 },
                { Label40, Label41, Label42 },
                { Label50, Label51, Label52 },
                { Label60, Label61, Label62 },
                { Label70, Label71, Label72 },
                { Label80, Label81, Label82 }
            };
        }

        private void OnAddBlondeBeerClicked(object sender, EventArgs e)
        {
            if (drinks.ContainsKey(DrinkType.BlondeBeer))
            {
                drinks[DrinkType.BlondeBeer]++;
            }
            else
            {
                drinks.Add(DrinkType.BlondeBeer, 1);
            }

            UpdateBill();
        }
        private void OnRemoveBlondeBeerClicked(object sender, EventArgs e)
        {
            if (drinks.ContainsKey(DrinkType.BlondeBeer) && drinks[DrinkType.BlondeBeer] > 0)
            {
                drinks[DrinkType.BlondeBeer]--;
                if (drinks[DrinkType.BlondeBeer] == 0)
                {
                    drinks.Remove(DrinkType.BlondeBeer);
                }
            }

            UpdateBill();
        }
        private void OnAddRedBeerClicked(object sender, EventArgs e)
        {
            if (drinks.ContainsKey(DrinkType.RedBeer))
            {
                drinks[DrinkType.RedBeer]++;
            }
            else
            {
                drinks.Add(DrinkType.RedBeer, 1);
            }

            UpdateBill();
        }
        private void OnRemoveRedBeerClicked(object sender, EventArgs e)
        {
            if (drinks.ContainsKey(DrinkType.RedBeer) && drinks[DrinkType.RedBeer] > 0)
            {
                drinks[DrinkType.RedBeer]--;
                if (drinks[DrinkType.RedBeer] == 0)
                {
                    drinks.Remove(DrinkType.RedBeer);
                }
            }

            UpdateBill();
        }
        private void OnAddSpritzClicked(object sender, EventArgs e)
        {
            if (drinks.ContainsKey(DrinkType.Spritz))
            {
                drinks[DrinkType.Spritz]++;
            }
            else
            {
                drinks.Add(DrinkType.Spritz, 1);
            }

            UpdateBill();
        }
        private void OnRemoveSpritzClicked(object sender, EventArgs e)
        {
            if (drinks.ContainsKey(DrinkType.Spritz) && drinks[DrinkType.Spritz] > 0)
            {
                drinks[DrinkType.Spritz]--;
                if (drinks[DrinkType.Spritz] == 0)
                {
                    drinks.Remove(DrinkType.Spritz);
                }
            }

            UpdateBill();
        }
        private void OnAddWaterClicked(object sender, EventArgs e)
        {
            if (drinks.ContainsKey(DrinkType.Water))
            {
                drinks[DrinkType.Water]++;
            }
            else
            {
                drinks.Add(DrinkType.Water, 1);
            }

            UpdateBill();
        }
        private void OnRemoveWaterClicked(object sender, EventArgs e)
        {
            if (drinks.ContainsKey(DrinkType.Water) && drinks[DrinkType.Water] > 0)
            {
                drinks[DrinkType.Water]--;
                if (drinks[DrinkType.Water] == 0)
                {
                    drinks.Remove(DrinkType.Water);
                }
            }

            UpdateBill();
        }
        private void OnAddSoftDrinkClicked(object sender, EventArgs e)
        {
            if (drinks.ContainsKey(DrinkType.SoftDrink))
            {
                drinks[DrinkType.SoftDrink]++;
            }
            else
            {
                drinks.Add(DrinkType.SoftDrink, 1);
            }

            UpdateBill();
        }
        private void OnRemoveSoftDrinkClicked(object sender, EventArgs e)
        {
            if (drinks.ContainsKey(DrinkType.SoftDrink) && drinks[DrinkType.SoftDrink] > 0)
            {
                drinks[DrinkType.SoftDrink]--;
                if (drinks[DrinkType.SoftDrink] == 0)
                {
                    drinks.Remove(DrinkType.SoftDrink);
                }
            }

            UpdateBill();
        }
        private void OnAddWaterMelonClicked(object sender, EventArgs e)
        {
            if (drinks.ContainsKey(DrinkType.WarterMelon))
            {
                drinks[DrinkType.WarterMelon]++;
            }
            else
            {
                drinks.Add(DrinkType.WarterMelon, 1);
            }

            UpdateBill();
        }
        private void OnRemoveWaterMelonClicked(object sender, EventArgs e)
        {
            if (drinks.ContainsKey(DrinkType.WarterMelon) && drinks[DrinkType.WarterMelon] > 0)
            {
                drinks[DrinkType.WarterMelon]--;
                if (drinks[DrinkType.WarterMelon] == 0)
                {
                    drinks.Remove(DrinkType.WarterMelon);
                }
            }

            UpdateBill();
        }

        private void OnCleanClicked(object sender, EventArgs e)
        {
            CleanBill();
        }

        private async void OnSaveClicked(object sender, EventArgs e)
        {
            var saveResult = await TrySaveCurrentTransactionAsync(
                "Salvataggio",
                "Nessuna transazione da salvare.");
            if (!saveResult.Success)
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

            if (!await EnsureBluetoothPermissionAsync())
            {
                await DisplayAlert("Stampa", "Permesso Bluetooth negato. Abilita 'Dispositivi nelle vicinanze'.", "OK");
                return;
            }

            var saveResult = await TrySaveCurrentTransactionAsync(
                "Stampa",
                "Nessun conto da stampare.");
            if (!saveResult.Success)
            {
                return;
            }

            var printResult = await thermalPrinterService.PrintAsync(
                saveResult.ReceiptText,
                PrinterSettings.PrinterNameHint,
                PrinterSettings.PrinterMacAddress);

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

        public async Task AppendTextToDownloadAsync(string fileName, string text)
        {
#if ANDROID
            var resolver = Platform.CurrentActivity.ContentResolver;

            // Check if the file already exists
            string selection = $"{MediaStore.Downloads.InterfaceConsts.DisplayName}=?";
            string[] selectionArgs = { fileName };
            var cursor = resolver.Query(MediaStore.Downloads.ExternalContentUri, null, selection, selectionArgs, null);

            Android.Net.Uri fileUri = null;
            if (cursor != null && cursor.MoveToFirst())
            {
                int idCol = cursor.GetColumnIndexOrThrow(MediaStore.Downloads.InterfaceConsts.Id);
                long id = cursor.GetLong(idCol);
                fileUri = ContentUris.WithAppendedId(MediaStore.Downloads.ExternalContentUri, id);
                cursor.Close();
            }

            // If not found, create a new file
            if (fileUri == null)
            {
                var values = new ContentValues();
                values.Put(MediaStore.Downloads.InterfaceConsts.DisplayName, fileName);
                values.Put(MediaStore.Downloads.InterfaceConsts.MimeType, "text/plain");
                values.Put(MediaStore.IMediaColumns.RelativePath, Android.OS.Environment.DirectoryDownloads);
                fileUri = resolver.Insert(MediaStore.Downloads.ExternalContentUri, values);
            }

            // Open the file for writing
            if (fileUri != null)
            {
                using var stream = resolver.OpenOutputStream(fileUri, "wa"); // "wa" = write+append
                using var writer = new StreamWriter(stream);
                await writer.WriteLineAsync(text);
            }
#endif
        }

        private bool HasPendingTransaction()
        {
            return drinks.Any() && totalBill > 0;
        }

        private async Task<(bool Success, string ReceiptText)> TrySaveCurrentTransactionAsync(
            string emptyAlertTitle,
            string emptyAlertMessage)
        {
            if (!HasPendingTransaction())
            {
                await DisplayAlert(emptyAlertTitle, emptyAlertMessage, "OK");
                return (false, string.Empty);
            }

            if (savedTransactionData.HasValue)
            {
                return (true, savedTransactionData.Value.ReceiptText);
            }

            try
            {
                var transactionData = BuildTransactionData();
                await AppendTextToDownloadAsync("transazioni.txt", transactionData.SerializedTransaction);
                savedTransactionData = transactionData;
                return (true, transactionData.ReceiptText);
            }
            catch (Exception ex)
            {
                await DisplayAlert("Salvataggio", $"Errore durante il salvataggio della transazione: {ex.Message}", "OK");
                return (false, string.Empty);
            }
        }

        private (string SerializedTransaction, string ReceiptText) BuildTransactionData()
        {
            var transactionBuilder = new StringBuilder();
            var receiptBuilder = new StringBuilder();
            var timestamp = DateTime.Now;

            double total = 0;
            transactionBuilder.Append($"DATA={timestamp:yyyy-MM-dd HH:mm:ss};");
            receiptBuilder.AppendLine(CenterReceiptLine("POLO ZEROSEI"));
            receiptBuilder.AppendLine(CenterReceiptLine("DON CIRILLO PIZIO"));
            receiptBuilder.AppendLine("------------------------------");

            var dtValues = Enum.GetValues<DrinkType>();
            foreach (var dt in dtValues)
            {
                drinks.TryGetValue(dt, out int quantity);
                var drinkTotal = quantity * prices[dt];
                total += drinkTotal;

                transactionBuilder.Append($"{dt.ToDescription()}={quantity};");
                if (quantity > 0)
                {
                    receiptBuilder.AppendLine($"{dt.ToDescription(),-12}{quantity,3}");
                }
            }

            transactionBuilder.Append($"TOTALE={total:0.00}");
            receiptBuilder.AppendLine("------------------------------");
            receiptBuilder.AppendLine(CenterReceiptLine(timestamp.ToString("yyyy-MM-dd HH:mm:ss")));

            for (var i = 0; i < 3; i++)
            {
                receiptBuilder.AppendLine();
            }

            return (transactionBuilder.ToString(), receiptBuilder.ToString());
        }

        private static string CenterReceiptLine(string text, int width = 30)
        {
            if (string.IsNullOrWhiteSpace(text) || text.Length >= width)
            {
                return text;
            }

            var leftPadding = (width - text.Length) / 2;
            return new string(' ', leftPadding) + text;
        }

        public void UpdateBill()
        {
            savedTransactionData = null;

            foreach (var label in labelGrid)
            {
                label.Text = string.Empty;
            }

            labelGrid[0, 1].Text = "CONTO";

            int rowCounter = 1;
            double total = 0;
            foreach (var drink in drinks)
            {
                labelGrid[rowCounter, 0].Text = $"{drink.Key.ToDescription()}";
                labelGrid[rowCounter, 1].Text = $"{drink.Value} x {prices[drink.Key]} €";
                labelGrid[rowCounter, 2].Text = $"{drink.Value * prices[drink.Key]} €";

                total += drink.Value * prices[drink.Key];

                rowCounter++;
            }

            labelGrid[rowCounter, 0].Text = "---------";
            labelGrid[rowCounter, 1].Text = "---------";
            labelGrid[rowCounter, 2].Text = "---------";

            rowCounter++;

            labelGrid[rowCounter, 0].Text = "TOTALE";
            labelGrid[rowCounter, 2].Text = $"{total} €";

            totalBill = total;
        }
        public void CleanBill()
        {
            drinks = new Dictionary<DrinkType, int>();
            savedTransactionData = null;

            foreach (var label in labelGrid)
            {
                label.Text = string.Empty;
            }

            totalBill = 0;
            totalMoney = 0;

            ReminderLb.Text = "";
            MoneyLb.Text = "";
        }

        public void OnM100Clicked(object sender, EventArgs e)
        {
            totalMoney += 100;
            UpdateMoney();
        }
        public void OnM50Clicked(object sender, EventArgs e)
        {
            totalMoney += 50;
            UpdateMoney();
        }
        public void OnM20Clicked(object sender, EventArgs e)
        {
            totalMoney += 20;
            UpdateMoney();
        }
        public void OnM10Clicked(object sender, EventArgs e)
        {
            totalMoney += 10;
            UpdateMoney();
        }
        public void OnM5Clicked(object sender, EventArgs e)
        {
            totalMoney += 5;
            UpdateMoney();
        }
        public void OnM2Clicked(object sender, EventArgs e)
        {
            totalMoney += 2;
            UpdateMoney();
        }
        public void OnM1Clicked(object sender, EventArgs e)
        {
            totalMoney += 1;
            UpdateMoney();
        }
        public void OnM05Clicked(object sender, EventArgs e)
        {
            totalMoney += 0.5;
            UpdateMoney();
        }

        public void UpdateMoney()
        {
            MoneyLb.Text = $"TOTALE: {totalMoney} €";
        }

        public void OnCleanMoneyClicked(object sender, EventArgs e)
        {
            totalMoney = 0;
            ReminderLb.Text = "";
            MoneyLb.Text = "";
        }

        public void OnComputeReminderClicked(object sender, EventArgs e)
        {
            ReminderLb.Text = $"RESTO: {totalMoney - totalBill} €";
        }
    }

    public static class EnumExtensions
    {
        public static string ToDescription(this Enum value)
        {
            var fi = value.GetType().GetField(value.ToString());
            var attrs = (DescriptionAttribute[])fi.GetCustomAttributes(typeof(DescriptionAttribute), false);
            return attrs.Length > 0 ? attrs[0].Description : value.ToString();
        }
    }
}
