using System.Collections.ObjectModel;
using System.IO;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;

namespace MatrixToolkit;

public partial class MainWindow : Window
{
    private CancellationTokenSource? operation;
    private string tool = "dns";
    private string? report;
    private ScanResult? lastScan;
    private readonly ObservableCollection<ScanHost> scanRows = new();
    private readonly Dictionary<string, string> targets = new()
    {
        ["dns"] = "example.com", ["whois"] = "example.com", ["ip"] = "1.1.1.1",
        ["ping"] = "", ["tcp"] = "", ["scan"] = "192.168.0.1/23", ["local"] = ""
    };

    public MainWindow()
    {
        InitializeComponent();
        Title = BuildInfo.FullName;
        BuildText.Text = BuildInfo.Label;
        ScanGrid.ItemsSource = scanRows;
        ToolList.SelectedIndex = 0;
    }

    private void ToolChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsInitialized || ToolList.SelectedItem is not ListBoxItem item) return;
        if (e.RemovedItems.Count > 0) targets[tool] = TargetInput.Text;
        tool = (string)item.Tag;
        TargetInput.Text = targets[tool];
        var details = tool switch
        {
            "dns" => ("DNS záznamy", "Zjistěte IPv4 a IPv6 adresy domény pomocí systémového DNS resolveru.", "Doména", "Například example.com. Zobrazí adresní záznamy A a AAAA."),
            "whois" => ("WHOIS domény", "Zobrazte veřejné registrační údaje domény.", "Doména", "Dotaz na IANA a příslušný registr přes port 43. Registry používající pouze RDAP nejsou podporovány."),
            "ip" => ("Informace o IP", "Ověřte IP adresu a zjistěte její zpětný DNS záznam.", "IP adresa", "IPv4 nebo IPv6 adresa. Nástroj neurčuje zeměpisnou polohu."),
            "ping" => ("Ping", "Ověřte dostupnost zařízení čtyřmi ICMP dotazy.", "Doména nebo IP adresa", "Některá zařízení odpovědi na ping blokují."),
            "tcp" => ("Ověření TCP portu", "Ověřte připojení k jednomu konkrétnímu portu.", "Doména nebo IP adresa / port", "Číslo portu 1–65535. Výchozí port 443 používá HTTPS."),
            "scan" => ("Průzkum sítě", "Najděte zařízení v lokální síti a ověřte jejich otevřené TCP porty.", "Rozsah IPv4 nebo CIDR", "Např. 192.168.0.1-192.168.1.254 nebo 192.168.0.1/23. Porty: 22,80,443 nebo 1-1024. TCP se ověřuje i bez odpovědi na ping."),
            _ => ("Moje síť", "Prohlédněte si síťové adaptéry a nastavení tohoto počítače.", "Místní připojení", "Adresy, brány a DNS servery tohoto počítače. Pro vyhledání zařízení použijte Průzkum sítě.")
        };
        (ToolTitle.Text, ToolDescription.Text, InputLabel.Text, InputHint.Text) = details;
        var palette = tool switch
        {
            "dns" => ("#8CB8EB", "#1E3049", "\uE774"),
            "whois" => ("#B7A5DC", "#2C2942", "\uE8A5"),
            "ip" => ("#85BEC8", "#1D343F", "\uE946"),
            "ping" => ("#9BC8A4", "#24382F", "\uE9D9"),
            "tcp" => ("#D0B18B", "#3A3028", "\uE839"),
            "scan" => ("#85C7BA", "#203A39", "\uE721"),
            _ => ("#ABB8CC", "#2A3345", "\uE968")
        };
        Resources["Accent"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(palette.Item1));
        Resources["AccentSurface"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(palette.Item2));
        ToolIcon.Text = palette.Item3;
        InputGrid.Visibility = tool == "local" ? Visibility.Collapsed : Visibility.Visible;
        PortPanel.Visibility = tool == "tcp" ? Visibility.Visible : Visibility.Collapsed;
        InputGrid.ColumnDefinitions[1].Width = new GridLength(tool == "tcp" ? 110 : 0);
        ScanOptions.Visibility = tool == "scan" ? Visibility.Visible : Visibility.Collapsed;
        RunLabel.Text = tool switch { "scan" => "Prohledat síť", "local" => "Zobrazit připojení", _ => "Spustit dotaz" };
        report = null;
        lastScan = null;
        scanRows.Clear();
        ResultBox.Visibility = Visibility.Visible;
        ScanGrid.Visibility = Visibility.Collapsed;
        ResultBox.Text = tool == "scan"
            ? "Zadejte místní rozsah a porty. Nalezená zařízení se zobrazí v tabulce.\n\nLimit: 4096 adres, 1024 různých portů a 262144 kombinací IP × port.\nZařízení bez odpovědi nemusí být vypnuté; provoz může blokovat firewall."
            : "Připraveno k novému dotazu.";
        CopyButton.IsEnabled = SaveButton.IsEnabled = false;
        StatusText.Text = "Připraveno";
    }

    private string ReportHeader() => $"{BuildInfo.FullName} — {ToolTitle.Text}\n{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}\n{new string('─', 56)}\n";

    private async void RunClicked(object sender, RoutedEventArgs e)
    {
        if (operation is not null) return;
        using var pending = tool == "scan" ? new CancellationTokenSource() : new CancellationTokenSource(TimeSpan.FromSeconds(30));
        operation = pending;
        SetBusy(true);
        report = null;
        lastScan = null;
        scanRows.Clear();
        ResultBox.Visibility = Visibility.Visible;
        ScanGrid.Visibility = Visibility.Collapsed;
        ResultBox.Text = "Probíhá dotaz…";
        try
        {
            if (tool == "scan")
            {
                var request = ScanRequest.Parse(TargetInput.Text, ScanPortsInput.Text, ScanTimeoutInput.Text);
                var finished = false;
                var progress = new Progress<ScanProgress>(update =>
                {
                    if (finished || !ReferenceEquals(operation, pending)) return;
                    if (update.Host is { } host) scanRows.Add(host);
                    BusyBar.IsIndeterminate = update.Completed == 0;
                    BusyBar.Maximum = update.Total;
                    BusyBar.Value = Math.Max(BusyBar.Value, update.Completed);
                    StatusText.Text = update.Completed == 0 ? $"Ověřování dostupnosti {request.Range.Count} adres…"
                        : $"TCP ověření: {BusyBar.Value:N0} / {update.Total:N0} · Nalezeno zařízení: {scanRows.Count}";
                });
                ResultBox.Visibility = Visibility.Collapsed;
                ScanGrid.Visibility = Visibility.Visible;
                var result = await NetworkScanner.RunAsync(request, progress, pending.Token);
                finished = true;
                lastScan = result;
                scanRows.Clear();
                foreach (var host in result.Hosts) scanRows.Add(host);
                report = ReportHeader() + $"Rozsah: {IPv4Range.ToAddress(request.Range.First)}–{IPv4Range.ToAddress(request.Range.Last)}\n"
                    + $"TCP porty: {string.Join(',', request.Ports)}\nLimit: {request.TimeoutMs} ms / port\n"
                    + $"Stav: {(result.Canceled ? "ZASTAVENO — částečné výsledky" : "Dokončeno")}\nOvěřeno: {result.Completed}/{result.Total}\n"
                    + $"Chyby TCP: {result.Errors}\nNalezeno zařízení: {result.Hosts.Length}\n\nIP adresa\tDostupnost\tOtevřené TCP porty\n"
                    + string.Join('\n', result.Hosts.Select(host => $"{host.Ip}\t{host.Availability}\t{host.OpenPorts}"))
                    + "\n\nBez odpovědi neznamená vypnuté zařízení. Výsledky se týkají pouze zadaných TCP portů. Při zastavení mohou být řádky neúplné.";
                StatusText.Text = $"{(result.Canceled ? "Zastaveno · Částečné výsledky" : "Dokončeno")} · Zařízení: {result.Hosts.Length} · TCP: {result.Completed:N0}/{result.Total:N0}"
                    + (result.Errors > 0 ? $" · Chyby TCP: {result.Errors}" : "");
                if (result.Hosts.Length == 0)
                {
                    ScanGrid.Visibility = Visibility.Collapsed;
                    ResultBox.Visibility = Visibility.Visible;
                    ResultBox.Text = result.Canceled ? "Průzkum zastaven. Zatím nebylo nalezeno odpovídající zařízení."
                        : "Žádné zařízení neodpovědělo na ping ani na zadané TCP porty.\n\nOvěřte rozsah, zvolené porty, časový limit a připojení k místní síti. Firewall může odpovědi blokovat.";
                }
            }
            else
            {
                var result = await NetworkTools.RunAsync(tool, TargetInput.Text, PortInput.Text, pending.Token);
                pending.Token.ThrowIfCancellationRequested();
                report = ReportHeader() + result;
                ResultBox.Text = report;
                StatusText.Text = "Hotovo · Výsledek můžete zkopírovat nebo uložit.";
            }
        }
        catch (OperationCanceledException)
        {
            ResultBox.Text = "Dotaz byl zrušen nebo překročil časový limit 30 sekund.";
            StatusText.Text = "Dotaz nedokončen";
        }
        catch (Exception ex) when (ex is ArgumentException or SocketException or IOException or PingException or InvalidOperationException or NetworkInformationException)
        {
            ShowError(ex.Message);
        }
        catch (Exception) { ShowError("Nastala neočekávaná chyba. Zkuste dotaz zopakovat."); }
        finally
        {
            operation = null;
            SetBusy(false);
        }
    }

    private void ShowError(string message)
    {
        ResultBox.Visibility = Visibility.Visible;
        ScanGrid.Visibility = Visibility.Collapsed;
        ResultBox.Text = $"Dotaz se nepodařilo dokončit.\n{message}";
        StatusText.Text = "Zkontrolujte zadání a připojení k síti.";
    }

    private void SetBusy(bool busy)
    {
        ToolList.IsEnabled = TargetInput.IsEnabled = PortInput.IsEnabled = RunButton.IsEnabled = !busy;
        ScanPortsInput.IsEnabled = ScanTimeoutInput.IsEnabled = !busy;
        CancelButton.IsEnabled = busy;
        CopyButton.IsEnabled = SaveButton.IsEnabled = !busy && report is not null;
        BusyBar.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        if (busy) { BusyBar.IsIndeterminate = true; BusyBar.Value = 0; StatusText.Text = "Probíhá dotaz…"; }
    }

    private void CancelClicked(object sender, RoutedEventArgs e) => operation?.Cancel();
    private void TargetKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && operation is null) RunClicked(sender, e);
    }

    private void CopyClicked(object sender, RoutedEventArgs e)
    {
        if (report is null) return;
        try { Clipboard.SetText(report); StatusText.Text = "Výsledky jsou ve schránce."; }
        catch (System.Runtime.InteropServices.COMException) { StatusText.Text = "Schránka je právě obsazená. Zkuste kopírování znovu."; }
    }

    private void SaveClicked(object sender, RoutedEventArgs e)
    {
        if (report is null) return;
        var dialog = new SaveFileDialog
        {
            Filter = lastScan is null ? "Textový soubor (*.txt)|*.txt" : "Textový soubor (*.txt)|*.txt|Tabulka CSV (*.csv)|*.csv",
            FileName = $"matrix-build-{BuildInfo.Number:000}-{tool}-{DateTime.Now:yyyyMMdd-HHmmss}"
        };
        if (dialog.ShowDialog(this) != true) return;
        var text = lastScan is not null && dialog.FilterIndex == 2
            ? "IP adresa;Dostupnost;Otevřené TCP porty;Stav průzkumu\r\n" + string.Join("\r\n", lastScan.Hosts.Select(host => $"{host.Ip};{host.Availability};{host.OpenPorts};{(lastScan.Canceled ? "Částečné výsledky" : "Dokončeno")}"))
            : report;
        try { File.WriteAllText(dialog.FileName, text, new UTF8Encoding(true)); StatusText.Text = "Výsledky byly uloženy."; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { StatusText.Text = $"Soubor nelze uložit: {ex.Message}"; }
    }

    protected override void OnClosed(EventArgs e)
    {
        operation?.Cancel();
        base.OnClosed(e);
    }
}
