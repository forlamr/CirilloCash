using System.Text;
#if ANDROID
using Android.Bluetooth;
using Android.Graphics;
using Java.Util;
#endif

namespace CirilloCash.Services;

public sealed class ThermalPrinterService
{
    public const string DefaultPrinterHint = "X5";
    public const string DefaultPrinterLanguage = "JL_SPP_RASTER";

#if ANDROID
    private const int PrinterWidthDots = 384;
    private const int PrinterWidthBytes = PrinterWidthDots / 8;
    // 58mm @ ~8 dot/mm; 10 mm tra scontrini, 30 mm finali per il taglio manuale.
    private const int X5InterDocSpacerDots = 80;
    private const int X5EndSpacerDots = 240;
    private const float X5NormalFontSize = 22f;
    private const float X5LargeFontSize = 30f;
    // From working X5 btsnoop logs: JL raster payloads are split into 662-byte chunks.
    private const int JlPayloadChunkBytes = 662;
    // Keep every ESC/POS raster strip inside a single JL payload to avoid cross-frame corruption.
    private const int MaxRasterRowsPerSegment = 13;
    // Observed on working X5 logs: ~55ms, 10ms, 10ms, ~4s, ~65ms between init frames.
    private static readonly int[] JlInitPostDelaysMs = { 55, 10, 10, 3950, 65 };
    private static readonly UUID SppUuid = UUID.FromString("00001101-0000-1000-8000-00805F9B34FB")!;
    private static readonly byte[][] JlInitFrames =
    {
        Convert.FromHexString("13FF07031E470379"),
        Convert.FromHexString("13FF07011D673979"),
        Convert.FromHexString("13FF07011D675379"),
        Convert.FromHexString("13FF07011D676979"),
        Convert.FromHexString("13FF13011D67531D49F0321B4079")
    };

    private static readonly byte[] JlTrailerFrame = Convert.FromHexString("13EF090A0A0A0A65");
#endif

    public async Task<PrinterResult> PrintAsync(
        string text,
        string printerNameHint = DefaultPrinterHint,
        string printerMacAddress = "")
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return PrinterResult.Fail("Nessun contenuto da stampare.");
        }

#if ANDROID
        return await Task.Run(() => PrintOnAndroid(text, printerNameHint, printerMacAddress));
#else
        await Task.CompletedTask;
        return PrinterResult.Fail("Stampa termica supportata solo su Android.");
#endif
    }

    public async Task<PrinterResult> PrintReceiptsAsync(
        IEnumerable<ReceiptDocument> documents,
        string printerNameHint = DefaultPrinterHint,
        string printerMacAddress = "")
    {
        var docs = documents.Where(d => d.Items.Count > 0).ToList();
        if (docs.Count == 0)
        {
            return PrinterResult.Fail("Nessun articolo da stampare.");
        }

#if ANDROID
        return await Task.Run(() => PrintReceiptsOnAndroid(docs, printerNameHint, printerMacAddress));
#else
        await Task.CompletedTask;
        return PrinterResult.Fail("Stampa termica supportata solo su Android.");
#endif
    }

