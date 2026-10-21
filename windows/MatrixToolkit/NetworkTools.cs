using System.Globalization;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;

namespace MatrixToolkit;

public static class NetworkTools
{
    public static string NormalizeHost(string input, bool domainOnly = false)
    {
        var value = input.Trim().TrimEnd('.');
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Zadejte doménu nebo IP adresu.");
        if (IPAddress.TryParse(value, out var ip))
        {
            if (domainOnly) throw new ArgumentException("Zadejte doménu, například example.com, nikoli IP adresu.");
            return ip.ToString();
        }
        try { value = new IdnMapping().GetAscii(value).ToLowerInvariant(); }
        catch (ArgumentException) { throw new ArgumentException("Doména má neplatný formát."); }
        if (value.Length > 253 || value.Split('.').Any(label => !Regex.IsMatch(label, @"\A[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?\z")))
            throw new ArgumentException("Zadejte samotnou doménu nebo IP adresu bez https://, cesty a čísla portu.");
        if (domainOnly && !value.Contains('.')) throw new ArgumentException("Zadejte úplnou doménu, například example.com.");
        return value;
    }

    public static int ParsePort(string input)
    {
        if (!int.TryParse(input, out var port) || port is < 1 or > 65535)
            throw new ArgumentException("Port musí být celé číslo od 1 do 65535.");
        return port;
    }

    public static async Task<string> RunAsync(string tool, string input, string port, CancellationToken token)
    {
        if (tool == "local") return await Task.Run(LocalNetwork, token);
        if (tool == "ip") return await IpInfoAsync(input, token);
        var host = NormalizeHost(input, tool == "whois");
        return tool switch
        {
            "dns" => await DnsAsync(host, token),
            "whois" => await WhoisAsync(host, token),
            "ping" => await PingAsync(host, token),
            "tcp" => await TcpAsync(host, ParsePort(port), token),
            _ => throw new ArgumentException("Neznámý nástroj.")
        };
    }

    private static async Task<string> DnsAsync(string host, CancellationToken token)
    {
        var addresses = await Dns.GetHostAddressesAsync(host, token);
        var lines = addresses.Distinct().Select(ip => $"{(ip.AddressFamily == AddressFamily.InterNetwork ? "A   " : "AAAA")}  {ip}");
        return $"Dotaz: {host}{Environment.NewLine}{Environment.NewLine}{string.Join(Environment.NewLine, lines)}";
    }

    private static async Task<string> IpInfoAsync(string input, CancellationToken token)
    {
        if (!IPAddress.TryParse(input.Trim(), out var ip)) throw new ArgumentException("Zadejte platnou IPv4 nebo IPv6 adresu.");
        var text = new StringBuilder().AppendLine($"IP adresa: {ip}")
            .AppendLine($"Protokol: {(ip.AddressFamily == AddressFamily.InterNetwork ? "IPv4" : "IPv6")}")
            .AppendLine($"Loopback: {(IPAddress.IsLoopback(ip) ? "ano" : "ne")}");
        try
        {
            var entry = await Dns.GetHostEntryAsync(ip.ToString(), token);
            text.AppendLine($"Zpětné DNS: {entry.HostName}");
        }
        catch (SocketException) { text.AppendLine("Zpětné DNS: záznam není dostupný."); }
        return text.ToString();
    }

    private static async Task<string> PingAsync(string host, CancellationToken token)
    {
        var addresses = await Dns.GetHostAddressesAsync(host, token);
        var address = addresses.FirstOrDefault(ip => ip.AddressFamily == AddressFamily.InterNetwork) ?? addresses.First();
        using var ping = new Ping();
        var output = new StringBuilder().AppendLine($"Zařízení: {host} ({address})").AppendLine();
        var times = new List<long>();
        for (var i = 1; i <= 4; i++)
        {
            token.ThrowIfCancellationRequested();
            var reply = await ping.SendPingAsync(address, TimeSpan.FromSeconds(2), new byte[32], null, token);
            if (reply.Status == IPStatus.Success)
            {
                times.Add(reply.RoundtripTime);
                output.AppendLine($"{i}/4  Odpověď od {reply.Address}: {reply.RoundtripTime} ms");
            }
            else output.AppendLine($"{i}/4  {reply.Status}");
            if (i < 4) await Task.Delay(400, token);
        }
        output.AppendLine().AppendLine($"Přijato: {times.Count}/4 · Ztráta: {(4 - times.Count) * 25} %");
        if (times.Count > 0) output.AppendLine($"Min / průměr / max: {times.Min()} / {times.Average():0.0} / {times.Max()} ms");
        return output.ToString();
    }

    private static async Task<string> TcpAsync(string host, int port, CancellationToken token)
    {
        using var client = new TcpClient();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        var timer = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            await client.ConnectAsync(host, port, timeout.Token);
            return $"Zařízení: {host}{Environment.NewLine}TCP port: {port}{Environment.NewLine}Stav: připojení navázáno{Environment.NewLine}Čas: {timer.ElapsedMilliseconds} ms";
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested)
        { return $"Zařízení: {host}{Environment.NewLine}TCP port: {port}{Environment.NewLine}Spojení se nepodařilo navázat do 5 sekund."; }
        catch (SocketException ex)
        { return $"Zařízení: {host}{Environment.NewLine}TCP port: {port}{Environment.NewLine}Spojení nenavázáno: {ex.Message}"; }
    }

    private static async Task<string> QueryWhoisAsync(string server, string query, CancellationToken token)
    {
        using var client = new TcpClient();
        await client.ConnectAsync(server, 43, token);
        await using var stream = client.GetStream();
        await stream.WriteAsync(Encoding.ASCII.GetBytes(query + "\r\n"), token);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        var output = new StringBuilder();
        var buffer = new char[4096];
        while (true)
        {
            var count = await reader.ReadAsync(buffer.AsMemory(), token);
            if (count == 0) break;
            output.Append(buffer, 0, count);
            if (output.Length > 262144) throw new IOException("WHOIS odpověď překročila limit 256 tisíc znaků.");
        }
        return output.ToString();
    }

    private static async Task<string> WhoisAsync(string host, CancellationToken token)
    {
        var iana = await QueryWhoisAsync("whois.iana.org", host.Split('.').Last(), token);
        var match = Regex.Match(iana, @"^whois:\s*(\S+)\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase);
        if (!match.Success) return "IANA pro tuto koncovku neuvádí WHOIS server. Registr může používat RDAP.";
        var server = NormalizeHost(match.Groups[1].Value, domainOnly: true);
        var response = await QueryWhoisAsync(server, host, token);
        return $"Doména: {host}{Environment.NewLine}Registr: {server}{Environment.NewLine}{Environment.NewLine}{response}";
    }

    private static string LocalNetwork()
    {
        var output = new StringBuilder();
        foreach (var adapter in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (adapter.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
            var properties = adapter.GetIPProperties();
            output.AppendLine(adapter.Name).AppendLine(adapter.Description)
                .AppendLine($"Stav: {adapter.OperationalStatus}")
                .AppendLine($"MAC: {adapter.GetPhysicalAddress()}");
            foreach (var entry in properties.UnicastAddresses) output.AppendLine($"  IP: {entry.Address}/{entry.PrefixLength}");
            foreach (var entry in properties.GatewayAddresses) output.AppendLine($"  Brána: {entry.Address}");
            foreach (var entry in properties.DnsAddresses) output.AppendLine($"  DNS: {entry}");
            output.AppendLine();
        }
        return output.Length == 0 ? "Nebyly nalezeny síťové adaptéry." : output.ToString();
    }
}
