# Ayn — العين

**Ayn** ("the eye" in Arabic) is a free, open-source Windows companion by **SabrWare**.
It places a small animated notch at the top of your screen with eyes, widgets and modules.
Everything stays on your PC — no account, no subscription, no license key, no purchase.

## Download

**One-click installer (Windows x64):** [Download Ayn-Setup.msi](https://github.com/salahuddinselim/Ayn/raw/main/Installer/Ayn-Setup.msi)

Double-click the downloaded `Ayn-Setup.msi` and follow the installer. A Start Menu entry and
an optional desktop shortcut are created. No administrator rights are required.

- Source and releases: <https://github.com/salahuddinselim/Ayn>
- Latest release page: <https://github.com/salahuddinselim/Ayn/releases/latest>

## Credit and acknowledgements

Ayn's interface and original module set were reconstructed from the shipped Windows app
**Vayli** by **Modi Studio** (Microsoft Store, version 1.1.62.0) using static reverse
engineering. Full credit for the original design, eye renderer, edge-light effects,
notch behaviour and module architecture goes to **Modi Studio**. Ayn is a separate,
independent, free and open-source rebuild by SabrWare, with the original commercial
licensing, activation and payment flows removed and new personal features added.

Thanks also to the open-source projects used by Ayn: WiX Toolset (installer), QRCoder
(QR codes), Microsoft.Data.Sqlite (local OpenCode usage), LibreOffice (optional document
conversion) and the .NET/WPF platform.

## Features

- **The notch** — an always-on-top pill with two animated eyes that follow the cursor.
- **Automatic emotions** — the eyes react on their own to time of day, battery, focus,
  idle state and activity: happy, sad, sleepy, surprised, ego and cry. No settings toggle.
- **Pets** — Ditto, Cat, Husky, Pikachu, Doraemon, ChatGPT-style dots, Grok and Robot,
  drawn and animated in-app with no downloads required.
- **Home** — screen time, CPU/RAM/disk, weather, focus timer, clipboard, notes and live
  internet speed.
- **Breaks & Meals** — a nudge after configurable computer time, plus lunch and dinner reminders.
- **Salat times** — calculated from your city or IP-detected area.
- **Network Guard** — a local DNS sinkhole blocking adult sites, betting sites/apps and ads.
- **QR & Wi-Fi Share** — make a QR for any text, generate a Wi-Fi QR from saved networks,
  and serve it on your LAN so other devices can scan it.
- **To-Do List** — add, complete and remove tasks; stored locally.
- **File Converter** — images/PDF to JPG, PNG, PDF, ICO, BMP, GIF and TIFF.
- **Doc Converter** — documents to PDF, DOCX, XLSX, PPTX, RTF, TXT, HTML, ODT and CSV
  (uses LibreOffice if installed).
- **AI token usage** — totals from local Claude Code, Codex and OpenCode sessions.

## Where things are

Open **Settings** from the notch gear icon. Your features have their own sidebar pages:
**Breaks & Meals**, **Salat Times**, **Network Guard**, **To-Do List**, **Wi-Fi QR**,
**Pets**, plus the standard Notch and Appearance pages.

To share Wi-Fi: Settings → **Wi-Fi QR** → **Open in notch** → choose your network →
**Make Wi-Fi QR** → optionally **Share on LAN** (`http://<your-pc>:8787`).

## Build from source

Requirements: Windows 10 1809 or newer, .NET 8 SDK, x64.

```powershell
git clone https://github.com/salahuddinselim/Ayn.git
cd Ayn
dotnet build src\Ayn\Ayn.csproj -c Release
dotnet run --project src\Ayn\Ayn.csproj
```

Build the installer:

```powershell
dotnet tool install -g wix --version 5.0.2
dotnet publish src\Ayn\Ayn.csproj -c Release -r win-x64 --self-contained true -o AynSetup\app
powershell -File Installer\generate-files.ps1
wix build Installer\Product.wxs Installer\Files.wxs -o Installer\Ayn-Setup.msi
```

## Privacy and data

Settings and data live in `%LOCALAPPDATA%\Ayn`. Nothing is uploaded. Network requests are
limited to optional city/IP lookup for prayer times and the LAN share you explicitly start.
Network Guard listens on UDP 5354; point your router DNS at this PC (and forward UDP 53 to
5354 if needed) to protect every device on the Wi-Fi.

## License

MIT — see [LICENSE](LICENSE). Free for personal and commercial use, modification and redistribution.

Made by SabrWare. Ayn / العين.
