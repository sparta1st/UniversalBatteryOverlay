@echo off
setlocal
cd /d "%~dp0"
title Universal Battery Overlay - Check Build

echo ============================================
echo   Universal Battery Overlay - CHECK BUILD
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

dotnet build "%~dp0src\UniversalBatteryOverlay\UniversalBatteryOverlay.csproj" -c Release

if errorlevel 1 (
  echo.
  echo [ERROR] Build failed.
  pause
  exit /b 1
)

echo.
echo [OK] Build completed without errors.
pause
