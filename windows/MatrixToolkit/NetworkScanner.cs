using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace MatrixToolkit;

public sealed record IPv4Range(uint First, uint Last)
{
    public int Count => checked((int)((ulong)Last - First + 1));
    public static IPAddress ToAddress(uint value) => new(new byte[] { (byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value });

    private static uint ParseAddress(string value)
    {
        var parts = value.Trim().Split('.');
        if (parts.Length != 4 || parts.Any(p => p.Length is < 1 or > 3 || p.Any(c => c is < '0' or > '9') || !byte.TryParse(p, out _)))
            throw new ArgumentException("Zadejte platnou IPv4 adresu se čtyřmi čísly, například 192.168.0.1.");
        return parts.Aggregate(0u, (result, part) => (result << 8) | byte.Parse(part, CultureInfo.InvariantCulture));
    }

    private static int LocalBlock(uint ip) => ip switch
    {
        >= 0x0A000000 and <= 0x0AFFFFFF => 1,
        >= 0xAC100000 and <= 0xAC1FFFFF => 2,
        >= 0xC0A80000 and <= 0xC0A8FFFF => 3,
        >= 0xA9FE0000 and <= 0xA9FEFFFF => 4,
        >= 0x7F000000 and <= 0x7FFFFFFF => 5,
        _ => 0
    };

    public static IPv4Range Parse(string input)
    {
        var value = input.Trim();
        uint first, last;
        if (value.Contains('/'))
        {
            var parts = value.Split('/');
            if (parts.Length != 2 || !int.TryParse(parts[1], out var prefix) || prefix is < 0 or > 32)
                throw new ArgumentException("CIDR musí obsahovat délku prefixu 0–32, například 192.168.0.1/23.");
            var address = ParseAddress(parts[0]);
            var mask = prefix == 0 ? 0u : uint.MaxValue << (32 - prefix);
            first = address & mask;
            last = first | ~mask;
            // RFC 3021 point-to-point ranges and /32 have no excluded endpoints.
            if (prefix < 31) { first++; last--; }
        }
        else if (value.Contains('-'))
        {
            var parts = value.Split('-');
            if (parts.Length != 2) throw new ArgumentException("Rozsah zadejte jako počáteční IP–koncová IP, oddělené znakem -.");
            first = ParseAddress(parts[0]);
            last = ParseAddress(parts[1]);
        }
        else first = last = ParseAddress(value);

        if (last < first) throw new ArgumentException("Koncová IP adresa musí být větší nebo rovna počáteční.");
        if ((ulong)last - first + 1 > 4096) throw new ArgumentException("Jeden průzkum může obsahovat nejvýše 4096 adres. Zmenšete rozsah.");
        if (LocalBlock(first) == 0 || LocalBlock(first) != LocalBlock(last))
            throw new ArgumentException("Průzkum je určen pro lokální IPv4: 10.x.x.x, 172.16–31.x.x, 192.168.x.x, 169.254.x.x nebo loopback.");
        return new(first, last);
    }
}

public sealed record ScanRequest(IPv4Range Range, int[] Ports, int TimeoutMs)
{
    public static int[] ParsePorts(string input)
    {
        var result = new SortedSet<int>();
        foreach (var raw in input.Split(','))
        {
            var part = raw.Trim();
            if (part.Length == 0) throw new ArgumentException("Zadejte porty oddělené čárkou, například 22,80,443 nebo 1-1024.");
            var boundaries = part.Split('-');
            if (boundaries.Length > 2) throw new ArgumentException("Neplatný rozsah portů.");
            var first = NetworkTools.ParsePort(boundaries[0]);
            var last = boundaries.Length == 2 ? NetworkTools.ParsePort(boundaries[1]) : first;
            if (last < first) throw new ArgumentException("Rozsah portů musí být vzestupný.");
            if (last - first >= 1024) throw new ArgumentException("Vyberte nejvýše 1024 různých portů.");
            for (var port = first; port <= last; port++) result.Add(port);
            if (result.Count > 1024) throw new ArgumentException("Vyberte nejvýše 1024 různých portů.");
        }
        return result.ToArray();
    }

