@echo off
setlocal
cd /d "%~dp0"
title Universal Battery Overlay - Stop

echo Stopping old UniversalBatteryOverlay.exe instances...
taskkill /IM UniversalBatteryOverlay.exe /F >nul 2>nul

echo [OK] Done.
pause
