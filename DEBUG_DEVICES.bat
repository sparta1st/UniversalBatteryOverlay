@echo off
setlocal
cd /d "%~dp0"
title Universal Battery Overlay - Debug Devices

echo ============================================
echo   Universal Battery Overlay v16 - DEBUG DEVICES
echo ============================================
echo.
echo Creating Windows device debug file...
echo.

set OUT=%~dp0device-debug.txt
powershell -NoProfile -ExecutionPolicy Bypass -Command "$ErrorActionPreference='SilentlyContinue'; $out='%OUT%'; '=== Universal Battery Overlay v16 device debug ===' | Out-File -Encoding utf8 $out; 'Generated: ' + (Get-Date) | Out-File -Append -Encoding utf8 $out; ''; '=== Matched known devices ===' | Out-File -Append -Encoding utf8 $out; $known=@('VID_1532&PID_00A6','VID_046D&PID_0AB5','VID_36B0&PID_3002'); Get-PnpDevice -PresentOnly | Where-Object { $id=$_.InstanceId; $known | Where-Object { $id -like ('*' + $_ + '*') } } | Sort-Object FriendlyName,Class | Select-Object Class,FriendlyName,Status,InstanceId | Format-List | Out-File -Append -Encoding utf8 $out; ''; '=== Present PnP devices ===' | Out-File -Append -Encoding utf8 $out; Get-PnpDevice -PresentOnly | Sort-Object Class,FriendlyName | Select-Object Class,FriendlyName,Status,InstanceId | Format-List | Out-File -Append -Encoding utf8 $out; ''; '=== Battery related properties for matching devices ===' | Out-File -Append -Encoding utf8 $out; Get-PnpDevice -PresentOnly | Where-Object { $_.FriendlyName -match 'Razer|Viper|Logitech|G733|QwertyKey|Keyboard|Mouse|Headset|Headphone|Wireless|Receiver|Dongle|Battery|Bluetooth' -or $_.InstanceId -match 'VID_1532&PID_00A6|VID_046D&PID_0AB5|VID_36B0&PID_3002' } | ForEach-Object { '--- ' + $_.FriendlyName + ' [' + $_.Class + '] ' + $_.InstanceId | Out-File -Append -Encoding utf8 $out; Get-PnpDeviceProperty -InstanceId $_.InstanceId | Where-Object { $_.KeyName -match 'Battery|Charge|Capacity|Power' -or $_.Data -match '^[0-9]{1,3}$' } | Select-Object KeyName,Data | Format-List | Out-File -Append -Encoding utf8 $out }"

if exist "%OUT%" (
  echo [OK] File created:
  echo %OUT%
) else (
  echo [ERROR] Could not create device-debug.txt
)

echo.
pause
