# Ayn — العين

**Ayn** ("the eye" in Arabic) is a free, open-source Windows desktop companion by **SabrWare**.
It puts a small animated notch at the top of your screen with eyes, widgets and modules, and
keeps everything on your PC. No account, no subscription, no license key, no purchase.

Repository: <https://github.com/salahuddinselim/Ayn>

## What it does

- **The notch** — a small always-on-top pill with two animated eyes that follow your cursor.
- **Eye emotions** — happy, sad, cry, ego, surprised, sleepy or neutral (Settings → Notch).
- **Pets** — Ditto, Cat, Husky, Pikachu, Doraemon, ChatGPT-style dots, Grok and Robot.
- **Home page** — screen time, CPU/RAM/disk, weather, focus timer, clipboard, notes and live internet speed.
- **Breaks & Meals** — a nudge after a configurable amount of computer time, plus lunch and dinner reminders.
- **Salat times** — calculated for your city/area. Type a city or press "Use my area" for IP-based detection.
- **Network Guard** — a local DNS sinkhole that blocks adult sites, betting sites/apps and ads on your Wi‑Fi.
- **QR & Wi‑Fi Share** — create a QR for any text, generate a Wi‑Fi QR from a saved network, and serve it on your LAN.
- **To-Do List** — add, complete and remove tasks; stored locally.
- **File Converter** — images and PDF to JPG, PNG, PDF, ICO, BMP, GIF and TIFF.
- **Doc Converter** — documents to PDF, DOCX, XLSX, PPTX, RTF, TXT, HTML, ODT and CSV (uses LibreOffice if installed).
- **Screen Time** — per-app usage history.
- **AI token usage** — totals from Claude Code, Codex and OpenCode session files on this PC.
- **Settings import/export**, themes, edge light effects, hotkey (`Ctrl+Shift+A`) and more.

## Install on another Windows PC

1. Download `Ayn-Setup.msi` from the Releases page (or from `Ayn-Setup.zip`).
2. Double-click **Ayn-Setup.msi** and click through the installer. No administrator rights needed.
3. Launch **Ayn** from the Start Menu or the desktop shortcut.

There is no sign-in, trial or payment screen. The app is fully usable immediately.

To uninstall, use *Settings → Apps → Ayn → Uninstall*.

### Portable option

Extract `Ayn-Setup.zip` and run `app\Ayn.exe`. The app is self-contained; .NET does not need
to be installed on the target PC.

## Build from source

Requirements: Windows 10 1809 or newer, .NET 8 SDK, x64.

```powershell
git clone https://github.com/salahuddinselim/Ayn.git
cd Ayn
dotnet build src\Ayn\Ayn.csproj -c Release
dotnet run --project src\Ayn\Ayn.csproj
```

Create a one-click installer:

```powershell
dotnet tool install -g wix --version 5.0.2
dotnet publish src\Ayn\Ayn.csproj -c Release -r win-x64 --self-contained true -o AynSetup\app
powershell -File Installer\generate-files.ps1
wix build Installer\Product.wxs Installer\Files.wxs -o Installer\Ayn-Setup.msi
```

## Where data lives

All settings and data are stored under `%LOCALAPPDATA%\Ayn`. Nothing is uploaded.
The only network calls are optional: city lookup for prayer times, IP area detection,
and LAN sharing when you explicitly start it.

Network Guard listens on **UDP 5354**. To protect every device on your Wi‑Fi, point your
router's DNS at this PC and forward UDP 53 to port 5354 if your router requires the
standard DNS port.

## Project layout

- `src/Ayn` — WPF application (views, services, modules, compatibility shims).
- `src/Ayn/Extras` — Ayn-specific modules (reminders, guard, QR/Wi‑Fi, pets, to-do, tools).
- `Installer` — WiX MSI sources and generator.
- `AynSetup` — self-contained publish output and MSI for distribution.

## License

MIT — see [LICENSE](LICENSE). Free for personal and commercial use, modification and redistribution.

Made by SabrWare. Ayn / العين.
