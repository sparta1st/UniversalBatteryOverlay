# Universal Battery Overlay

A clean Windows 11 tray app that shows a compact realtime battery overlay for wireless devices such as mice, headsets, keyboards, controllers, and the system battery.

The app is built for a simple use case: keep a tiny overlay in the corner of your screen and see useful battery levels without keeping vendor apps open.

## Features

- Compact always-on-top overlay.
- Runs quietly in the Windows tray.
- Closing the settings window with `X` hides it to the tray instead of exiting.
- Realtime refresh, configurable from 1 second upward.
- Clean English UI with sections for Dashboard, Overlay, Devices, and Advanced.
- Live overlay preview inside the settings window.
- Customizable overlay:
  - monitor selection
  - position
  - offset X / Y
  - text size
  - opacity
  - background opacity
  - padding
  - row spacing
  - minimum width
  - corner radius
  - click-through mode
- Charging indicator with `⚡` when a reader can detect charging.
- Safe keyboard behavior: the app avoids sending unknown HID commands to keyboards.
- Extensible reader system through PowerShell scripts in the `readers` folder.

## Current reader support

The app tries multiple safe methods, from generic Windows APIs to device-specific readers.

| Device/source | Status |
|---|---|
| Windows system/laptop battery | Supported |
| Windows PnP battery properties | Supported when exposed by Windows |
| Standard HID battery strength | Optional, safe generic reader |
| Razer Viper V2 Pro | Direct HID reader included |
| Logitech G733 | Direct attempt + optional HeadsetControl helper |
| QwertyKey / generic keyboards | Safe passive detection; battery only if exposed |
| Bluetooth / controllers / common gaming brands | Best-effort detection through Windows/PnP/HID |
| Custom devices | Supported through custom reader scripts |

## Honest limitation

No app can read the real battery percentage of every device in the world if the device or its 2.4 GHz dongle does not expose that information to Windows or through a known protocol.

Universal Battery Overlay is designed to be honest:

- If the app has a real percentage, it shows it.
- If a device is detected but does not expose battery, the app does not invent a fake percentage.
- If a charging state is available, the app shows `⚡`.
- If a reader returns unstable values, the app stabilizes them instead of showing obvious spikes.

## Requirements

- Windows 11 or Windows 10.
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) for development/building from source.

For normal use after publishing, users can run the built `.exe` without the SDK if you publish a self-contained build.

## Quick start

Download or clone the repository, then run:

```bat
CHECK_BUILD.bat
START_APP.bat
```

The first launch can take a moment because .NET restores packages and builds the project.

## Build

```bat
BUILD_APP.bat
```

or:

```powershell
.\build.ps1
```

## Publish single EXE

```bat
MAKE_EXE.bat
```

or:

```powershell
.\publish-single-exe.ps1
```

The published executable will be created under:

```text
src\UniversalBatteryOverlay\bin\Release\net8.0-windows\win-x64\publish
```

## Tray behavior

- `X` on the settings window hides the window to tray.
- Double-click the tray icon to reopen settings.
- Right-click the tray icon for Open settings, Refresh now, Show/Hide overlay, and Exit.
- Use Exit only when you want to fully stop monitoring.

## Optional Logitech headset helper

For some headsets, Windows does not expose battery level. The app can optionally call `HeadsetControl` as a short-lived command-line helper.

Run:

```bat
INSTALL_HEADSETCONTROL.bat
```

This helper is not Logitech G HUB and does not stay running in the background. The app launches it only when it checks headset battery.

If automatic installation fails, manually place `headsetcontrol.exe` here:

```text
tools\headsetcontrol\headsetcontrol.exe
```

## Custom reader scripts

You can add custom battery readers without recompiling the app.

Put a `.ps1` file in the `readers` folder. A reader should print JSON like this:

```json
[
  {
    "name": "My Wireless Device",
    "deviceType": "Mouse",
    "batteryPercent": 74,
    "isCharging": false,
    "status": "OK",
    "reader": "My custom reader"
  }
]
```

See `docs/CUSTOM_READERS.md` for details.

## Repository structure

```text
UniversalBatteryOverlay/
├─ src/UniversalBatteryOverlay/     WPF app source
├─ readers/                         optional custom PowerShell readers
├─ docs/                            documentation
├─ tools/                           optional local tools, ignored by Git
├─ START_APP.bat                    easy run script
├─ CHECK_BUILD.bat                  build verification script
├─ MAKE_EXE.bat                     publish script
├─ README.md
└─ LICENSE
```

## Safety notes

The app uses a conservative approach for HID devices. It avoids active unknown commands on keyboards because some keyboards/dongles can behave badly if probed incorrectly. Device-specific HID commands are only used for readers where the protocol is known and intentionally targeted.

See `docs/SAFETY.md` for more details.

## License

MIT License. See `LICENSE`.
