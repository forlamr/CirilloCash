using System.Net.Sockets;
using System.Text;

namespace CirilloCash.Services;

public sealed class EthernetPrinterService
{
    private const int ConnectTimeoutMs = 5000;
    private const int WriteTimeoutMs = 5000;

    private static readonly byte[] EscPosInit = { 0x1B, 0x40 };
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
        var normalized = NormalizeForCp437(text).Replace("\r\n", "\n").Replace("\n", "\r\n");
        var body = Encoding.ASCII.GetBytes(normalized);

        var payload = new byte[EscPosInit.Length + body.Length + EscPosFeedAndCut.Length];
        Buffer.BlockCopy(EscPosInit, 0, payload, 0, EscPosInit.Length);
        Buffer.BlockCopy(body, 0, payload, EscPosInit.Length, body.Length);
        Buffer.BlockCopy(EscPosFeedAndCut, 0, payload, EscPosInit.Length + body.Length, EscPosFeedAndCut.Length);
        return payload;
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
}
