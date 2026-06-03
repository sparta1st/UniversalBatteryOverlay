@echo off
setlocal
cd /d "%~dp0"
echo Starting Universal Battery Overlay...
echo.
dotnet run --project .\src\UniversalBatteryOverlay\UniversalBatteryOverlay.csproj
if errorlevel 1 (
  echo.
  echo [ERROR] The app stopped with an error.
  echo Logs are saved in: %%APPDATA%%\UniversalBatteryOverlay\logs
  echo.
  pause
  exit /b 1
)
endlocal
