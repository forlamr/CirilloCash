namespace CirilloCash.Services;

public static class PrinterSettings
{
    private const string PrinterNameHintKey = "printer_name_hint";
    private const string PrinterMacAddressKey = "printer_mac_address";

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
}
