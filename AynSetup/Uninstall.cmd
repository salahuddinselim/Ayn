@echo off
setlocal
set "TARGET=%LOCALAPPDATA%\SabrWare\Ayn"

echo Uninstalling Ayn...
del "%APPDATA%\Microsoft\Windows\Start Menu\Programs\SabrWare\Ayn.lnk" >nul 2>&1
del "%USERPROFILE%\Desktop\Ayn.lnk" >nul 2>&1
rmdir "%APPDATA%\Microsoft\Windows\Start Menu\Programs\SabrWare" >nul 2>&1
taskkill /IM Ayn.exe /F >nul 2>&1
if exist "%TARGET%" rmdir /S /Q "%TARGET%"
echo Ayn has been removed from this PC.
pause
