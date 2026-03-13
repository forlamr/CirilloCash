using CirilloCash.Services;

namespace CirilloCash;

public partial class PrinterSettingsPage : ContentPage
{
    public PrinterSettingsPage()
    {
        InitializeComponent();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        PrinterNameEntry.Text = PrinterSettings.PrinterNameHint;
        PrinterMacEntry.Text = PrinterSettings.PrinterMacAddress;
    }

    private async void OnSaveClicked(object sender, EventArgs e)
    {
        var printerName = string.IsNullOrWhiteSpace(PrinterNameEntry.Text)
            ? ThermalPrinterService.DefaultPrinterHint
            : PrinterNameEntry.Text.Trim();

        var printerMac = PrinterMacEntry.Text?.Trim().ToUpperInvariant() ?? string.Empty;

        PrinterSettings.PrinterNameHint = printerName;
        PrinterSettings.PrinterMacAddress = printerMac;

        await DisplayAlert("Impostazioni", "Configurazione stampante salvata.", "OK");
    }
}
