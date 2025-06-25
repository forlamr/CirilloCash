namespace CirilloCash
{
    public partial class ResultsPage : ContentPage
    {
        public ResultsPage()
        {
            InitializeComponent();
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            ResultsLabel.Text = string.Empty;
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
        }

        public async void OnCalculateClicked(object sender, EventArgs e)
        {
#if ANDROID
            ResultsLabel.Text = string.Empty;

            if (!StoragePermissionHelper.HasManageAllFilesPermission())
            {
                StoragePermissionHelper.RequestManageAllFilesPermission();
                bool granted = await StoragePermissionHelper.WaitForPermissionResult();
                if (!granted)
                {
                    await DisplayAlert("Permesso negato", "Servono permessi per accedere ai file!", "OK");
                    return;
                }
            }

            string downloadsPath = Android.OS.Environment.GetExternalStoragePublicDirectory(Android.OS.Environment.DirectoryDownloads).AbsolutePath;
            string file = Path.Combine(downloadsPath, "transazioni.txt");

            if (File.Exists(file))
            {
                var dict = CalculateTotal(file);
                foreach (var kvp in dict)
                {
                    if (kvp.Key == "TOTALE")
                        ResultsLabel.Text += $"{kvp.Key}: {kvp.Value} €\n";
                    else
                        ResultsLabel.Text += $"{kvp.Key}: {kvp.Value}\n";
                }
            }
            else
            {
                ResultsLabel.Text = "File non trovato: " + file;
            }
#else
            string file = "transazioni.txt";
            string[] lines = new[]
            {
                "Bionda=1;Rossa=1;Spritz=1;Acqua=1;Coca=1;TOTALE=19,5",
                "Bionda=1;Rossa=1;Spritz=1;Acqua=1;Coca=0;TOTALE=16,5",
                "Bionda=0;Rossa=0;Spritz=0;Acqua=2;Coca=0;TOTALE=2"
            };
            File.WriteAllLines(file, lines);

            if (File.Exists(file))
            {
                var dict = CalculateTotal(file);
            }
#endif
        }

        protected Dictionary<string, float> CalculateTotal(string filePath)
        {
            var dict = new Dictionary<string, float>();
            foreach (var line in File.ReadLines(filePath))
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                var pairs = line.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var pair in pairs)
                {
                    var kv = pair.Split(new[] { '=' }, 2);
                    if (kv.Length != 2)
                        continue;

                    string key = kv[0].Trim();
                    string valuePart = kv[1].Trim();
                    if (float.TryParse(valuePart, out float value))
                    {
                        if (dict.ContainsKey(key))
                        {
                            dict[key] += value;
                        }
                        else
                        {
                            dict[key] = value;
                        }
                    }
                }
            }
            return dict;
        }
    }
}