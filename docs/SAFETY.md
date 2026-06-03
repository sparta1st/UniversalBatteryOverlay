# Safety Notes

Universal Battery Overlay is designed to avoid interfering with input devices.

## What is disabled

The app does not use generic HID probing. Generic probing can accidentally open keyboard or dongle interfaces and may interfere with input.

## What is allowed

The app uses:

- passive Windows device inventory;
- passive Windows battery properties;
- targeted readers for exact device IDs only.

Current targeted readers:

- Razer Viper V2 Pro: `VID_1532&PID_00A6` / `VID_1532&PID_00A5`;
- Logitech G733: `VID_046D&PID_0AB5`.

The QwertyKey keyboard is passive-only. The app does not send commands to it.

## If input ever feels wrong

1. Exit the app from the tray menu.
2. Unplug/replug the affected dongle.
3. Disable targeted readers from the Devices tab if needed.
4. Open a GitHub issue with logs from `%APPDATA%\UniversalBatteryOverlay\logs`.
