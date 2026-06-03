# Universal Battery Overlay

Universal Battery Overlay is a clean Windows tray app that shows realtime battery information for wireless peripherals in a compact desktop overlay.

It is made for setups with gaming mice, wireless headsets, keyboards and controllers, without needing to keep every vendor app open.

```text
Mouse      40%
Headset   ⚡ 61%
Keyboard   85%
```

## Highlights

- Compact always-on-top overlay.
- Clean dark UI with organized tabs: **Overview**, **Overlay**, **Devices**, **Safety**, **Diagnostics**, **About**.
- Runs in the Windows tray; closing with **X** hides the settings window instead of exiting.
- Realtime overlay refresh from 1 second upward.
- Performance-safe reader cache: slow Windows inventory scans are cached so the app does not lag every second.
- Customizable overlay placement, monitor, offset, opacity, row spacing, padding, corner radius and colors.
- Charging icon `⚡` when a reader can detect charging.
- Wireless-focused filtering so random USB hubs, monitors, RGB controllers and storage devices do not clutter the UI.
- Keyboard-safe model: no generic HID probing and no active commands sent to unknown keyboards.
- Targeted direct readers only for known hardware IDs.
- Large built-in device-profile database for safe passive recognition.
- External profile support through `device-profiles.json`.
- GitHub issue templates for bugs and device-support requests.

## Support model

Universal support is handled in layers. This is the safest way to support many devices without breaking input devices.

| Level | Meaning | Battery percentage | Safety |
|---|---|---:|---|
| Targeted direct reader | Exact code for a known model/hardware ID. | Yes, when protocol works. | Active but isolated. |
| Passive Windows battery | Windows exposes battery properties. | Yes. | Passive and safe. |
| Optional helper | A trusted external CLI can query a supported headset. | Yes, when installed. | On-demand only. |
| Passive profile detection | Device is recognized by name or VID/PID. | No, unless Windows exposes it. | Passive and safe. |

## Built-in targeted readers

| Device | Status | Notes |
|---|---:|---|
| Razer Viper V2 Pro | Supported | Targeted zero-access HID reader for known Viper V2 Pro hardware IDs. |
| Logitech G733 | Experimental | Targeted G733 reader plus optional HeadsetControl helper. Some devices report unstable values while charging, so values are stabilized. |
| Windows laptop/system battery | Supported | Uses normal Windows battery APIs. |

## Built-in passive profiles

The app includes safe passive profiles for many popular wireless-device families, including:

- Logitech G / LIGHTSPEED / MX mice, keyboards and headsets;
- Razer Viper, DeathAdder, Basilisk, Naga, Cobra, BlackWidow, DeathStalker, BlackShark, Barracuda and Kraken families;
- SteelSeries Arctis/Nova, Aerox, Rival, Prime and Apex wireless devices;
- Corsair Virtuoso, HS, VOID, Dark Core, Harpoon, Katar, M75, Sabre and K-series wireless devices;
- HyperX Cloud and Pulsefire wireless devices;
- ASUS ROG/TUF wireless mice, keyboards and headsets;
- Turtle Beach / ROCCAT wireless devices;
- Glorious, Pulsar, Lamzu, Finalmouse, WLmouse, Ninjutso, Endgame Gear and Zowie wireless mice;
- Keychron, NuPhy, Akko, Epomaker, Royal Kludge, Anne Pro and similar Bluetooth keyboards;
- Xbox, DualSense, DualShock, Nintendo Switch Pro, 8BitDo, Flydigi, GuliKit, SCUF and Victrix controllers;
- Apple AirPods, Sony, Bose, Sennheiser, JBL, Beats, Jabra, Soundcore, Galaxy Buds, Pixel Buds, Shokz and other Bluetooth headsets.

Passive profile support means the app can recognize the device safely. A real percentage appears only when Windows exposes battery data or when a dedicated reader exists.

## Safety first

Older experimental builds used wider HID probing. That can interfere with some keyboards or dongles.

This build avoids that approach. It uses only:

1. passive Windows device inventory;
2. passive Windows battery properties;
3. passive device profiles;
4. exact targeted readers for known hardware IDs;
5. optional headset helper only when installed.

The app does **not** run a generic HID scanner, and it does **not** send unknown HID commands to keyboards such as QwertyKey.

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

## Adding device support

For safe passive detection, add a profile in `device-profiles.json`.

See [Adding devices](docs/ADDING_DEVICES.md).

For real battery percentages on proprietary dongle devices, a dedicated reader may be required. Open a GitHub device-support issue with hardware IDs and logs.

## Project structure

```text
UniversalBatteryOverlay/
├─ .github/                 GitHub Actions and issue templates
├─ docs/                    Documentation
├─ src/UniversalBatteryOverlay/
│  ├─ Readers/              Device battery readers
│  ├─ Services/             Monitoring, settings, logging and app control
│  ├─ Models/               App settings, device data and device profiles
│  └─ Utils/                Native Windows helpers
├─ device-profiles.example.json
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
- hardware IDs;
- whether the official vendor app shows a battery percentage;
- whether Windows Device Manager shows the device;
- screenshots if the issue is visual;
- the latest log from:

```text
%APPDATA%\UniversalBatteryOverlay\logs
```

## Changelog and roadmap

- See [Changelog](CHANGELOG.md) for version changes.
- See [Roadmap](ROADMAP.md) for future additions.

## License

MIT License. See [LICENSE](LICENSE).
