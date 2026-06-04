# Universal Battery Overlay

A clean Windows 11 battery overlay for wireless peripherals.

Universal Battery Overlay is a lightweight desktop app that shows the battery level of supported wireless devices in a small always-on-top overlay. It is designed for gaming and daily desktop use: minimal UI, real-time updates, tray behavior, safe device detection, and a portable EXE build.

> Built for Windows 11. Focused on wireless mice, headsets, keyboards, controllers, and Bluetooth devices.

---

## Highlights

* Clean always-on-top battery overlay
* Real-time refresh
* Tray icon support
* Close-to-tray behavior
* Portable single EXE build
* Customizable overlay design
* Multiple display support
* Safe wireless device detection
* No generic HID scanning by default
* Targeted support for selected devices
* Charging indicator with `⚡`
* Battery stabilization while charging
* GitHub-ready documentation and issue templates

---

## Preview

Overlay example:

```text
Mouse      40%
Headset   ⚡ 61%
Keyboard  85%
```

The overlay can be customized from the app:

* Display selection
* Position
* Offset X / Y
* Opacity
* Background color
* Text color
* Percentage color
* Padding
* Row spacing
* Corner radius
* Shadow
* Click-through mode

---

## Why this app exists

Most wireless peripherals do not expose battery information in the same way.

Some devices expose battery level directly through Windows.
Some require vendor-specific HID commands.
Some only expose battery through official software.
Some do not expose battery to Windows at all.

Universal Battery Overlay tries to support as many devices as possible while avoiding unsafe behavior, especially anything that could interfere with keyboard or mouse input.

---

## Safety First

This app is built around one important rule:

> Do not break user input.

To avoid keyboard or dongle interference:

* Generic HID scanning is disabled by default.
* Unknown keyboards are not actively queried.
* QwertyKey-style keyboards are passive-only.
* Targeted readers are used only for known devices.
* Wireless detection prefers passive Windows APIs.
* Potentially risky direct device access is isolated per-device.

Read more in [Safety Notes](docs/SAFETY.md).

---

## Current Device Support

### Confirmed / Targeted

| Device                      |           Support | Notes                           |
| --------------------------- | ----------------: | ------------------------------- |
| Razer Viper V2 Pro          | Battery supported | Uses targeted direct reader     |
| Logitech G733               |      Experimental | Targeted reader + stabilization |
| QwertyKey wireless keyboard | Passive detection | No active commands for safety   |

### Broad Passive Detection

The app includes safe profiles for many wireless device families:

* Logitech G / LIGHTSPEED / MX
* Razer wireless devices
* SteelSeries Arctis / Aerox / Prime
* Corsair wireless devices
* HyperX wireless devices
* ASUS ROG / TUF wireless
* Turtle Beach / ROCCAT
* Glorious
* Pulsar
* Lamzu
* Finalmouse
* Keychron
* NuPhy
* Akko
* Xbox controllers
* DualSense / DualShock controllers
* Nintendo Switch Pro Controller
* 8BitDo controllers
* Bluetooth headsets
* Bluetooth keyboards
* Bluetooth mice
* AirPods
* Sony headphones
* Bose headphones
* JBL headphones
* Beats headphones
* Soundcore devices

Passive detection means the app can detect the device without sending commands to it. Battery percentage appears only when Windows or a safe reader exposes it.

Read more in [Device Support](docs/DEVICE_SUPPORT.md).

---

## Installation

### Option 1: Portable EXE

Download or build:

```text
UniversalBatteryOverlay_Portable.exe
```

Then run it from anywhere, including Desktop.

No installer required.

---

### Option 2: Run from source

Requirements:

* Windows 11
* .NET 8 SDK

Run:

```bat
run.bat
```

---

## Build Portable EXE

Run:

```bat
publish.bat
```

The portable EXE will be created as:

```text
UniversalBatteryOverlay_Portable.exe
```

You can copy only this EXE to Desktop or another folder.

---

## GitHub Setup

Clone the repository:

```bat
git clone https://github.com/sparta1st/UniversalBatteryOverlay.git
cd UniversalBatteryOverlay
```

Run the app:

```bat
run.bat
```

Publish the portable EXE:

```bat
publish.bat
```

Commit changes:

```bat
git add -A
git commit -m "Update Universal Battery Overlay"
git push
```

---

## Clean Replace Local Repository

To replace all project files while keeping Git history:

