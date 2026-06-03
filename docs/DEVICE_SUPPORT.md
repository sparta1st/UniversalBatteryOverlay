# Device support

Universal Battery Overlay uses a safe multi-layer support model.

## Support levels

| Level | Description | Battery value | Risk |
|---|---|---:|---|
| Targeted direct reader | Exact code for a known hardware ID. | Real percentage when protocol works. | Low, because it targets only that hardware ID. |
| Passive Windows battery | Windows exposes a battery property. | Real percentage. | Very low. |
| Optional helper | A CLI such as HeadsetControl is used on demand. | Real percentage when supported. | Low; no background vendor service required. |
| Passive profile | Device is recognized by name/VID/PID only. | No percentage unless Windows exposes it. | Very low. |

## Current targeted readers

| Device | Status | Notes |
|---|---:|---|
| Razer Viper V2 Pro | Supported | Targeted reader for known Viper V2 Pro VID/PID paths. |
| Logitech G733 | Experimental | Targeted reader plus optional HeadsetControl helper. Some devices may report unstable values while charging; values are stabilized. |
| System/laptop battery | Supported | Uses normal Windows APIs. |

## Passive profile families

The app includes passive profiles for common wireless families from:

- Logitech
- Razer
- SteelSeries
- Corsair
- HyperX
- ASUS ROG/TUF
- Turtle Beach / ROCCAT
- Glorious
- Pulsar
- Lamzu
- Finalmouse
- WLmouse
- Ninjutso
- Endgame Gear
- Zowie
- Keychron
- NuPhy
- Akko
- Epomaker
- Royal Kludge
- Microsoft Xbox
- Sony PlayStation / DualSense / DualShock
- Nintendo
- 8BitDo
- Flydigi
- GuliKit
- SCUF
- Victrix
- Apple AirPods
- Sony, Bose, Sennheiser, JBL, Beats, Jabra, Soundcore, Galaxy Buds, Pixel Buds, Shokz and other Bluetooth headset families.

Passive profiles are safe recognition rules. They do not guarantee a real percentage. A real percentage needs either Windows battery exposure or a dedicated reader.

## Why some devices only show as detected

Many 2.4 GHz dongle devices do not expose a standard battery value to Windows. Vendor apps can show the percentage because they use private commands.

The app avoids generic probing because that can interfere with keyboards. New direct readers should be added one model/family at a time and tested safely.

## How to request support

Open a **Device support request** on GitHub and include:

- exact device model;
- connection type: dongle, Bluetooth, wired;
- hardware IDs from Device Manager;
- whether the vendor app shows a battery percentage;
- screenshots/logs from `%APPDATA%\\UniversalBatteryOverlay\\logs`.