    public static ScanRequest Parse(string range, string ports, string timeout)
    {
        var addresses = IPv4Range.Parse(range);
        var parsedPorts = ParsePorts(ports);
        if (!int.TryParse(timeout, out var delay) || delay is < 100 or > 5000)
            throw new ArgumentException("Časový limit musí být 100 až 5000 ms.");
        if ((long)addresses.Count * parsedPorts.Length > 262144)
            throw new ArgumentException("Jeden průzkum může ověřit nejvýše 262144 kombinací IP a portu. Zmenšete rozsah adres nebo portů.");
        return new(addresses, parsedPorts, delay);
    }
}

public sealed record ScanHost(uint Address, bool PingResponded, int[] Ports, bool TcpResponded)
{
    public string Ip => IPv4Range.ToAddress(Address).ToString();
    public string Availability => PingResponded ? "Odpovídá na ping" : "Odpovídá na TCP";
    public string OpenPorts => Ports.Length == 0 ? "Žádné ze zadaných" : string.Join(", ", Ports);
}

public sealed record ScanProgress(int Completed, int Total, ScanHost? Host = null);
public sealed record ScanResult(ScanHost[] Hosts, int Completed, int Total, bool Canceled, int Errors);

public static class NetworkScanner
{
    private sealed class HostState(uint address, int remaining)
    {
        public readonly uint Address = address;
        public readonly ConcurrentBag<int> Ports = new();
        public int Remaining = remaining;
        public bool PingResponded;
        public int TcpResponded;
        public bool IsResponsive => PingResponded || TcpResponded != 0;
        public ScanHost Snapshot() => new(Address, PingResponded, Ports.Order().ToArray(), TcpResponded != 0);
    }

    public static async Task<ScanResult> RunAsync(ScanRequest request, IProgress<ScanProgress>? progress, CancellationToken token)
    {
        // Validate even when called directly instead of through the UI parser.
        request = ScanRequest.Parse($"{IPv4Range.ToAddress(request.Range.First)}-{IPv4Range.ToAddress(request.Range.Last)}",
            string.Join(',', request.Ports), request.TimeoutMs.ToString(CultureInfo.InvariantCulture));
        var states = Enumerable.Range(0, request.Range.Count)
            .Select(offset => new HostState(request.Range.First + (uint)offset, request.Ports.Length)).ToArray();
        var total = states.Length * request.Ports.Length;
        var completed = 0;
        var errors = 0;
        progress?.Report(new(0, total));
        try
        {
            await Parallel.ForEachAsync(states, new ParallelOptions { MaxDegreeOfParallelism = 32, CancellationToken = token }, async (state, ct) =>
            {
                using var ping = new Ping();
                try
                {
                    var reply = await ping.SendPingAsync(IPv4Range.ToAddress(state.Address), TimeSpan.FromMilliseconds(request.TimeoutMs), new byte[32], null, ct);
                    state.PingResponded = reply.Status == IPStatus.Success;
                }
                catch (Exception ex) when (ex is PingException or SocketException or PlatformNotSupportedException or UnauthorizedAccessException) { /* TCP checks still run if ICMP is unavailable. */ }
            });

            // TCP probes run for every address, even when ping is blocked.
            var probes = states.SelectMany(state => request.Ports.Select(port => (State: state, Port: port)));
            await Parallel.ForEachAsync(probes, new ParallelOptions { MaxDegreeOfParallelism = 64, CancellationToken = token }, async (probe, ct) =>
            {
                using var client = new TcpClient(AddressFamily.InterNetwork);
                using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
                deadline.CancelAfter(request.TimeoutMs);
                try
                {
                    await client.ConnectAsync(IPv4Range.ToAddress(probe.State.Address), probe.Port, deadline.Token);
                    probe.State.Ports.Add(probe.Port);
                    Interlocked.Exchange(ref probe.State.TcpResponded, 1);
                }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested) { }
                catch (SocketException ex) when (ex.SocketErrorCode == SocketError.ConnectionRefused)
                { Interlocked.Exchange(ref probe.State.TcpResponded, 1); }
                catch (SocketException ex) when (ex.SocketErrorCode is SocketError.TimedOut or SocketError.HostUnreachable or SocketError.NetworkUnreachable or SocketError.HostDown) { }
                catch (SocketException) { Interlocked.Increment(ref errors); }
                var done = Interlocked.Increment(ref completed);
                var hostDone = Interlocked.Decrement(ref probe.State.Remaining) == 0;
                if (hostDone && probe.State.IsResponsive) progress?.Report(new(done, total, probe.State.Snapshot()));
                else if (done % 32 == 0 || done == total) progress?.Report(new(done, total));
            });
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        return new(states.Where(state => state.IsResponsive).Select(state => state.Snapshot()).OrderBy(host => host.Address).ToArray(),
            completed, total, token.IsCancellationRequested, errors);
    }
}
