using CirilloCash.Services;

namespace CirilloCash;

public partial class PrinterSettingsPage : ContentPage
{
    private readonly ThermalPrinterService thermalPrinterService = new();
    private readonly EthernetPrinterService ethernetPrinterService = new();

    public PrinterSettingsPage()
    {
        InitializeComponent();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        PrinterNameEntry.Text = PrinterSettings.PrinterNameHint;
        PrinterMacEntry.Text = PrinterSettings.PrinterMacAddress;
        HostEntry.Text = PrinterSettings.EthernetHost;
        PortEntry.Text = PrinterSettings.EthernetPort.ToString();

        PrinterTypePicker.SelectedIndex = (int)PrinterSettings.ActivePrinter;
        UpdateSectionsVisibility();
    }

    private void OnPrinterTypeChanged(object sender, EventArgs e)
    {
        UpdateSectionsVisibility();
    }

    private void UpdateSectionsVisibility()
    {
        var isEthernet = PrinterTypePicker.SelectedIndex == (int)ActivePrinter.Ethernet;
        X5Section.IsVisible = !isEthernet;
        EthSection.IsVisible = isEthernet;
    }

    private async void OnSaveClicked(object sender, EventArgs e)
    {
        var activePrinter = (ActivePrinter)Math.Max(0, PrinterTypePicker.SelectedIndex);

        if (activePrinter == ActivePrinter.Ethernet)
        {
            if (!TryReadEthernetSettings(out var host, out var port, out var error))
            {
                await DisplayAlert("Impostazioni", error, "OK");
                return;
            }

            PrinterSettings.EthernetHost = host;
            PrinterSettings.EthernetPort = port;
        }
        else
        {
            var printerName = string.IsNullOrWhiteSpace(PrinterNameEntry.Text)
                ? ThermalPrinterService.DefaultPrinterHint
                : PrinterNameEntry.Text.Trim();
            var printerMac = PrinterMacEntry.Text?.Trim().ToUpperInvariant() ?? string.Empty;

            PrinterSettings.PrinterNameHint = printerName;
            PrinterSettings.PrinterMacAddress = printerMac;
        }

        PrinterSettings.ActivePrinter = activePrinter;
        await DisplayAlert("Impostazioni", "Configurazione stampante salvata.", "OK");
    }

    private async void OnTestPrintClicked(object sender, EventArgs e)
    {
        var sample =
            "      POLO ZEROSEI\r\n" +
            "    DON CIRILLO PIZIO\r\n" +
            "------------------------------\r\n" +
            "Stampa di prova\r\n" +
            $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}\r\n" +
            "------------------------------\r\n";

        var activePrinter = (ActivePrinter)Math.Max(0, PrinterTypePicker.SelectedIndex);

        PrinterResult result;
        if (activePrinter == ActivePrinter.Ethernet)
        {
            if (!TryReadEthernetSettings(out var host, out var port, out var error))
            {
                await DisplayAlert("Stampa di prova", error, "OK");
                return;
            }

            result = await ethernetPrinterService.PrintAsync(sample, host, port);
        }
        else
        {
            var printerName = string.IsNullOrWhiteSpace(PrinterNameEntry.Text)
                ? ThermalPrinterService.DefaultPrinterHint
                : PrinterNameEntry.Text.Trim();
            var printerMac = PrinterMacEntry.Text?.Trim().ToUpperInvariant() ?? string.Empty;
            result = await thermalPrinterService.PrintAsync(sample, printerName, printerMac);
        }

        await DisplayAlert("Stampa di prova", result.Message, "OK");
    }

    private bool TryReadEthernetSettings(out string host, out int port, out string error)
    {
        host = HostEntry.Text?.Trim() ?? string.Empty;
        port = 0;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(host))
        {
            error = "Inserisci l'host o l'IP della stampante.";
            return false;
        }

        var portText = PortEntry.Text?.Trim();
        if (string.IsNullOrWhiteSpace(portText))
        {
            port = PrinterSettings.DefaultEthernetPort;
        }
        else if (!int.TryParse(portText, out port) || port <= 0 || port > 65535)
        {
            error = "Porta TCP non valida (1-65535).";
            return false;
        }

        return true;
    }
}
