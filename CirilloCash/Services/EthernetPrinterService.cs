using System.Net.Sockets;
using System.Text;
#if ANDROID
using AG = Android.Graphics;
#endif

namespace CirilloCash.Services;

public sealed class EthernetPrinterService
{
    private const int ConnectTimeoutMs = 5000;
    private const int WriteTimeoutMs = 5000;
    private const string ReceiptHeaderLine1 = "POLO ZEROSEI";
    private const string ReceiptHeaderLine2 = "DON CIRILLO PIZIO";

    private static readonly byte[] EscPosInit = { 0x1B, 0x40 };
    private static readonly byte[] EscPosAlignLeft = { 0x1B, 0x61, 0x00 };
    private static readonly byte[] EscPosAlignCenter = { 0x1B, 0x61, 0x01 };
    private static readonly byte[] EscPosFontA = { 0x1B, 0x4D, 0x00 };
    private static readonly byte[] EscPosFontB = { 0x1B, 0x4D, 0x01 };
    private static readonly byte[] EscPosBoldOn = { 0x1B, 0x45, 0x01 };
    private static readonly byte[] EscPosBoldOff = { 0x1B, 0x45, 0x00 };
    private static readonly byte[] EscPosSizeNormal = { 0x1D, 0x21, 0x00 };
    private static readonly byte[] EscPosSizeDoubleHeight = { 0x1D, 0x21, 0x01 };
    private static readonly byte[] EscPosSizeDoubleWidthHeight = { 0x1D, 0x21, 0x11 };
    private static readonly byte[] EscPosFeedAndCut = { 0x0A, 0x0A, 0x0A, 0x0A, 0x1D, 0x56, 0x00 };

    public async Task<PrinterResult> PrintAsync(string text, string host, int port)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return PrinterResult.Fail("Nessun contenuto da stampare.");
        }

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

            var payload = BuildEscPosPayload(text);
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

    private static byte[] BuildEscPosPayload(string text)
    {
        var normalized = NormalizeForCp437(text).Replace("\r\n", "\n");
        var lines = normalized.Split('\n').ToList();

        var printLogo = lines.Count >= 2 &&
                        lines[0].Trim().Equals(ReceiptHeaderLine1, StringComparison.OrdinalIgnoreCase) &&
                        lines[1].Trim().Equals(ReceiptHeaderLine2, StringComparison.OrdinalIgnoreCase);

        if (printLogo)
        {
            lines.RemoveRange(0, 2);
        }

        while (lines.Count > 0 && string.IsNullOrWhiteSpace(lines[^1]))
        {
            lines.RemoveAt(lines.Count - 1);
        }

#if ANDROID
        return BuildAndroidEscPosPayload(lines, printLogo);
#else
        return BuildTextOnlyEscPosPayload(lines, includeLogoPlaceholder: false);
#endif
    }

    private static string NormalizeForCp437(string text)
    {
        var sb = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            sb.Append(c switch
            {
                'à' or 'á' or 'â' or 'ä' or 'ã' => 'a',
                'è' or 'é' or 'ê' or 'ë' => 'e',
                'ì' or 'í' or 'î' or 'ï' => 'i',
                'ò' or 'ó' or 'ô' or 'ö' or 'õ' => 'o',
                'ù' or 'ú' or 'û' or 'ü' => 'u',
                'À' or 'Á' or 'Â' or 'Ä' or 'Ã' => 'A',
                'È' or 'É' or 'Ê' or 'Ë' => 'E',
                'Ì' or 'Í' or 'Î' or 'Ï' => 'I',
                'Ò' or 'Ó' or 'Ô' or 'Ö' or 'Õ' => 'O',
                'Ù' or 'Ú' or 'Û' or 'Ü' => 'U',
                '€' => 'E',
                _ => c
            });
        }

        return sb.ToString();
    }

