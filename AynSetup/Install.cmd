@echo off
setlocal
set "TARGET=%LOCALAPPDATA%\SabrWare\Ayn"
set "START=%APPDATA%\Microsoft\Windows\Start Menu\Programs\SabrWare"

echo Installing Ayn by SabrWare...
if not exist "%TARGET%" mkdir "%TARGET%"
xcopy /E /I /Y /Q "%~dp0app\*" "%TARGET%\" >nul
if errorlevel 1 goto :error

if not exist "%START%" mkdir "%START%"
powershell -NoProfile -ExecutionPolicy Bypass -Command "$w=New-Object -ComObject WScript.Shell; $s=$w.CreateShortcut((Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs\SabrWare\Ayn.lnk')); $s.TargetPath=(Join-Path $env:LOCALAPPDATA 'SabrWare\Ayn\Ayn.exe'); $s.WorkingDirectory=(Join-Path $env:LOCALAPPDATA 'SabrWare\Ayn'); $s.Description='Ayn - the notch for Windows'; $s.Save()"

choice /C YN /N /M "Create a desktop shortcut? (Y/N) "
if errorlevel 2 goto :done
powershell -NoProfile -ExecutionPolicy Bypass -Command "$w=New-Object -ComObject WScript.Shell; $s=$w.CreateShortcut((Join-Path $env:USERPROFILE 'Desktop\Ayn.lnk')); $s.TargetPath=(Join-Path $env:LOCALAPPDATA 'SabrWare\Ayn\Ayn.exe'); $s.WorkingDirectory=(Join-Path $env:LOCALAPPDATA 'SabrWare\Ayn'); $s.Save()"

:done
echo.
echo Ayn installed to %TARGET%
echo You can launch it from the Start Menu or desktop shortcut.
pause
exit /b 0

:error
echo Installation failed. Please run this file as your normal Windows user.
pause
exit /b 1
