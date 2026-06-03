@echo off
setlocal
cd /d "%~dp0"
echo Publishing Universal Battery Overlay for Windows x64...
echo.
dotnet restore .\src\UniversalBatteryOverlay\UniversalBatteryOverlay.csproj --configfile .\NuGet.config -r win-x64
if errorlevel 1 goto failed
dotnet publish .\src\UniversalBatteryOverlay\UniversalBatteryOverlay.csproj -c Release -r win-x64 --self-contained false --no-restore /p:UseAppHost=true /p:PublishSingleFile=false
if errorlevel 1 goto failed
echo.
echo [OK] Publish completed.
echo EXE folder:
echo .\src\UniversalBatteryOverlay\bin\Release\net8.0-windows\win-x64\publish
echo.
pause
exit /b 0
:failed
echo.
echo [ERROR] Publish failed.
echo.
pause
exit /b 1
