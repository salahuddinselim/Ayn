Ayn by SabrWare - Windows installer
===================================

Requirements: Windows 10 1809 or newer, x64. No .NET installation needed.

Install:
  1. Copy the whole "AynSetup" folder to the target PC.
  2. Double-click Install.cmd.
  3. Ayn is installed under %LOCALAPPDATA%\SabrWare\Ayn and a Start Menu
     shortcut is created. A desktop shortcut is optional.

Uninstall:
  Double-click Uninstall.cmd.

First run:
  Ayn starts as a small notch at the top center of the screen.
  Click the notch (or press Ctrl+Shift+A) to expand it.
  The gear icon in the notch opens Settings.

Your features are in the Settings sidebar and the notch dock:
  Breaks & Meals, Salat Times, Network Guard, AI Mail & Calendar,
  Pets (Ditto, Cat, Husky, Pikachu, Doraemon, Dots, Grok),
  QR & Wi-Fi Share, Doc Converter, File Converter, Screen Time, AI token usage.

Doc Converter uses LibreOffice if installed (PDF, DOCX, XLSX, PPTX, RTF,
TXT, HTML, ODT, CSV). File Converter adds BMP, GIF and TIFF alongside
JPG, PNG, PDF and ICO.

Eyes: choose a skin and an emotion (happy, sad, cry, ego, surprised,
sleepy) in Settings > Notch. Logo: Ayn / العين by SabrWare.

Notes:
  - All data stays in %LOCALAPPDATA%\Ayn on that PC.
  - Network Guard listens on UDP 5354. Point your router DNS at this PC
    (and forward UDP 53 to 5354 if your router requires the standard port)
    to protect every device on the Wi-Fi.
  - AI Mail & Calendar needs a free Gemini API key, entered in Settings.
  - AI token totals for Claude Code, Codex and OpenCode are read from local
    session files/databases on this PC.
