# Matrix Toolkit

Nativní desktopová aplikace pro Windows s českým grafickým rozhraním. Funguje bez instalace Pythonu nebo .NET. Aktuální verze: **Build 002**.

## Funkce

- DNS záznamy A a AAAA a WHOIS domén.
- Informace o IP, zpětné DNS a ping.
- Ověření jednoho TCP portu.
- Průzkum lokální IPv4 sítě podle rozsahu nebo CIDR, například `192.168.0.1-192.168.1.254` nebo `192.168.0.1/23`.
- Volba TCP portů seznamem nebo rozsahem, tabulka nalezených zařízení, průběh a možnost zastavení.
- Přehled místních síťových adaptérů.
- Export výsledků do TXT a CSV.

Karty používají Windows ikony a jemné barevné akcenty. Číslo buildu je zobrazené v aplikaci a v názvech distribučních balíčků.

## Spuštění

Ve vydání Build 002 stáhněte `MatrixToolkit-Build-002-win-x64.zip`, rozbalte jej a spusťte `MatrixToolkit.exe`. Cílový systém: Windows 11 x64.

## Vývoj

Zdrojový kód je ve složce [windows](windows/README.md). Používá C#, WPF a .NET 10 SDK.

V PowerShellu z kořene repozitáře:

```powershell
dotnet run --project windows/MatrixToolkit.Checks/MatrixToolkit.Checks.csproj
./windows/publish.ps1
```

Podrobnosti, limity průzkumu a postup sestavení jsou v [návodu](windows/README.md). Historie změn: [CHANGELOG](windows/CHANGELOG.md).

## Stav ověření

Sestavení Windows x64 a 66 kontrol síťové logiky prošlo na Ubuntu. Vzhled oken a provoz v reálné lokální síti vyžadují ověření přímo na Windows. Testy průzkumu používají pouze lokální testovací server.

## Původ a licence

Matrix Toolkit vznikl přepisem diagnostické části projektu [L0p4 Toolkit](https://github.com/HaxL0p4/L0p4-Toolkit) do samostatné Windows aplikace. Toto úložiště obsahuje Windows aplikaci; původní Python toolkit a jeho ostatní moduly nejsou součástí.

Distribuováno pod [MIT licencí](LICENSE). Původní licenční a autorské oznámení je zachované.