#if ANDROID
    private static byte[] BuildAndroidEscPosPayload(IReadOnlyList<string> lines, bool printLogo)
    {
        var bytes = new List<byte>(2048);
        bytes.AddRange(EscPosInit);

        if (printLogo)
        {
            using var bitmap = RenderHeaderLogoBitmap();
            bytes.AddRange(BuildRasterCommand(bitmap));
            bytes.Add((byte)'\n');
        }

        bytes.AddRange(BuildTextCommands(lines, includeLogoPlaceholder: false));
        bytes.AddRange(EscPosFeedAndCut);
        return bytes.ToArray();
    }

    private static AG.Bitmap RenderHeaderLogoBitmap()
    {
        const int paperWidth = 384;
        const int bitmapHeight = 118;

        var bitmap = AG.Bitmap.CreateBitmap(paperWidth, bitmapHeight, AG.Bitmap.Config.Argb8888!);
        using var canvas = new AG.Canvas(bitmap);
        canvas.DrawColor(AG.Color.White);
        DrawHeaderLogo(canvas, 10);
        return bitmap;
    }

    private static void DrawHeaderLogo(AG.Canvas canvas, int top)
    {
        using var titlePaint = new AG.Paint(AG.PaintFlags.AntiAlias)
        {
            Color = AG.Color.Black,
            TextSize = 24,
            FakeBoldText = true,
            TextAlign = AG.Paint.Align.Left
        };

        using var subtitlePaint = new AG.Paint(AG.PaintFlags.AntiAlias)
        {
            Color = AG.Color.Black,
            TextSize = 12,
            TextAlign = AG.Paint.Align.Left
        };

        using var badgePaint = new AG.Paint(AG.PaintFlags.AntiAlias)
        {
            Color = AG.Color.Black
        };

        using var heartCutoutPaint = new AG.Paint(AG.PaintFlags.AntiAlias)
        {
            Color = AG.Color.White
        };

        var badgeCenterX = 48f;
        var badgeCenterY = top + 42f;
        canvas.DrawCircle(badgeCenterX, badgeCenterY, 30f, badgePaint);

        using var heartPath = new AG.Path();
        heartPath.MoveTo(badgeCenterX, badgeCenterY + 12f);
        heartPath.CubicTo(badgeCenterX - 18f, badgeCenterY - 2f, badgeCenterX - 18f, badgeCenterY - 20f, badgeCenterX, badgeCenterY - 8f);
        heartPath.CubicTo(badgeCenterX + 18f, badgeCenterY - 20f, badgeCenterX + 18f, badgeCenterY - 2f, badgeCenterX, badgeCenterY + 12f);
        canvas.DrawPath(heartPath, heartCutoutPaint);

        canvas.DrawCircle(badgeCenterX - 18f, badgeCenterY - 32f, 6f, badgePaint);
        canvas.DrawCircle(badgeCenterX + 12f, badgeCenterY - 38f, 8f, badgePaint);

        const float textStartX = 92f;
        var line1Y = top + 30f;
        var line2Y = top + 58f;
        var taglineY = top + 82f;

        canvas.DrawText(ReceiptHeaderLine1, textStartX, line1Y, titlePaint);
        canvas.DrawText(ReceiptHeaderLine2, textStartX, line2Y, titlePaint);
        canvas.DrawText("DAL 1901 CRESCIAMO CON IL CUORE", textStartX, taglineY, subtitlePaint);
    }

    private static byte[] BuildRasterCommand(AG.Bitmap bitmap)
    {
        var width = bitmap.Width;
        var height = bitmap.Height;
        var widthBytes = (width + 7) / 8;
        var data = new byte[8 + (widthBytes * height)];

        data[0] = 0x1D;
        data[1] = 0x76;
        data[2] = 0x30;
        data[3] = 0x00;
        data[4] = (byte)(widthBytes & 0xFF);
        data[5] = (byte)((widthBytes >> 8) & 0xFF);
        data[6] = (byte)(height & 0xFF);
        data[7] = (byte)((height >> 8) & 0xFF);

        var offset = 8;
        for (var y = 0; y < height; y++)
        {
            for (var xByte = 0; xByte < widthBytes; xByte++)
            {
                byte value = 0;
                for (var bit = 0; bit < 8; bit++)
                {
                    var x = (xByte * 8) + bit;
                    if (x >= width)
                    {
                        continue;
                    }

                    var pixel = bitmap.GetPixel(x, y);
                    var luminance =
                        (AG.Color.GetRedComponent(pixel) * 0.299) +
                        (AG.Color.GetGreenComponent(pixel) * 0.587) +
                        (AG.Color.GetBlueComponent(pixel) * 0.114);
                    if (luminance < 180)
                    {
                        value |= (byte)(0x80 >> bit);
                    }
                }

                data[offset++] = value;
            }
        }

        return data;
    }
