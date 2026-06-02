# Manual fallback example for QwertyKey keyboard.
# Rename this file and edit batteryPercent if your keyboard does not expose battery to Windows.
# This is not automatic; it is useful only as a temporary manual fallback.

[pscustomobject]@{
  name = "QwertyKey Keyboard"
  deviceType = "Keyboard"
  batteryPercent = 100
  isCharging = $false
  status = "Manual value"
  reader = "Manual QwertyKey fallback"
  rawId = "QWERTYKEY-MANUAL"
} | ConvertTo-Json -Compress