#if ANDROID
    private static PrinterResult PrintOnAndroid(string text, string printerNameHint, string printerMacAddress)
    {
        try
        {
            var adapter = BluetoothAdapter.DefaultAdapter;
            if (adapter is null)
            {
                return PrinterResult.Fail("Bluetooth non disponibile sul dispositivo.");
            }

            if (!adapter.IsEnabled)
            {
                return PrinterResult.Fail("Bluetooth disattivato. Attivalo e riprova.");
            }

            var bondedDevices = adapter.BondedDevices;
            if (bondedDevices is null || bondedDevices.Count == 0)
            {
                return PrinterResult.Fail("Nessuna stampante associata. Associa prima il modello X5.");
            }

            var printer = FindBondedPrinter(adapter, printerNameHint, printerMacAddress);
            if (printer is null)
            {
                var names = string.Join(", ", bondedDevices
                    .Select(d => string.IsNullOrWhiteSpace(d.Name) ? d.Address : $"{d.Name} ({d.Address})"));
                return PrinterResult.Fail(
                    $"Stampante non trovata. Filtro nome='{printerNameHint}', MAC='{printerMacAddress}'. Dispositivi associati: {names}");
            }

            var secureResult = TryPrintSpp(printer, adapter, text, useInsecureSocket: false);
            if (secureResult.Success)
            {
                return secureResult;
            }

            var insecureResult = TryPrintSpp(printer, adapter, text, useInsecureSocket: true);
            if (insecureResult.Success)
            {
                return insecureResult;
            }

            return PrinterResult.Fail(
                $"Connessione alla stampante fallita. Tentativo secure: {secureResult.Message} | Tentativo insecure: {insecureResult.Message}");
        }
        catch (Exception ex)
        {
            return WrapAndroidPrintError(ex);
        }
    }

    private static PrinterResult WrapAndroidPrintError(Exception ex)
    {
        if (ex.Message.Contains("bluetooth", StringComparison.OrdinalIgnoreCase) ||
            ex.Message.Contains("permission", StringComparison.OrdinalIgnoreCase))
        {
            return PrinterResult.Fail(
                "Permessi Bluetooth mancanti o non completi. Concedi 'Dispositivi nelle vicinanze' nelle impostazioni app. " +
                $"Dettaglio: {ex.Message}");
        }

        return PrinterResult.Fail($"Errore stampa: {ex.Message}");
    }

    private static BluetoothDevice? FindBondedPrinter(
        BluetoothAdapter adapter,
        string printerNameHint,
        string printerMacAddress)
    {
        var bondedDevices = adapter.BondedDevices ?? Enumerable.Empty<BluetoothDevice>();

        if (!string.IsNullOrWhiteSpace(printerMacAddress))
        {
            var matchByMac = bondedDevices.FirstOrDefault(d =>
                !string.IsNullOrWhiteSpace(d.Address) &&
                d.Address.Equals(printerMacAddress, StringComparison.OrdinalIgnoreCase));
            if (matchByMac is not null)
            {
                return matchByMac;
            }
        }

        return bondedDevices.FirstOrDefault(d =>
            !string.IsNullOrWhiteSpace(d.Name) &&
            d.Name.Contains(printerNameHint, StringComparison.OrdinalIgnoreCase));
    }

    private static PrinterResult TryPrintSpp(
        BluetoothDevice printer,
        BluetoothAdapter adapter,
        string text,
        bool useInsecureSocket)
    {
        try
        {
            using var socket = useInsecureSocket
                ? printer.CreateInsecureRfcommSocketToServiceRecord(SppUuid)
                : printer.CreateRfcommSocketToServiceRecord(SppUuid);

            adapter.CancelDiscovery();
            socket.Connect();

            using var stream = socket.OutputStream
                ?? throw new InvalidOperationException("Output stream Bluetooth non disponibile.");
            WriteJlSppFrames(stream, text);

            return PrinterResult.Ok(
                $"Stampa inviata a {printer.Name} ({(useInsecureSocket ? "insecure" : "secure")}, protocollo {DefaultPrinterLanguage}).");
        }
        catch (Exception ex)
        {
            return PrinterResult.Fail($"{(useInsecureSocket ? "insecure" : "secure")}: {ex.Message}");
        }
    }

    private static void WriteJlSppFrames(Stream stream, string text)
    {
        var frames = BuildJlSppRasterFrames(text);
        WriteFrames(stream, frames);
    }

    private static void WriteJlSppFramesForReceipts(Stream stream, IReadOnlyList<ReceiptDocument> docs)
    {
        var frames = BuildJlSppRasterFramesForReceipts(docs);
        WriteFrames(stream, frames);
    }

    private static void WriteFrames(Stream stream, IReadOnlyList<JlFramePlan> frames)
    {
        foreach (var plan in frames)
        {
            stream.Write(plan.Frame, 0, plan.Frame.Length);
            stream.Flush();

            if (plan.DelayAfterMs > 0)
            {
                Thread.Sleep(plan.DelayAfterMs);
            }
        }
    }

    private static PrinterResult PrintReceiptsOnAndroid(List<ReceiptDocument> docs, string printerNameHint, string printerMacAddress)
    {
        try
        {
            var adapter = BluetoothAdapter.DefaultAdapter;
            if (adapter is null)
            {
                return PrinterResult.Fail("Bluetooth non disponibile sul dispositivo.");
            }
            if (!adapter.IsEnabled)
            {
                return PrinterResult.Fail("Bluetooth disattivato. Attivalo e riprova.");
            }

            var bondedDevices = adapter.BondedDevices;
            if (bondedDevices is null || bondedDevices.Count == 0)
            {
                return PrinterResult.Fail("Nessuna stampante associata. Associa prima il modello X5.");
            }

            var printer = FindBondedPrinter(adapter, printerNameHint, printerMacAddress);
            if (printer is null)
            {
                var names = string.Join(", ", bondedDevices
                    .Select(d => string.IsNullOrWhiteSpace(d.Name) ? d.Address : $"{d.Name} ({d.Address})"));
                return PrinterResult.Fail(
                    $"Stampante non trovata. Filtro nome='{printerNameHint}', MAC='{printerMacAddress}'. Dispositivi associati: {names}");
            }

            var secureResult = TryPrintReceiptsSpp(printer, adapter, docs, useInsecureSocket: false);
            if (secureResult.Success) return secureResult;

            var insecureResult = TryPrintReceiptsSpp(printer, adapter, docs, useInsecureSocket: true);
            if (insecureResult.Success) return insecureResult;

            return PrinterResult.Fail(
                $"Connessione alla stampante fallita. Tentativo secure: {secureResult.Message} | Tentativo insecure: {insecureResult.Message}");
        }
        catch (Exception ex)
        {
            return WrapAndroidPrintError(ex);
        }
    }

    private static PrinterResult TryPrintReceiptsSpp(BluetoothDevice printer, BluetoothAdapter adapter, IReadOnlyList<ReceiptDocument> docs, bool useInsecureSocket)
    {
        try
        {
            using var socket = useInsecureSocket
                ? printer.CreateInsecureRfcommSocketToServiceRecord(SppUuid)
                : printer.CreateRfcommSocketToServiceRecord(SppUuid);

            adapter.CancelDiscovery();
            socket.Connect();

            using var stream = socket.OutputStream
                ?? throw new InvalidOperationException("Output stream Bluetooth non disponibile.");
            WriteJlSppFramesForReceipts(stream, docs);

            return PrinterResult.Ok(
                $"Stampa inviata a {printer.Name} ({(useInsecureSocket ? "insecure" : "secure")}, protocollo {DefaultPrinterLanguage}).");
        }
        catch (Exception ex)
        {
            return PrinterResult.Fail($"{(useInsecureSocket ? "insecure" : "secure")}: {ex.Message}");
        }
    }

    private static IReadOnlyList<JlFramePlan> BuildJlSppRasterFramesForReceipts(IReadOnlyList<ReceiptDocument> docs)
    {
        var frames = new List<JlFramePlan>();
        for (var i = 0; i < JlInitFrames.Length; i++)
        {
            frames.Add(new JlFramePlan(JlInitFrames[i], JlInitPostDelaysMs[i]));
        }

        using var bitmap = RenderReceiptsBitmap(docs);
        var segments = new List<byte[]>();
        AddRasterSegments(segments, bitmap);

        for (var segmentIndex = 0; segmentIndex < segments.Count; segmentIndex++)
        {
            var segmentFrames = BuildJlFramesForPayload(segments[segmentIndex]);
            var isLastSegment = segmentIndex == segments.Count - 1;
            for (var frameIndex = 0; frameIndex < segmentFrames.Count; frameIndex++)
            {
                var isLastFrameOfSegment = frameIndex == segmentFrames.Count - 1;
                var delay = isLastSegment
                    ? (isLastFrameOfSegment ? 100 : 1)
                    : (isLastFrameOfSegment ? 28 : 1);
                frames.Add(new JlFramePlan(segmentFrames[frameIndex], delay));
            }
        }

        frames.Add(new JlFramePlan(JlTrailerFrame, 0));
        return frames;
    }

    private enum X5LineStyle
    {
        NormalLeft,
        NormalCenter,
        LargeCenter,
        LargeBoldCenter,
        LargeBoldLeft
    }

    private sealed record X5StyledLine(string Text, X5LineStyle Style);

    private static List<X5StyledLine> BuildLinesForDocument(ReceiptDocument doc)
    {
        var lines = new List<X5StyledLine>();

        if (!string.IsNullOrWhiteSpace(doc.Title))
            lines.Add(new(doc.Title.ToUpperInvariant(), X5LineStyle.LargeBoldCenter));
        if (!string.IsNullOrWhiteSpace(doc.Subtitle))
            lines.Add(new(doc.Subtitle.ToUpperInvariant(), X5LineStyle.LargeBoldCenter));
        if (!string.IsNullOrWhiteSpace(doc.SectionLabel))
            lines.Add(new(doc.SectionLabel!.ToUpperInvariant(), X5LineStyle.LargeBoldCenter));

        lines.Add(new(BuildSeparator(28), X5LineStyle.NormalLeft));

        foreach (var item in doc.Items)
        {
            lines.Add(new($"{item.Quantity}x {item.Name}", X5LineStyle.LargeBoldLeft));
            var left = $"{item.Quantity} x {item.UnitPrice:0.00} EUR";
            var right = $"{item.LineTotal:0.00} EUR";
            lines.Add(new(PadColumns(left, right, 18, 10), X5LineStyle.NormalLeft));
        }

        lines.Add(new(BuildSeparator(28), X5LineStyle.NormalLeft));
        lines.Add(new(PadColumns("TOT -->", $"{doc.Total:0.00} EUR", 10, 10), X5LineStyle.LargeBoldLeft));
        lines.Add(new(BuildSeparator(28), X5LineStyle.NormalLeft));
        lines.Add(new(doc.Timestamp.ToString("dd/MM/yyyy HH:mm:ss"), X5LineStyle.NormalCenter));

        return lines;
    }

    private static string BuildSeparator(int width) => new('-', width);

    private static string PadColumns(string left, string right, int labelWidth, int amountWidth)
    {
        var l = left.Length >= labelWidth ? left[..labelWidth] : left.PadRight(labelWidth);
        var r = right.Length >= amountWidth ? right[..amountWidth] : right.PadLeft(amountWidth);
        return l + r;
    }

    private static Bitmap RenderReceiptsBitmap(IReadOnlyList<ReceiptDocument> docs)
    {
        // Pre-misura tutte le righe per calcolare l'altezza totale.
        var allBlocks = new List<List<(X5StyledLine Line, global::Android.Graphics.Paint Paint, int LineHeight, float Baseline)>>();
        var totalHeight = 0;

        for (var d = 0; d < docs.Count; d++)
        {
            var doc = docs[d];
            var lines = BuildLinesForDocument(doc);
            var measured = new List<(X5StyledLine, global::Android.Graphics.Paint, int, float)>(lines.Count);
            foreach (var line in lines)
            {
                var paint = CreatePaintForStyle(line.Style);
                var m = paint.GetFontMetrics();
                var height = (int)Math.Ceiling(m.Bottom - m.Top + 4);
                var baseline = -m.Top + 2;
                measured.Add((line, paint, height, baseline));
                totalHeight += height;
            }
            allBlocks.Add(measured);

            if (d < docs.Count - 1)
            {
                totalHeight += X5InterDocSpacerDots;
            }
        }
        totalHeight += X5EndSpacerDots;
        totalHeight = Math.Max(1, totalHeight);

        var bitmap = Bitmap.CreateBitmap(PrinterWidthDots, totalHeight, Bitmap.Config.Argb8888!);
        using var canvas = new Canvas(bitmap);
        canvas.DrawColor(global::Android.Graphics.Color.White);

        var y = 0f;
        for (var d = 0; d < allBlocks.Count; d++)
        {
            foreach (var (line, paint, lineHeight, baseline) in allBlocks[d])
            {
                var isCenter = line.Style is X5LineStyle.NormalCenter
                                            or X5LineStyle.LargeCenter
                                            or X5LineStyle.LargeBoldCenter;
                float x;
                if (isCenter)
                {
                    var textWidth = paint.MeasureText(line.Text);
                    x = Math.Max(0, (PrinterWidthDots - textWidth) / 2f);
                }
                else
                {
                    x = 4f;
                }
                canvas.DrawText(line.Text, x, y + baseline, paint);
                y += lineHeight;
                paint.Dispose();
            }

            if (d < allBlocks.Count - 1)
            {
                y += X5InterDocSpacerDots;
            }
        }

        return bitmap;
    }

    private static global::Android.Graphics.Paint CreatePaintForStyle(X5LineStyle style)
    {
        var isLarge = style is X5LineStyle.LargeCenter
                              or X5LineStyle.LargeBoldCenter
                              or X5LineStyle.LargeBoldLeft;
        var isBold = style is X5LineStyle.LargeBoldCenter or X5LineStyle.LargeBoldLeft;

        var paint = new global::Android.Graphics.Paint
        {
            AntiAlias = false,
            Color = global::Android.Graphics.Color.Black,
            TextSize = isLarge ? X5LargeFontSize : X5NormalFontSize,
            FakeBoldText = isBold
        };
        paint.SetTypeface(Typeface.Monospace);
        return paint;
    }

    private static IReadOnlyList<JlFramePlan> BuildJlSppRasterFrames(string text)
    {
        var frames = new List<JlFramePlan>();
        for (var i = 0; i < JlInitFrames.Length; i++)
        {
            frames.Add(new JlFramePlan(JlInitFrames[i], JlInitPostDelaysMs[i]));
        }

        var segments = BuildEscPosRasterSegments(text);
        for (var segmentIndex = 0; segmentIndex < segments.Count; segmentIndex++)
        {
            var segmentFrames = BuildJlFramesForPayload(segments[segmentIndex]);
            var isLastSegment = segmentIndex == segments.Count - 1;
            for (var frameIndex = 0; frameIndex < segmentFrames.Count; frameIndex++)
            {
                var isLastFrameOfSegment = frameIndex == segmentFrames.Count - 1;
                var delay = isLastSegment
                    ? (isLastFrameOfSegment ? 100 : 1)
                    : (isLastFrameOfSegment ? 28 : 1);
                frames.Add(new JlFramePlan(segmentFrames[frameIndex], delay));
            }
        }

        frames.Add(new JlFramePlan(JlTrailerFrame, 0));
        return frames;
    }

    private static List<byte[]> BuildEscPosRasterSegments(string text)
    {
        var lines = GetPrintableLines(text);
        var segments = new List<byte[]>(Math.Max(lines.Length, 1));
        foreach (var line in lines)
        {
            using var bitmap = RenderLineBitmap(line);
            AddRasterSegments(segments, bitmap);
        }

        if (segments.Count == 0)
        {
            using var bitmap = RenderLineBitmap("Nessun dato");
            AddRasterSegments(segments, bitmap);
        }

        return segments;
    }

    private static void AddRasterSegments(List<byte[]> segments, Bitmap bitmap)
    {
        var rows = BuildRasterRows(bitmap);
        for (var offset = 0; offset < rows.Count; offset += MaxRasterRowsPerSegment)
        {
            var rowCount = Math.Min(MaxRasterRowsPerSegment, rows.Count - offset);
            var header = BuildEscPosRasterHeader(rowCount);
            var segment = new byte[header.Length + (rowCount * PrinterWidthBytes)];
            Buffer.BlockCopy(header, 0, segment, 0, header.Length);
            for (var index = 0; index < rowCount; index++)
            {
                Buffer.BlockCopy(rows[offset + index], 0, segment, header.Length + (index * PrinterWidthBytes), PrinterWidthBytes);
            }

            segments.Add(segment);
        }
    }

    private static List<byte[]> BuildJlFramesForPayload(byte[] payload)
    {
        var frames = new List<byte[]>();
        var fullChunkCount = payload.Length / JlPayloadChunkBytes;
        var remainderLength = payload.Length % JlPayloadChunkBytes;

        var offset = 0;
        for (; offset + JlPayloadChunkBytes <= payload.Length; offset += JlPayloadChunkBytes)
        {
            var chunk = new byte[JlPayloadChunkBytes];
            Buffer.BlockCopy(payload, offset, chunk, 0, JlPayloadChunkBytes);
            frames.Add(BuildJlFrame(chunk));
        }

        if (remainderLength > 0)
        {
            var remainderPayload = new byte[remainderLength];
            Buffer.BlockCopy(payload, offset, remainderPayload, 0, remainderLength);
            frames.Add(BuildJlContinuationFrame(remainderPayload));
        }

        if (fullChunkCount == 0 && remainderLength == 0)
        {
            frames.Add(BuildJlFrame(Array.Empty<byte>()));
        }

        return frames;
    }

    private static byte[] BuildEscPosRasterHeader(int heightDots)
    {
        return new byte[]
        {
            0x1D, 0x76, 0x30, 0x00,
            (byte)PrinterWidthBytes, 0x00,
            (byte)(heightDots & 0xFF),
            (byte)((heightDots >> 8) & 0xFF)
        };
    }

    private static List<byte[]> BuildRasterRows(Bitmap bitmap)
    {
        var rows = new List<byte[]>(bitmap.Height);
        for (var y = 0; y < bitmap.Height; y++)
        {
            var row = new byte[PrinterWidthBytes];
            for (var xByte = 0; xByte < PrinterWidthBytes; xByte++)
            {
                byte packed = 0;
                for (var bit = 0; bit < 8; bit++)
                {
                    var x = (xByte * 8) + bit;
                    if (IsBlack(bitmap.GetPixel(x, y)))
                    {
                        packed |= (byte)(1 << (7 - bit));
                    }
                }

                row[xByte] = packed;
            }

            rows.Add(row);
        }

        if (rows.Count == 0)
        {
            rows.Add(new byte[PrinterWidthBytes]);
        }

        return rows;
    }

    private static bool IsBlack(int pixel)
    {
        return global::Android.Graphics.Color.GetRedComponent(pixel) < 160 &&
               global::Android.Graphics.Color.GetGreenComponent(pixel) < 160 &&
               global::Android.Graphics.Color.GetBlueComponent(pixel) < 160;
    }

    private static byte[] BuildJlFrame(byte[] payload)
    {
        var lenWord = checked((ushort)(payload.Length * 2));
        var frame = new byte[4 + payload.Length + 1];
        frame[0] = 0x13;
        frame[1] = 0xEF;
        frame[2] = (byte)(lenWord & 0xFF);
        frame[3] = (byte)(lenWord >> 8);
        Buffer.BlockCopy(payload, 0, frame, 4, payload.Length);
        frame[^1] = 0x65;
        return frame;
    }

    private static byte[] BuildJlContinuationFrame(byte[] payload)
    {
        var lenWord = checked((ushort)(payload.Length * 2));
        var frame = new byte[5 + payload.Length + 1];
        frame[0] = 0x13;
        frame[1] = 0xFF;
        frame[2] = (byte)(lenWord & 0xFF);
        frame[3] = (byte)(lenWord >> 8);
        frame[4] = 0x01;
        Buffer.BlockCopy(payload, 0, frame, 5, payload.Length);
        frame[^1] = 0x79;
        return frame;
    }

    private sealed record JlFramePlan(byte[] Frame, int DelayAfterMs);

    private static Bitmap RenderLineBitmap(string line)
    {
        const float fontSize = 24f;
        const float leftPadding = 4f;
        const float topPadding = 4f;
        const float bottomPadding = 6f;

        using var measurePaint = new global::Android.Graphics.Paint
        {
            AntiAlias = false,
            Color = global::Android.Graphics.Color.Black,
            TextSize = fontSize
        };
        measurePaint.SetTypeface(Typeface.Monospace);

        var safeLine = string.IsNullOrEmpty(line) ? " " : line;
        var metrics = measurePaint.GetFontMetrics();
        var baseline = topPadding - metrics.Top;
        var contentHeight = baseline + metrics.Bottom + bottomPadding;
        var heightDots = Math.Max(1, (int)Math.Ceiling(contentHeight));

        var bitmap = Bitmap.CreateBitmap(PrinterWidthDots, heightDots, Bitmap.Config.Argb8888!);
        using var canvas = new Canvas(bitmap);
        canvas.DrawColor(global::Android.Graphics.Color.White);

        using var paint = new global::Android.Graphics.Paint(measurePaint);
        canvas.DrawText(safeLine, leftPadding, baseline, paint);
        return bitmap;
    }
#endif

    private static string[] GetPrintableLines(string text)
    {
        var lines = PreparePrinterText(text)
            .Replace("\r\n", "\n")
            .Split('\n', StringSplitOptions.None);

        if (lines.Length > 0 && lines[^1].Length == 0)
        {
            return lines.Take(lines.Length - 1).ToArray();
        }

        return lines;
    }

    private static string PreparePrinterText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return "Nessun dato";
        }

        return new string(text
            .Replace('\t', ' ')
            .Select(c => c <= 127 ? c : ' ')
            .ToArray());
    }
}

public sealed record PrinterResult(bool Success, string Message)
{
    public static PrinterResult Ok(string message) => new(true, message);
    public static PrinterResult Fail(string message) => new(false, message);
}
