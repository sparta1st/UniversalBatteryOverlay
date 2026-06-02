@echo off
setlocal
cd /d "%~dp0"
title Universal Battery Overlay

echo ============================================
echo   Universal Battery Overlay - START
echo ============================================
echo.

where dotnet >nul 2>nul
if errorlevel 1 (
  echo [ERROR] .NET 8 SDK is not installed or is not in PATH.
  echo Install .NET 8 SDK from:
  echo https://dotnet.microsoft.com/download/dotnet/8.0
  echo.
  pause
  exit /b 1
)

echo Closing old UniversalBatteryOverlay.exe instance if it exists...
taskkill /IM UniversalBatteryOverlay.exe /F >nul 2>nul

echo.
echo Starting the app...
echo First launch may take a moment while packages are restored.
echo.

dotnet run --project "%~dp0src\UniversalBatteryOverlay\UniversalBatteryOverlay.csproj"

if errorlevel 1 (
  echo.
  echo [ERROR] The app stopped with an error.
  echo Copy the text above and send it back.
)

echo.
pause
