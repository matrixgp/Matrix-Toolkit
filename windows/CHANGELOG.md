# Matrix Toolkit — historie sestavení

Každá vydaná uživatelská úprava dostává další číslo buildu. Opakované sestavení stejné úpravy číslo nemění. Číslo je společně nastavené v `Directory.Build.props` a používá se v titulku okna, přehledu aplikace, verzi EXE, exportovaných TXT výsledcích, názvech exportů a distribučním ZIPu. Při další úpravě zvyšte `MatrixBuild` z 4 na 5 a přidejte záznam níže.

## Build 004 — 26. 10. 2026

- Vlastní ikona Matrix Toolkit: zelený monogram M na tmavém podkladu.
- Ikona vložená do EXE a hlavního okna pro zobrazení v Průzkumníku, titulku, hlavním panelu a přepínači oken.
- ICO obsahuje devět rozlišení od 16 do 256 pixelů. Součástí zdrojů je také SVG a PNG náhled.

## Build 003 — 26. 10. 2026

- V tabulce průzkumu sítě je u zařízení s nalezeným otevřeným TCP portem 80 tlačítko **Otevřít web**.
- Kliknutí otevře `http://IP-adresa/` ve výchozím prohlížeči. Stránky se neotevírají automaticky.
- Pokud prohlížeč nelze spustit, aplikace zobrazí adresu pro ruční otevření.

## Build 002 — 21. 10. 2026

- Nový název Matrix Toolkit a číslování sestavení.
- Windows ikony u nástrojů a akcí, barevný akcent každé karty.
- Lokální IPv4 průzkum podle rozsahu nebo CIDR, například `192.168.0.1/23`.
- Volba TCP portů seznamem nebo rozsahem a nastavení časového limitu.
- Ověřování TCP i u zařízení bez odpovědi na ping, průběh a tabulka výsledků.
- Zastavení zachovává částečné výsledky. Export do TXT a CSV.

## Build 001 — 21. 10. 2026

Původní první desktopová verze pod názvem L0p4 Toolkit. DNS, WHOIS, IP informace, ping, ověření jednoho TCP portu a místní adaptéry. Zpětně označeno jako Build 001; původní EXE toto označení ještě neobsahuje.
