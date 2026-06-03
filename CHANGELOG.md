# Changelog

## 0.36.0

### Added

- Expanded built-in safe profiles for many more wireless devices:
  - Logitech G/LIGHTSPEED/MX mice, keyboards and headsets;
  - Razer wireless mice, keyboards and headsets;
  - SteelSeries Arctis/Nova, Aerox, Rival, Prime and Apex families;
  - Corsair wireless mice, keyboards and headsets;
  - HyperX Cloud and Pulsefire wireless families;
  - ASUS ROG/TUF wireless peripherals;
  - Turtle Beach/ROCCAT, Glorious, Pulsar, Lamzu, Finalmouse, WLmouse, Ninjutso, Endgame Gear and Zowie devices;
  - Keychron, NuPhy, Akko, Epomaker, Royal Kludge, Anne Pro and similar Bluetooth keyboards;
  - Xbox, PlayStation, Nintendo, 8BitDo, Flydigi, GuliKit, SCUF and Victrix controllers;
  - AirPods, Sony, Bose, Sennheiser, JBL, Beats, Jabra, Soundcore, Galaxy Buds, Pixel Buds and other Bluetooth headsets.
- Reader cadence cache to reduce lag: slow Windows inventory readers are not executed every overlay tick.
- Cleaner default filtering: unknown devices are hidden by default to avoid random USB clutter.
- More polished button and tab styling in the main UI.

### Changed

- Kept generic HID scanning disabled.
- Kept QwertyKey and unknown keyboards passive-only.
- Kept Razer Viper V2 Pro and Logitech G733 as targeted direct readers only.
- Updated README, roadmap and device-support documentation.

### Safety

- No new generic active probing was added.
- Active reads remain limited to exact supported hardware IDs.

## 0.35.0

### Added

- Device profile system for safe passive detection.
- External `device-profiles.json` support.
- Documentation for adding devices safely.

## 0.34.1

### Fixed

- WPF/Windows Forms brush ambiguity in overlay UI customization build.

## Earlier development builds

- Added Windows tray behavior.
- Added compact overlay.
- Added Razer Viper V2 Pro targeted reader.
- Added experimental Logitech G733 direct reader and optional HeadsetControl helper.
- Added keyboard-safe mode after generic HID probing caused interference on some keyboards.