#endif

    private static byte[] BuildTextOnlyEscPosPayload(IReadOnlyList<string> lines, bool includeLogoPlaceholder)
    {
        var bytes = new List<byte>(1024);
        bytes.AddRange(EscPosInit);
        bytes.AddRange(BuildTextCommands(lines, includeLogoPlaceholder));
        bytes.AddRange(EscPosFeedAndCut);
        return bytes.ToArray();
    }

    private static byte[] BuildTextCommands(IReadOnlyList<string> lines, bool includeLogoPlaceholder)
    {
        var bytes = new List<byte>(1024);

        if (includeLogoPlaceholder)
        {
            AppendStyledLine(bytes, ReceiptHeaderLine1, centered: true, bold: true, fontB: false, doubleHeight: true, doubleWidth: true);
            AppendStyledLine(bytes, ReceiptHeaderLine2, centered: true, bold: true, fontB: false, doubleHeight: true, doubleWidth: true);
        }

        foreach (var rawLine in lines)
        {
            var line = rawLine ?? string.Empty;
            var trimmed = line.Trim();

            if (trimmed.Length == 0)
            {
                bytes.Add((byte)'\n');
                continue;
            }

            if (trimmed.StartsWith("----------------", StringComparison.Ordinal))
            {
                AppendStyledLine(bytes, trimmed, centered: true, bold: false, fontB: false, doubleHeight: false, doubleWidth: false);
                continue;
            }

            var isCentered = line.StartsWith(" ", StringComparison.Ordinal);
            var isTimestamp = trimmed.Contains(':') && trimmed.Contains('-');
            AppendStyledLine(
                bytes,
                isCentered ? trimmed : line,
                centered: isCentered,
                bold: !isTimestamp && !line.Contains("  ", StringComparison.Ordinal),
                fontB: !isTimestamp,
                doubleHeight: true,
                doubleWidth: isTimestamp);
        }

        bytes.AddRange(EscPosAlignLeft);
        bytes.AddRange(EscPosFontA);
        bytes.AddRange(EscPosBoldOff);
        bytes.AddRange(EscPosSizeNormal);
        return bytes.ToArray();
    }

    private static void AppendStyledLine(
        List<byte> bytes,
        string text,
        bool centered,
        bool bold,
        bool fontB,
        bool doubleHeight,
        bool doubleWidth)
    {
        bytes.AddRange(centered ? EscPosAlignCenter : EscPosAlignLeft);
        bytes.AddRange(fontB ? EscPosFontB : EscPosFontA);
        bytes.AddRange(bold ? EscPosBoldOn : EscPosBoldOff);
        bytes.AddRange(doubleWidth
            ? EscPosSizeDoubleWidthHeight
            : doubleHeight
                ? EscPosSizeDoubleHeight
                : EscPosSizeNormal);
        bytes.AddRange(Encoding.ASCII.GetBytes(text));
        bytes.Add((byte)'\n');
        bytes.AddRange(EscPosBoldOff);
        bytes.AddRange(EscPosSizeNormal);
    }
}
