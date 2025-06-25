namespace CirilloCash
{
    public partial class TransactionsPage : ContentPage
    {
        public TransactionsPage()
        {
            InitializeComponent();
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            ContentLabel.Text = string.Empty;
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
        }

        public async void OnLoadClicked(object sender, EventArgs e)
        {
#if ANDROID
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
                string text = await File.ReadAllTextAsync(file);
                ContentLabel.Text = text;
            }
            else
            {
                ContentLabel.Text = "File non trovato: " + file;
            }
#else
            ContentLabel.Text = "Funzionalità disponibile solo su Android.";
#endif
        }
    }
}