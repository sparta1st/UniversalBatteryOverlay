# Safety

Universal Battery Overlay is designed to avoid interfering with keyboard and mouse input.

## What the app does

- Reads passive Windows device inventory.
- Reads passive Windows battery properties.
- Recognizes known wireless device names and hardware IDs.
- Uses targeted direct readers only for known supported devices.
- Uses reader timeouts and caching to avoid lag.

## What the app does not do

- No generic HID brute-force probing.
- No active commands to unknown keyboards.
- No active commands to QwertyKey.
- No scanning every HID report ID on every device.
- No mandatory vendor background apps.

## Why this matters

Some keyboards and 2.4 GHz dongles can behave badly if an app sends unexpected HID reports. Older experimental builds proved this risk. Current builds avoid that pattern.

## Adding a direct reader

A direct reader should be allowed only when all of these are true:

- exact VID/PID is known;
- target interface is understood;
- read method is tested;
- it does not open unrelated keyboard interfaces;
- it has a timeout;
- it returns cleanly when the device does not respond.
