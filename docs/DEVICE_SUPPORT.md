# Device support

Universal Battery Overlay supports devices through a layered reader system.

## Reader order

1. System battery reader.
2. Windows PnP battery properties.
3. Optional standard HID battery reader.
4. Known direct readers, such as Razer Viper V2 Pro.
5. Optional command-line helpers, such as HeadsetControl.
6. Custom PowerShell readers from the `readers` folder.
7. Safe presence detection for known VID/PID devices.

## What works best

Devices usually work well when they expose battery through:

- Windows battery APIs.
- Bluetooth LE battery service.
- HID battery strength usage.
- A known vendor protocol.

## What may not work

Some 2.4 GHz dongle devices do not expose the real battery percentage to Windows. They may only show battery inside the vendor app, or only with LED indicators. In those cases, the app can detect the device but cannot truthfully show a percentage until a specific reader is added.

## Included known-device focus

- Razer Viper V2 Pro: direct targeted HID reader.
- Logitech G733: direct attempt and optional HeadsetControl helper.
- QwertyKey/generic keyboards: passive safe detection by default.

## Adding more devices

Add a custom reader in the `readers` folder or implement a new `IBatteryReader` in C#.
