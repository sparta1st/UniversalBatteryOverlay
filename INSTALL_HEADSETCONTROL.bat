@echo off
setlocal
cd /d "%~dp0"
echo ============================================
echo  Install optional HeadsetControl helper
echo ============================================
echo.
echo This helper is NOT G HUB and does NOT stay running in the background.
echo The app starts it only for a few seconds while checking headset battery.
echo.
powershell -NoProfile -ExecutionPolicy Bypass -Command ^
  "$ErrorActionPreference='Stop';" ^
  "$ProgressPreference='SilentlyContinue';" ^
  "$root=(Get-Location).Path;" ^
  "$tools=Join-Path $root 'tools\headsetcontrol';" ^
  "New-Item -ItemType Directory -Force -Path $tools | Out-Null;" ^
  "$api='https://api.github.com/repos/Sapd/HeadsetControl/releases/latest';" ^
  "$release=Invoke-RestMethod -Uri $api -Headers @{ 'User-Agent'='UniversalBatteryOverlay' };" ^
  "$asset=$release.assets | Where-Object { $_.name -match '(?i)win|windows' -and $_.name -match '(?i)\.zip$' } | Select-Object -First 1;" ^
  "if(-not $asset){ Write-Host 'Could not automatically find a Windows ZIP asset. Available assets:'; $release.assets | ForEach-Object { Write-Host (' - ' + $_.name) }; throw 'Download the Windows ZIP manually and place headsetcontrol.exe in tools\headsetcontrol\'; }" ^
  "$zip=Join-Path $tools $asset.name;" ^
  "Write-Host ('Downloading: ' + $asset.browser_download_url);" ^
  "Invoke-WebRequest -Uri $asset.browser_download_url -OutFile $zip -Headers @{ 'User-Agent'='UniversalBatteryOverlay' };" ^
  "$unpack=Join-Path $tools 'unpacked'; if(Test-Path $unpack){ Remove-Item $unpack -Recurse -Force }; New-Item -ItemType Directory -Force -Path $unpack | Out-Null;" ^
  "Expand-Archive -Path $zip -DestinationPath $unpack -Force;" ^
  "$exe=Get-ChildItem -Path $unpack -Recurse -Filter 'headsetcontrol*.exe' | Select-Object -First 1;" ^
  "if(-not $exe){ throw 'headsetcontrol.exe was not found inside the ZIP.' }" ^
  "Copy-Item $exe.FullName (Join-Path $tools 'headsetcontrol.exe') -Force;" ^
  "Write-Host ('[OK] Installed: ' + (Join-Path $tools 'headsetcontrol.exe'));"
if errorlevel 1 (
  echo.
  echo [ERROR] Automatic helper install failed.
  echo You can manually download HeadsetControl Windows ZIP and copy headsetcontrol.exe here:
  echo %cd%\tools\headsetcontrol\headsetcontrol.exe
  pause
  exit /b 1
)

echo.
echo [OK] HeadsetControl helper is ready.
echo Run START_APP.bat and click Refresh now.
pause
