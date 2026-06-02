# Universal Battery Overlay - external reader sample
# Leave this file as sample-* and it will be ignored.
# To activate: copy it into %APPDATA%\UniversalBatteryOverlay\readers\ and rename it, for example razer-reader.ps1.
# It must print JSON to stdout.

@(
  [pscustomobject]@{
    name = "Example Mouse"
    deviceType = "Mouse"
    batteryPercent = 74
    isCharging = $false
    status = "OK"
    reader = "Example reader"
    rawId = "EXAMPLE-001"
  }
) | ConvertTo-Json -Compress
