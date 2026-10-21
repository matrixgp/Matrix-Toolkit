using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using MatrixToolkit;

var passed = 0;
void Check(bool condition, string name)
{
    if (!condition) throw new Exception($"FAIL: {name}");
    Console.WriteLine($"PASS: {name}");
    passed++;
}

void Reject(Action action, string name)
{
    try { action(); }
    catch (ArgumentException) { Check(true, name); return; }
    throw new Exception($"FAIL: {name}: input was accepted");
}

Check(NetworkTools.NormalizeHost(" EXAMPLE.COM. ") == "example.com", "Domain normalization");
Check(NetworkTools.NormalizeHost("bücher.de") == "xn--bcher-kva.de", "IDN normalization");
Check(NetworkTools.NormalizeHost("::1") == "::1", "IPv6 acceptance");
foreach (var input in new[] { "", "https://example.com", "example.com/path", "example.com:443", "-bad.com", "a..com", "test\r\nother.com", new string('a', 64) + ".com" })
    Reject(() => NetworkTools.NormalizeHost(input), $"Reject malformed host: {input.Replace("\r", "\\r").Replace("\n", "\\n")}");
Reject(() => NetworkTools.NormalizeHost("127.0.0.1", true), "WHOIS rejects IP");
Reject(() => NetworkTools.NormalizeHost("localhost", true), "WHOIS requires full domain");
Check(NetworkTools.ParsePort("443") == 443 && NetworkTools.ParsePort("65535") == 65535, "TCP port boundaries");
foreach (var input in new[] { "0", "65536", "abc", "-1", "1.5" })
    Reject(() => NetworkTools.ParsePort(input), $"Reject invalid TCP port {input}");

using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
var dns = await NetworkTools.RunAsync("dns", "127.0.0.1", "", deadline.Token);
Check(dns.Contains("127.0.0.1") && dns.Contains("A   "), "DNS lookup for loopback");

using var listener = new TcpListener(IPAddress.Loopback, 0);
listener.Start();
var port = ((IPEndPoint)listener.LocalEndpoint).Port;
var acceptedTask = listener.AcceptTcpClientAsync(deadline.Token).AsTask();
var tcp = await NetworkTools.RunAsync("tcp", "127.0.0.1", port.ToString(), deadline.Token);
using var accepted = await acceptedTask;
Check(tcp.Contains("připojení navázáno"), "TCP connection to local listener");

using var canceled = new CancellationTokenSource();
canceled.Cancel();
try
{
    await NetworkTools.RunAsync("tcp", "127.0.0.1", port.ToString(), canceled.Token);
    throw new Exception("FAIL: Cancellation was ignored");
}
catch (OperationCanceledException) { Check(true, "Cancellation propagates"); }

try
{
    await NetworkTools.RunAsync("ip", "example.com", "", deadline.Token);
    throw new Exception("FAIL: IP tool accepted domain");
}
catch (ArgumentException) { Check(true, "IP tool rejects domain"); }

var cidr = IPv4Range.Parse("192.168.0.1/23");
var explicitRange = IPv4Range.Parse("192.168.0.1-192.168.1.254");
Check(cidr == explicitRange && cidr.Count == 510, "CIDR /23 equals requested explicit range (510 hosts)");
Check(IPv4Range.ToAddress(cidr.First).ToString() == "192.168.0.1"
    && IPv4Range.ToAddress(cidr.Last).ToString() == "192.168.1.254", "CIDR network/broadcast excluded");
Check(IPv4Range.Parse("192.168.1.0/31").Count == 2, "Both /31 endpoints retained");
Check(IPv4Range.Parse("192.168.1.42/32").Count == 1, "Single /32 retained");
Check(IPv4Range.Parse("10.1.2.3").Count == 1, "Single local IP accepted");
Check(IPv4Range.Parse("172.16.0.1-172.16.0.2").Count == 2, "Private 172.16 range accepted");
Check(IPv4Range.Parse("169.254.0.1").Count == 1, "Link-local address accepted");
foreach (var invalid in new[] { "192.168.1.2-192.168.0.1", "192.168.0.1/33", "192.168.0.1/-1", "192.168.0.1/x", "192.168.0.1/0", "192.168.0.0/16", "192.168.256.1", "127.1", "0x7f.0.0.1", "::1", "8.8.8.8", "172.15.0.1", "192.168.0.1-192.169.0.1", "" })
    Reject(() => IPv4Range.Parse(invalid), $"Reject invalid/local range {invalid}");
Check(ScanRequest.ParsePorts("443,22,80,22,100-102").SequenceEqual(new[] { 22,80,100,101,102,443 }), "Port list sorted and deduplicated");
Check(ScanRequest.ParsePorts("1-1024").Length == 1024, "1024 ports accepted");
Check(ScanRequest.ParsePorts("65535").Single() == 65535, "Highest port accepted");
foreach (var invalid in new[] { "", "0", "65536", "443,", "10-1", "1-1025", "1-1024,2048", "1-2-3", "80;443", "abc" })
    Reject(() => ScanRequest.ParsePorts(invalid), $"Reject malformed port list {invalid}");
Reject(() => ScanRequest.Parse("192.168.0.1/23", "1-1024", "500"), "Reject excessive number of probes");
Reject(() => ScanRequest.Parse("192.168.0.1", "80", "99"), "Reject too short timeout");
Reject(() => ScanRequest.Parse("192.168.0.1", "80", "5001"), "Reject too long timeout");

// Bind without listening to reserve a known closed port for this local check.
using var closedSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
closedSocket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
var closedPort = ((IPEndPoint)closedSocket.LocalEndPoint!).Port;
var updates = new ConcurrentQueue<ScanProgress>();
using var scanDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
var scan = await NetworkScanner.RunAsync(ScanRequest.Parse("127.0.0.1", $"{port},{closedPort}", "500"),
    new InlineProgress<ScanProgress>(updates.Enqueue), scanDeadline.Token);
Check(!scan.Canceled && scan.Completed == 2 && scan.Total == 2, "Scanner completes all local probes");
Check(scan.Hosts.Length == 1 && scan.Hosts[0].Ports.SequenceEqual(new[] { port }), "Scanner distinguishes open from closed port");
Check(scan.Hosts[0].TcpResponded && scan.Hosts[0].Ip == "127.0.0.1", "Scanner records TCP reachability");
Check(updates.Any(update => update.Completed == 2 && update.Host is not null), "Scanner reports discovered host and progress");

var canceledScan = await NetworkScanner.RunAsync(ScanRequest.Parse("127.0.0.1", port.ToString(), "500"), null, canceled.Token);
Check(canceledScan.Canceled && canceledScan.Completed == 0, "Pre-canceled scanner returns partial result");

using var midScan = new CancellationTokenSource();
var partialScan = await NetworkScanner.RunAsync(ScanRequest.Parse("127.0.0.1-127.0.0.128", $"{port},{closedPort}", "500"),
    new InlineProgress<ScanProgress>(update => { if (update.Host?.Ip == "127.0.0.1") midScan.Cancel(); }), midScan.Token);
Check(partialScan.Canceled && partialScan.Completed < partialScan.Total
    && partialScan.Hosts.Any(host => host.Ip == "127.0.0.1" && host.Ports.Contains(port)), "Mid-scan cancellation retains discovered open port");

Console.WriteLine($"{passed} checks passed.");

sealed class InlineProgress<T>(Action<T> handler) : IProgress<T>
{
    public void Report(T value) => handler(value);
}
