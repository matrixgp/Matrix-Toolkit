# Matrix Toolkit — Build 004

Matrix Toolkit is a standalone Windows desktop application for network diagnostics and local IPv4 discovery. It includes DNS and WHOIS lookups, IP information, ping, TCP port checks, and local network adapter details in a Czech graphical interface.

## Changes since Build 002

### Open device websites — Build 003

- Devices with a confirmed open TCP port **80** now show an **Open website** button (labelled **Otevřít web** in the application).
- Click it to open `http://DEVICE-IP/` in your default browser. Websites never open automatically.
- If the browser cannot be launched, the application shows the URL so you can open it manually.

### Application icon — Build 004

- Added a custom Matrix Toolkit icon: a green M on a dark background.
- Embedded the icon in the Windows executable and main window for use in File Explorer, the taskbar and the window switcher.
- Included nine icon sizes from 16 to 256 pixels, plus editable SVG artwork and a PNG preview in the source repository.

## Getting started

Download `MatrixToolkit-Build-004-win-x64.zip` from the release assets, extract it, and launch `MatrixToolkit.exe`.

**Target platform:** Windows 11 x64. No separate Python or .NET installation is required. The interface is currently in Czech.

## Validation

The Windows x64 build and all 66 existing automated checks passed on Ubuntu. All nine icon images were verified inside the executable. The graphical interface, Windows icon display and default-browser launch still require testing on Windows.

This remains an early desktop release. Local discovery supports IPv4 and TCP; UDP scanning is not included.
