# Troubleshooting

## The mouse battery does not appear

Reconnect the Razer receiver, wait a few seconds and press refresh in the app. Try a direct motherboard USB port instead of a hub.

## The headset appears but has no percentage

Some Logitech G733 receivers do not expose battery through Windows. This build detects the headset passively but does not aggressively probe it because keyboard safety is prioritized.

## The keyboard interferes or becomes unresponsive

This build should not actively touch the keyboard. Make sure older versions are closed from Task Manager and delete old settings:

```powershell
Remove-Item "$env:APPDATA\UniversalBatteryOverlay\settings.json" -Force -ErrorAction SilentlyContinue
```

Then run the latest build again.
