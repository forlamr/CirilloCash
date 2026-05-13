using CirilloCash.Services;

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

        public async void OnLoadClicked(object sender, EventArgs e)
        {
            if (!TransactionsStorage.Exists())
            {
                ContentLabel.Text = "Nessuna transazione registrata.";
                return;
            }

            ContentLabel.Text = await TransactionsStorage.ReadAllAsync();
        }

        public async void OnExportClicked(object sender, EventArgs e)
        {
            if (!TransactionsStorage.Exists())
            {
                await DisplayAlert("Esporta", "Nessuna transazione da esportare.", "OK");
                return;
            }

            try
            {
                await Share.Default.RequestAsync(new ShareFileRequest
                {
                    Title = "Esporta transazioni",
                    File = new ShareFile(TransactionsStorage.FilePath)
                });
            }
            catch (Exception ex)
            {
                await DisplayAlert("Esporta", $"Errore: {ex.Message}", "OK");
            }
        }

        public async void OnDeleteClicked(object sender, EventArgs e)
        {
            if (!TransactionsStorage.Exists())
            {
                await DisplayAlert("Delete", "Nessuna transazione da eliminare.", "OK");
                return;
            }

            var confirmed = await DisplayAlert(
                "Delete",
                "Eliminare tutte le transazioni? L'operazione è irreversibile.",
                "Elimina", "Annulla");

            if (!confirmed)
            {
                return;
            }

            try
            {
                TransactionsStorage.Delete();
                ContentLabel.Text = string.Empty;
                await DisplayAlert("Delete", "Transazioni eliminate.", "OK");
            }
            catch (Exception ex)
            {
                await DisplayAlert("Delete", $"Errore: {ex.Message}", "OK");
            }
        }
    }
}
