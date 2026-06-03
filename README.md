# Universal Battery Overlay

Universal Battery Overlay is a Windows tray app that shows realtime battery information for wireless peripherals in a compact overlay.

It is designed for gaming and desktop setups where you want a small, clean overlay instead of keeping vendor apps open.

```text
Mouse      40%
Headset   ⚡ 61%
Keyboard   85%
```

## Highlights

- Compact always-on-top overlay.
- Runs in the Windows system tray.
- Closing the settings window with **X** hides the app to the tray instead of exiting.
- Realtime refresh, configurable from 1 second upward.
- Clean English UI with organized tabs.
- Live overlay preview inside the settings window.
- Customizable overlay placement, size, spacing, opacity, colors and click-through mode.
- Charging indicator with `⚡` when the reader can detect charging.
- Wireless-focused device list so random USB devices do not clutter the UI.
- Keyboard-safe reader model: no generic HID probing and no active commands sent to unknown keyboards.
- Targeted direct readers for known hardware.

## Current support

| Device / source | Status | Notes |
|---|---:|---|
| Razer Viper V2 Pro | Supported | Targeted direct reader for known Razer VID/PID paths. |
| Logitech G733 | Experimental | Targeted G733 reader plus optional HeadsetControl helper. Some units may report unstable values while charging. |
| QwertyKey wireless keyboard | Passive only | Detected safely. The app does not actively probe the keyboard. Battery appears only if Windows exposes it. |
| Windows laptop/system battery | Supported | Works when a system battery is present. |
| Bluetooth battery devices | Best effort | Works when Windows exposes battery properties. |
| Other wireless devices | Best effort | Can be added through targeted readers or future device support requests. |

## Safety first

Older experimental builds used wider HID probing. That could interfere with some keyboards or dongles.

This clean build avoids that approach. It uses only:

1. passive Windows device inventory;
2. passive Windows battery properties;
3. targeted readers for known hardware IDs.

The app does **not** run a generic HID scanner, and it does **not** send unknown HID commands to QwertyKey or other keyboards.

See [Safety](docs/SAFETY.md) for more details.

## Requirements

- Windows 11 or Windows 10.
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) for running from source.

Install the SDK with WinGet:

```powershell
winget install -e --id Microsoft.DotNet.SDK.8
```

## Run from source

```bat
run.bat
```

## Publish Windows EXE folder

```bat
publish.bat
```

The published output is created under:

```text
src\UniversalBatteryOverlay\bin\Release\net8.0-windows\win-x64\publish
```

## Project structure

```text
UniversalBatteryOverlay/
├─ .github/                 GitHub Actions and issue templates
├─ docs/                    Documentation
├─ src/UniversalBatteryOverlay/
│  ├─ Readers/              Device battery readers
│  ├─ Services/             Monitoring, settings, logging and app control
│  ├─ Models/               App settings and device data models
│  └─ Utils/                Native Windows helpers
├─ run.bat                  Run from source
├─ publish.bat              Publish EXE folder
├─ README.md
├─ CHANGELOG.md
├─ ROADMAP.md
└─ LICENSE
```

## Reporting bugs

Use the issue templates in the GitHub **Issues** tab:

- **Bug report** for crashes, UI problems, tray problems, wrong percentages or detection issues.
- **Device support request** for new wireless devices.

When reporting a device issue, include:

- device model;
- connection type: 2.4 GHz dongle, Bluetooth or wired;
- whether the official vendor app shows a battery percentage;
- whether Windows Device Manager shows the device;
- screenshots if the issue is visual;
- the latest log from:

```text
%APPDATA%\UniversalBatteryOverlay\logs
```

## Future plans

See [Roadmap](ROADMAP.md) for planned improvements.

## Changelog

See [Changelog](CHANGELOG.md).

## License

MIT License. See [LICENSE](LICENSE).
