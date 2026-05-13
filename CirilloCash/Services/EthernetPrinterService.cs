using System.Net.Sockets;
using System.Text;

namespace CirilloCash.Services;

public sealed class EthernetPrinterService
{
    private const int ConnectTimeoutMs = 5000;
    private const int WriteTimeoutMs = 5000;

    // Formato ispirato a fantafestando: 40 caratteri per riga (Font A), separator 32 trattini
    public const int LineWidth = 40;
    public const int SeparatorWidth = 32;
    public const int LabelWidth = 24;
    public const int AmountWidth = LineWidth - LabelWidth;

    // Codepage CP858 (multilingual + Euro). Su Epson ESC/POS = pagina 19.
    private const int CodepageEpson = 19;
    // Tabella internazionale per accenti italiani = 6 (Italy)
    private const int InternationalCharsetItaly = 6;

    private static readonly byte[] EscInit = { 0x1B, 0x40 };
    private static readonly byte[] SelectCodepage = { 0x1B, 0x74, (byte)CodepageEpson };
    private static readonly byte[] SelectIntlCharset = { 0x1B, 0x52, (byte)InternationalCharsetItaly };
    private static readonly byte[] AlignLeft = { 0x1B, 0x61, 0x00 };
    private static readonly byte[] AlignCenter = { 0x1B, 0x61, 0x01 };
    private static readonly byte[] SizeNormal = { 0x1D, 0x21, 0x00 };
    private static readonly byte[] SizeDouble = { 0x1D, 0x21, 0x11 };
    private static readonly byte[] BoldOn = { 0x1B, 0x45, 0x01 };
    private static readonly byte[] BoldOff = { 0x1B, 0x45, 0x00 };
    private static readonly byte[] FeedAndCut = { 0x0A, 0x0A, 0x0A, 0x0A, 0x1D, 0x56, 0x01 };

    private static readonly Encoding ReceiptEncoding = ResolveReceiptEncoding();

