using CirilloCash.Services;

namespace CirilloCash;

public partial class PrinterSettingsPage : ContentPage
{
    private readonly ThermalPrinterService thermalPrinterService = new();
    private readonly EthernetPrinterService ethernetPrinterService = new();
    private readonly EthernetPrinterDiscovery printerDiscovery = new();

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
                await DisplayAlertAsync("Impostazioni", error, "OK");
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
        await DisplayAlertAsync("Impostazioni", "Configurazione stampante salvata.", "OK");
    }

    private async void OnTestPrintClicked(object sender, EventArgs e)
    {
        var activePrinter = (ActivePrinter)Math.Max(0, PrinterTypePicker.SelectedIndex);

        PrinterResult result;
        if (activePrinter == ActivePrinter.Ethernet)
        {
            if (!TryReadEthernetSettings(out var host, out var port, out var error))
            {
                await DisplayAlertAsync("Stampa di prova", error, "OK");
                return;
            }

            var testDoc = new ReceiptDocument
            {
                Title = "POLO ZEROSEI",
                Subtitle = "DON CIRILLO PIZIO",
                SectionLabel = "TEST",
                Items = new[]
                {
                    new ReceiptLineItem("Stampa di prova", 1, 0.00, 0.00)
                },
                Total = 0.00,
                Timestamp = DateTime.Now
            };
            result = await ethernetPrinterService.PrintReceiptAsync(testDoc, host, port);
        }
        else
        {
            var printerName = string.IsNullOrWhiteSpace(PrinterNameEntry.Text)
                ? ThermalPrinterService.DefaultPrinterHint
                : PrinterNameEntry.Text.Trim();
            var printerMac = PrinterMacEntry.Text?.Trim().ToUpperInvariant() ?? string.Empty;

            var testDoc = new ReceiptDocument
            {
                Title = "POLO ZEROSEI",
                Subtitle = "DON CIRILLO PIZIO",
                SectionLabel = "TEST",
                Items = new[]
                {
                    new ReceiptLineItem("Stampa di prova", 1, 0.00, 0.00)
                },
                Total = 0.00,
                Timestamp = DateTime.Now
            };
            result = await thermalPrinterService.PrintReceiptsAsync(new[] { testDoc }, printerName, printerMac);
        }

        await DisplayAlertAsync("Stampa di prova", result.Message, "OK");
    }

    private async void OnDiscoverClicked(object sender, EventArgs e)
    {
        DiscoverBtn.IsEnabled = false;
        var originalText = DiscoverBtn.Text;
        DiscoverBtn.Text = "Ricerca in corso…";

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(45));
            var report = await printerDiscovery.DiscoverAsync(perHostTimeoutMs: 2000, totalTimeoutMs: 40000, ct: cts.Token);

            var subnetInfo = report.ScannedSubnets.Count == 0
                ? "Nessuna interfaccia di rete privata rilevata."
                : "Subnet scansionate (" + report.HostsProbed + " host):\n  " + string.Join("\n  ", report.ScannedSubnets);

            if (report.ResponsiveHosts.Count == 0)
            {
                await DisplayAlertAsync("Discovery",
                    "Nessun dispositivo risponde sulla porta 9100.\n\n" + subnetInfo,
                    "OK");
                return;
            }

            var labels = report.ResponsiveHosts.Select(h => $"{h}:9100").ToArray();
            var picked = await DisplayActionSheetAsync(
                $"Dispositivi che rispondono su porta 9100 ({report.ResponsiveHosts.Count}):",
                "Annulla", null, labels);

            if (string.IsNullOrEmpty(picked) || picked == "Annulla")
            {
                return;
            }

            var selected = report.ResponsiveHosts.FirstOrDefault(h => $"{h}:9100" == picked);
            if (selected is null)
            {
                return;
            }

            HostEntry.Text = selected;
            PortEntry.Text = "9100";
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Discovery", $"Errore durante la ricerca: {ex.Message}", "OK");
        }
        finally
        {
            DiscoverBtn.Text = originalText;
            DiscoverBtn.IsEnabled = true;
        }
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
