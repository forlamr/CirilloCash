namespace CirilloCash.Services;

public sealed class ReceiptDocument
{
    public string Title { get; init; } = string.Empty;
    public string Subtitle { get; init; } = string.Empty;
    public string? SectionLabel { get; init; }
    public IReadOnlyList<ReceiptLineItem> Items { get; init; } = Array.Empty<ReceiptLineItem>();
    public double Total { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.Now;
}

public sealed record ReceiptLineItem(string Name, int Quantity, double UnitPrice, double LineTotal);
