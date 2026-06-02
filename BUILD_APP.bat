@echo off
setlocal
cd /d "%~dp0"
title Universal Battery Overlay - Build

echo Building Universal Battery Overlay...
echo.

dotnet build "%~dp0src\UniversalBatteryOverlay\UniversalBatteryOverlay.csproj" -c Release

if errorlevel 1 (
  echo.
  echo [ERROR] Build failed.
  pause
  exit /b 1
)

echo.
echo [OK] Build completed.
pause
