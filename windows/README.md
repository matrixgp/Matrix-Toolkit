# Matrix Toolkit — Build 002

Samostatná aplikace v C# / WPF s českým grafickým rozhraním. Publikovaný balíček obsahuje .NET runtime: uživatel neinstaluje Python ani .NET. Aplikace běží bez administrátorských oprávnění. Cílová platforma: Windows 11 x64; sestavení je možné také pro Windows ARM64.

## Spuštění

Rozbalte `MatrixToolkit-Build-002-win-x64.zip` do vlastní složky a spusťte `MatrixToolkit.exe`. Aktuální build je v titulku a v levém dolním rohu aplikace. Nástroje mají vlastní Windows ikony a tlumené barevné akcenty při zachování společného vzhledu.

## Průzkum lokální sítě

1. Vyberte **Průzkum sítě**.
2. Zadejte rozsah `192.168.0.1-192.168.1.254` nebo CIDR `192.168.0.1/23`. Obě varianty ověří 510 adres. U CIDR se adresa převede na síť a vynechá se síťová a broadcast adresa; pro /31 a /32 zůstávají všechny adresy. Explicitní rozsah zahrnuje oba krajní body.
3. Zadejte porty: například `22,80,443,445,3389` nebo `1-1024`. Seznamy i rozsahy lze kombinovat, duplicity se odstraní. Výchozí seznam obsahuje 10 běžných TCP portů, nikoli všechny porty.
4. Nastavte limit jednoho pokusu v milisekundách, výchozí hodnota je 500 ms. Při pomalejším připojení nebo přes VPN jej zvyšte.
5. Klikněte na **Prohledat síť**. Tabulka se doplňuje po ověření zadaných portů u jednotlivých zařízení. Ukazuje IP, odpověď na ping/TCP a otevřené TCP porty. Stavový řádek ukazuje průběh; **Zastavit** zachová částečné výsledky.

Průzkum ověřuje TCP na všech adresách i tehdy, když zařízení blokuje ICMP ping. Za odpověď TCP se považuje úspěšné připojení nebo aktivní odmítnutí spojení. Zobrazení zařízení tedy neznamená, že některý z jeho zadaných portů musí být otevřený. Firewall může odpovědi blokovat nebo generovat; bez odpovědi neznamená vypnuté zařízení. Jde o test dostupnosti a TCP připojení, ne rozpoznávání skutečně běžící služby. UDP tato verze neověřuje.

Podporované adresy: privátní IPv4 `10.0.0.0/8`, `172.16.0.0/12`, `192.168.0.0/16`, link-local `169.254.0.0/16` a loopback `127.0.0.0/8`. Pro přístup do jiného místního segmentu musí existovat síťová cesta. Jeden průzkum může obsahovat nejvýše 4096 adres, 1024 různých portů a 262144 kombinací adres a portů. Používá nejvýše 64 souběžných TCP spojení. Celý průzkum nemá původní třicetisekundový limit ostatních nástrojů.

Výsledky lze kopírovat nebo uložit. U průzkumu je k dispozici TXT přehled a CSV tabulka se středníky pro Excel. Částečné výsledky jsou označené v obou formátech; jejich seznam portů nemusí být úplný.

## Ostatní nástroje

- DNS: adresy A a AAAA ze systémového resolveru.
- WHOIS: registrační údaje přes IANA a registr koncovky; vyžaduje TCP port 43. Registry používající pouze RDAP nejsou podporovány.
- Informace o IP: protokol, loopback a zpětné DNS; bez geolokace.
- Ping: čtyři ICMP dotazy a souhrn odezvy.
- TCP port: připojení na jeden port s limitem 5 sekund.
- Moje síť: místní adaptéry, IP adresy, brány a DNS.

Tato aplikace je přepsaná diagnostická část původního projektu. Linuxové instalátory, externí frameworky, DoS, phishing, reverse shell, odposlech a katalogy kamer nejsou součástí Windows aplikace. Původní Python se nespouští.

## Historie a sestavení

Historie úprav: [CHANGELOG.md](CHANGELOG.md). Číslo buildu je nastavené v `Directory.Build.props`. Při další vydané úpravě zvyšte `MatrixBuild` na další číslo; pouhé opakování sestavení číslo nemění.

Pro vývoj je potřeba .NET 10 SDK. Z PowerShellu ve složce `windows`:

```powershell
./publish.ps1
# Pro Windows ARM:
./publish.ps1 -Runtime win-arm64
```

Build 002 vytvoří `artifacts/Build-002/win-x64/MatrixToolkit.exe` a `artifacts/MatrixToolkit-Build-002-win-x64.zip`. Aplikace používá samorozbalovací balíček .NET; při prvním spuštění rozbalí nativní knihovny do uživatelské dočasné složky. Binární soubor není digitálně podepsaný.

Na Linuxu lze vytvořit Windows balíček s Windows targetingem:

```sh
dotnet publish MatrixToolkit/MatrixToolkit.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -p:DebugSymbols=false -o artifacts/Build-002/win-x64
```

## Ověření

```sh
dotnet run --project MatrixToolkit.Checks/MatrixToolkit.Checks.csproj
```

Kontroly používají lokální testovací server, nikoli průzkum skutečné okolní sítě. Ověřují validaci, IPv4/CIDR výpočty, rozsahy portů, otevřené a zavřené TCP porty, průběh, rušení a zachování výsledků.

Vzhled a chování skutečných oken, Windows ikony, schránku, ukládací dialog a provoz v reálné místní síti je potřeba ověřit přímo na Windows. Sestavení a síťové kontroly na Ubuntu nejsou vizuální test Windows aplikace.
