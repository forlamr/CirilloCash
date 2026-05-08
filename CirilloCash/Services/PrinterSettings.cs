namespace CirilloCash.Services;

public enum ActivePrinter
{
    X5 = 0,
    Ethernet = 1
}

public static class PrinterSettings
{
    private const string PrinterNameHintKey = "printer_name_hint";
    private const string PrinterMacAddressKey = "printer_mac_address";
    private const string EthernetHostKey = "printer_eth_host";
    private const string EthernetPortKey = "printer_eth_port";
    private const string ActivePrinterKey = "printer_active";

    public const int DefaultEthernetPort = 9100;

    public static string PrinterNameHint
    {
        get => Preferences.Default.Get(PrinterNameHintKey, ThermalPrinterService.DefaultPrinterHint);
        set => Preferences.Default.Set(PrinterNameHintKey, value?.Trim() ?? string.Empty);
    }

    public static string PrinterMacAddress
    {
        get => Preferences.Default.Get(PrinterMacAddressKey, string.Empty);
        set => Preferences.Default.Set(PrinterMacAddressKey, value?.Trim() ?? string.Empty);
    }

    public static string EthernetHost
    {
        get => Preferences.Default.Get(EthernetHostKey, string.Empty);
        set => Preferences.Default.Set(EthernetHostKey, value?.Trim() ?? string.Empty);
    }

    public static int EthernetPort
    {
        get => Preferences.Default.Get(EthernetPortKey, DefaultEthernetPort);
        set => Preferences.Default.Set(EthernetPortKey, value);
    }

    public static ActivePrinter ActivePrinter
    {
        get => (ActivePrinter)Preferences.Default.Get(ActivePrinterKey, (int)ActivePrinter.X5);
        set => Preferences.Default.Set(ActivePrinterKey, (int)value);
    }
}
