@echo off
setlocal
cd /d "%~dp0"
title Universal Battery Overlay - Publish EXE

echo Publishing Universal Battery Overlay...
echo.

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0publish-single-exe.ps1"

if errorlevel 1 (
  echo.
  echo [ERROR] Publish failed.
  pause
  exit /b 1
)

echo.
echo [OK] Publish completed.
pause
