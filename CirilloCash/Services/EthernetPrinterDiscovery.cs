using System.Collections.Concurrent;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
#if ANDROID
using Android.Content;
using Android.Net.Wifi;
#endif

namespace CirilloCash.Services;

public sealed class EthernetPrinterDiscovery
{
    public const int ProbePort = 9100;

    public sealed record DiscoveryReport(
        IReadOnlyList<string> ResponsiveHosts,
        IReadOnlyList<string> ScannedSubnets,
        int HostsProbed);

    public async Task<DiscoveryReport> DiscoverAsync(int perHostTimeoutMs = 2000, int totalTimeoutMs = 20000, CancellationToken ct = default)
    {
        var subnets = GetLocalSubnets();
        if (subnets.Count == 0)
        {
            return new DiscoveryReport(Array.Empty<string>(), Array.Empty<string>(), 0);
        }

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(totalTimeoutMs);

        var responsive = new ConcurrentBag<string>();
        var concurrency = new SemaphoreSlim(16);
        var tasks = new List<Task>();
        var probed = 0;

        foreach (var subnet in subnets)
        {
            for (var last = 1; last <= 254; last++)
            {
                if (last == subnet.SelfLastOctet)
                {
                    continue;
                }

                var host = $"{subnet.Prefix}.{last}";
                Interlocked.Increment(ref probed);
                tasks.Add(ProbeAsync(host, perHostTimeoutMs, responsive, concurrency, deadline.Token));
            }
        }

        try { await Task.WhenAll(tasks); }
        catch { /* deadline reached */ }

        var hosts = responsive.Distinct().OrderBy(IpSortKey).ToList();
        var subnetLabels = subnets.Select(s => $"{s.Prefix}.0/24 (mio IP: {s.Prefix}.{s.SelfLastOctet})").ToList();
        return new DiscoveryReport(hosts, subnetLabels, probed);
    }

    private static async Task ProbeAsync(string host, int timeoutMs, ConcurrentBag<string> bag, SemaphoreSlim sem, CancellationToken ct)
    {
        try { await sem.WaitAsync(ct); }
        catch (OperationCanceledException) { return; }

        try
        {
            using var client = new TcpClient();
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(timeoutMs);
            try
            {
                await client.ConnectAsync(host, ProbePort, cts.Token);
                if (client.Connected)
                {
                    bag.Add(host);
                }
            }
            catch
            {
                // host non raggiungibile o porta chiusa
            }
        }
        finally
        {
            sem.Release();
        }
    }

    private sealed record SubnetInfo(string Prefix, int SelfLastOctet);

    private static List<SubnetInfo> GetLocalSubnets()
    {
        var subnets = new Dictionary<string, SubnetInfo>(StringComparer.OrdinalIgnoreCase);

        foreach (var ip in EnumeratePrivateIPv4())
        {
            var b = ip.GetAddressBytes();
            var key = $"{b[0]}.{b[1]}.{b[2]}";
            if (!subnets.ContainsKey(key))
            {
                subnets[key] = new SubnetInfo(key, b[3]);
            }
        }

        return subnets.Values.ToList();
    }

    private static IEnumerable<IPAddress> EnumeratePrivateIPv4()
    {
        var seen = new HashSet<string>();

        // Prima sorgente: NetworkInterface (cross-platform)
        IEnumerable<NetworkInterface> nics;
        try { nics = NetworkInterface.GetAllNetworkInterfaces(); }
        catch { nics = Array.Empty<NetworkInterface>(); }

        foreach (var nic in nics)
        {
            if (nic.OperationalStatus != OperationalStatus.Up) continue;
            if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;

            IEnumerable<UnicastIPAddressInformation> unicast;
            try { unicast = nic.GetIPProperties().UnicastAddresses; }
            catch { continue; }

            foreach (var ua in unicast)
            {
                if (ua.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                if (!IsPrivateIPv4(ua.Address)) continue;
                if (seen.Add(ua.Address.ToString()))
                {
                    yield return ua.Address;
                }
            }
        }

#if ANDROID
        // Seconda sorgente: Android WifiManager (fallback affidabile sul Wi-Fi)
        var wifiIp = TryGetAndroidWifiIp();
        if (wifiIp is not null && IsPrivateIPv4(wifiIp) && seen.Add(wifiIp.ToString()))
        {
            yield return wifiIp;
        }
#endif
    }

#if ANDROID
    private static IPAddress? TryGetAndroidWifiIp()
    {
        try
        {
            var ctx = global::Android.App.Application.Context;
            var wifi = (WifiManager?)ctx.GetSystemService(Context.WifiService);
            var raw = wifi?.ConnectionInfo?.IpAddress ?? 0;
            if (raw == 0) return null;
            // WifiManager ritorna l'IP in little-endian (byte invertiti rispetto a network order)
            var bytes = new[]
            {
                (byte)(raw & 0xFF),
                (byte)((raw >> 8) & 0xFF),
                (byte)((raw >> 16) & 0xFF),
                (byte)((raw >> 24) & 0xFF)
            };
            return new IPAddress(bytes);
        }
        catch
        {
            return null;
        }
    }
#endif

    private static bool IsPrivateIPv4(IPAddress ip)
    {
        var b = ip.GetAddressBytes();
        return b[0] == 10
            || (b[0] == 172 && b[1] >= 16 && b[1] <= 31)
            || (b[0] == 192 && b[1] == 168);
    }

    private static int IpSortKey(string ip)
    {
        var parts = ip.Split('.');
        return parts.Length == 4 && int.TryParse(parts[3], out var n) ? n : 0;
    }
}