    public async Task<PrinterResult> PrintAsync(string text, string host, int port)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return PrinterResult.Fail("Nessun contenuto da stampare.");
        }

        var payload = BuildPlainTextPayload(text);
        return await SendAsync(payload, host, port);
    }

    public async Task<PrinterResult> PrintReceiptAsync(ReceiptDocument document, string host, int port)
    {
        if (document.Items.Count == 0)
        {
            return PrinterResult.Fail("Nessun articolo da stampare.");
        }

        var payload = BuildReceiptPayload(document);
        return await SendAsync(payload, host, port);
    }

    public async Task<PrinterResult> PrintReceiptsAsync(IEnumerable<ReceiptDocument> documents, string host, int port)
    {
        var docs = documents.Where(d => d.Items.Count > 0).ToList();
        if (docs.Count == 0)
        {
            return PrinterResult.Fail("Nessun articolo da stampare.");
        }

        var combined = new List<byte>(1024);
        foreach (var doc in docs)
        {
            combined.AddRange(BuildReceiptPayload(doc));
        }

        return await SendAsync(combined.ToArray(), host, port);
    }

    private async Task<PrinterResult> SendAsync(byte[] payload, string host, int port)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return PrinterResult.Fail("Host stampante non configurato.");
        }

        if (port <= 0 || port > 65535)
        {
            return PrinterResult.Fail($"Porta TCP non valida: {port}.");
        }

        try
        {
            using var client = new TcpClient();
            client.SendTimeout = WriteTimeoutMs;

            using var connectCts = new CancellationTokenSource(ConnectTimeoutMs);
            try
            {
                await client.ConnectAsync(host, port, connectCts.Token);
            }
            catch (OperationCanceledException)
            {
                return PrinterResult.Fail($"Timeout connessione a {host}:{port}.");
            }

            await using var stream = client.GetStream();
            stream.WriteTimeout = WriteTimeoutMs;

            await stream.WriteAsync(payload);
            await stream.FlushAsync();

            return PrinterResult.Ok($"Stampa inviata a {host}:{port}.");
        }
        catch (SocketException ex)
        {
            return PrinterResult.Fail($"Errore di rete verso {host}:{port}: {ex.Message}");
        }
        catch (Exception ex)
        {
            return PrinterResult.Fail($"Errore stampa Ethernet: {ex.Message}");
        }
    }

    private static byte[] BuildPlainTextPayload(string text)
    {
        var buffer = new EscPosBuilder();
        buffer.Append(EscInit);
        buffer.Append(SelectCodepage);
        buffer.Append(SelectIntlCharset);
        buffer.Append(AlignLeft);

        foreach (var line in NormalizeLines(text))
        {
            buffer.WriteLine(line);
        }

        buffer.Append(FeedAndCut);
        return buffer.ToArray();
    }

    private static byte[] BuildReceiptPayload(ReceiptDocument doc)
    {
        var b = new EscPosBuilder();
        b.Append(EscInit);
        b.Append(SelectCodepage);
        b.Append(SelectIntlCharset);

        // Header centrato in double-size
        b.Append(AlignCenter);
        b.Append(SizeDouble);
        if (!string.IsNullOrWhiteSpace(doc.Title))
        {
            b.WriteLine(doc.Title.ToUpperInvariant());
        }
        if (!string.IsNullOrWhiteSpace(doc.Subtitle))
        {
            b.WriteLine(doc.Subtitle.ToUpperInvariant());
        }
        b.Append(SizeNormal);

        if (!string.IsNullOrWhiteSpace(doc.SectionLabel))
        {
            b.Append(BoldOn);
            b.Append(SizeDouble);
            b.WriteLine(doc.SectionLabel!.ToUpperInvariant());
            b.Append(SizeNormal);
            b.Append(BoldOff);
        }

        b.WriteLine(Separator());

        // Items: per ogni articolo "QTY x NOME" in double-size, poi prezzo unit + totale riga
        b.Append(AlignLeft);
        foreach (var item in doc.Items)
        {
            var title = $"{item.Quantity}x {item.Name}";
            b.Append(SizeDouble);
            b.WriteLine(title);
            b.Append(SizeNormal);

            var left = $"{item.Quantity} x {item.UnitPrice:0.00} EUR";
            var right = $"{item.LineTotal:0.00} EUR";
            b.WriteLine(PadColumns(left, right));
        }

        b.WriteLine(Separator());

        // Totale in double-size + bold
        b.Append(SizeDouble);
        b.Append(BoldOn);
        b.WriteLine(PadColumns("TOTALE -->", $"{doc.Total:0.00} EUR", labelWidth: 12, amountWidth: 8));
        b.Append(BoldOff);
        b.Append(SizeNormal);

        b.WriteLine(Separator());

        // Timestamp centrato
        b.Append(AlignCenter);
        b.WriteLine(doc.Timestamp.ToString("dd/MM/yyyy HH:mm:ss"));
        b.Append(AlignLeft);

        b.Append(FeedAndCut);
        return b.ToArray();
    }

    private static string Separator() => new('-', SeparatorWidth);

    private static string PadColumns(string left, string right, int? labelWidth = null, int? amountWidth = null)
    {
        var lw = labelWidth ?? LabelWidth;
        var aw = amountWidth ?? AmountWidth;
        var leftPadded = left.Length >= lw ? left[..lw] : left.PadRight(lw);
        var rightPadded = right.Length >= aw ? right[..aw] : right.PadLeft(aw);
        return leftPadded + rightPadded;
    }

    private static IEnumerable<string> NormalizeLines(string text)
    {
        return text.Replace("\r\n", "\n").Split('\n');
    }

    private static Encoding ResolveReceiptEncoding()
    {
        try
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            return Encoding.GetEncoding(858);
        }
        catch
        {
            return Encoding.ASCII;
        }
    }

    private sealed class EscPosBuilder
    {
        private readonly List<byte> buffer = new(512);

        public void Append(byte[] bytes) => buffer.AddRange(bytes);

        public void WriteLine(string text)
        {
            if (!string.IsNullOrEmpty(text))
            {
                buffer.AddRange(ReceiptEncoding.GetBytes(text));
            }
            buffer.Add(0x0A);
        }

        public byte[] ToArray() => buffer.ToArray();
    }
}
