using System.ComponentModel;
using static Microsoft.Maui.ApplicationModel.Permissions;
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
            [Description("Coca")]
            CocaCola
        }

        Label[,] labelGrid;

        Dictionary<DrinkType, int> drinks = new Dictionary<DrinkType, int>();

        Dictionary<DrinkType, double> prices = new Dictionary<DrinkType, double>
        {
            { DrinkType.BlondeBeer , 4.5 },
            { DrinkType.RedBeer, 5 },
            { DrinkType.Spritz, 6 },
            { DrinkType.Water, 1 },
            { DrinkType.CocaCola, 3 }
        };

        double totalBill = 0;
        double totalMoney = 0;


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
        private void OnAddCocaColaClicked(object sender, EventArgs e)
        {
            if (drinks.ContainsKey(DrinkType.CocaCola))
            {
                drinks[DrinkType.CocaCola]++;
            }
            else
            {
                drinks.Add(DrinkType.CocaCola, 1);
            }

            UpdateBill();
        }
        private void OnRemoveCocaColaClicked(object sender, EventArgs e)
        {
            if (drinks.ContainsKey(DrinkType.CocaCola) && drinks[DrinkType.CocaCola] > 0)
            {
                drinks[DrinkType.CocaCola]--;
                if (drinks[DrinkType.CocaCola] == 0)
                {
                    drinks.Remove(DrinkType.CocaCola);
                }
            }

            UpdateBill();
        }

        private void OnCleanClicked(object sender, EventArgs e)
        {
            CleanBill();
        }

        private void OnSaveClicked(object sender, EventArgs e)
        {
            string transaction = "";
            double total = 0;
            var dtValues = Enum.GetValues<DrinkType>();
            foreach (var dt in dtValues)
            {
                drinks.TryGetValue(dt, out int value);
                transaction += $"{dt.ToDescription()}={value};";
                total += value * prices[dt];
            }

            transaction += $"TOTALE={total}";

            AppendTextToDownloadAsync("transazioni.txt", transaction).ConfigureAwait(false);

            CleanBill();
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

        public void UpdateBill()
        {
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

            labelGrid[rowCounter, 0].Text = $"---------";
            labelGrid[rowCounter, 1].Text = $"---------";
            labelGrid[rowCounter, 2].Text = $"---------";

            rowCounter++;

            labelGrid[rowCounter, 0].Text = $"TOTALE";
            labelGrid[rowCounter, 2].Text = $"{total} €";

            totalBill = total;
        }
        public void CleanBill()
        {
            drinks = new Dictionary<DrinkType, int>();

            foreach (var label in labelGrid)
            {
                label.Text = string.Empty;
            }

            totalBill = 0;
            totalMoney = 0;

            ReminderLb.Text = $"";
            MoneyLb.Text = $"";
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
            ReminderLb.Text = $"";
            MoneyLb.Text = $"";
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