# Roadmap

This project should grow safely. The priority is: **no input interference, no lag, clean UI, accurate battery values when possible**.

## Short term

- Add a device-support wizard that exports hardware IDs and diagnostic logs.
- Add a safer profile editor inside the UI.
- Add a release ZIP created automatically by GitHub Actions.
- Add optional portable settings export/import.
- Improve Logitech G733 battery parsing with more real-world samples.

## Medium term

- Add more exact targeted readers, one device family at a time:
  - Logitech HID++ devices where safe;
  - additional Razer mice with exact VID/PID paths;
  - selected SteelSeries/Corsair/HyperX headsets if safe protocols are confirmed.
- Add per-device refresh intervals.
- Add per-device hide/rename controls.
- Add tray notifications for low battery.

## Long term

- Create a community device database.
- Add signed releases.
- Add plugin API for safe model-specific readers.
- Add optional cloud-free update checker.

## What will not be added by default

- Generic HID brute-force probing.
- Active reads against unknown keyboards.
- Background vendor-app dependencies as mandatory requirements.
- Anything that risks breaking keyboard/mouse input.