```bat
cd /d "C:\Users\SPARTA\Desktop\Proiecte\batt-clean"
powershell -NoProfile -Command "Get-ChildItem -Force | Where-Object { $_.Name -ne '.git' } | Remove-Item -Recurse -Force"
```

Then copy the new project files into the folder and run:

```bat
git add -A
git commit -m "Clean project update"
git push
```

---

## Project Structure

```text
UniversalBatteryOverlay
├─ .github
│  └─ ISSUE_TEMPLATE
├─ docs
│  ├─ ADDING_DEVICES.md
│  ├─ CUSTOM_READERS.md
│  ├─ DEVICE_SUPPORT.md
│  ├─ DEVELOPMENT.md
│  ├─ SAFETY.md
│  └─ TROUBLESHOOTING.md
├─ src
│  └─ UniversalBatteryOverlay
├─ CHANGELOG.md
├─ LICENSE
├─ README.md
├─ ROADMAP.md
├─ VERSION
├─ run.bat
└─ publish.bat
```

The build is intentionally clean. Only the required scripts are kept in root.

---

## App Behavior

### Main Window

The main window contains sections for:

* Overview
* Overlay customization
* Devices
* Safety
* Diagnostics
* About

### Tray Behavior

When closing the window with `X`, the app stays active in the tray.

Tray menu:

* Open settings
* Refresh now
* Toggle overlay
* Exit

### Overlay

The overlay is designed to be small, readable, and useful during gaming or work.

Example:

```text
Mouse      40%
Headset   ⚡ 61%
Keyboard  85%
```

Devices without a real battery percentage are hidden from the overlay by default to avoid clutter.

---

## Charging Stabilization

Some wireless devices report fake or unstable values while charging.

Example:

```text
55% → 100% → 57% → 55%
```

Universal Battery Overlay filters unstable values and keeps the last trusted battery level while showing the charging indicator.

Example:

```text
Headset   ⚡ 55%
```

This prevents fake instant jumps to 100%.

---

## Known Limitations

* Some dongles do not expose battery data to Windows.
* Some devices require official software to read battery.
* Some devices report unstable values while charging.
* Passive detection can detect a device without being able to read its battery.
* Battery support depends on the device firmware, driver, and Windows exposure.

The app avoids dangerous generic HID probing because it can interfere with keyboards and input devices.

---

## Adding More Devices

Device support should be added safely.

Preferred order:

1. Passive Windows detection
2. Known VID/PID profile
3. Windows-exposed battery data
4. Targeted direct reader for a specific device
5. Optional helper integration
6. Never use broad generic HID probing by default

See [Adding Devices](docs/ADDING_DEVICES.md).

---

## Bug Reports

Use GitHub Issues for bugs.

Include:

* Device name
* Connection type
* VID/PID, if available
* Windows version
* App version
* Screenshot
* Logs from:

```text
%APPDATA%\UniversalBatteryOverlay\logs
```

Open an issue here:

```text
https://github.com/sparta1st/UniversalBatteryOverlay/issues
```

---

## Troubleshooting

### The app does not open

Run from CMD:

```bat
UniversalBatteryOverlay_Portable.exe
```

Then check logs:

```text
%APPDATA%\UniversalBatteryOverlay\logs
```

### The overlay does not show

Open the tray icon and enable the overlay from settings.

### A device is detected but battery is missing

That usually means Windows can see the device, but the battery is not exposed safely.

### Keyboard input feels broken

Stop the app immediately and open a bug report. The app should not interfere with keyboard input.

---

## Roadmap

Planned improvements:

* Better Logitech headset support
* More Razer wireless profiles
* More SteelSeries profiles
* More Corsair profiles
* More controller support
* Safer optional device-specific readers
* Import/export overlay themes
* Installer package
* Auto-start with Windows
* Better diagnostic report export
* Community device profile database

See [ROADMAP.md](ROADMAP.md).

---

## Changelog

See [CHANGELOG.md](CHANGELOG.md).

Latest major updates:

* `0.40.0` — True portable EXE and charging stabilization
* `0.39.0` — Portable EXE attempt and charging battery fix
* `0.38.0` — Publish EXE fix
* `0.37.0` — Build fix for broad device support
* `0.36.0` — Broad safe wireless device support

---

## License

MIT License. See [LICENSE](LICENSE).

---

## Disclaimer

Universal Battery Overlay is an unofficial project and is not affiliated with Logitech, Razer, QwertyKey, Microsoft, or any other device manufacturer.

Battery readings depend on hardware, firmware, drivers, and Windows support. Some devices may only support detection, not real battery percentage.
