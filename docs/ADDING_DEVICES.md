# Adding devices safely

You can add recognition for new devices without changing C# code by creating a `device-profiles.json` file.

## Where to put the file

Place it next to the app or in:

```text
%APPDATA%\UniversalBatteryOverlay\device-profiles.json
```

## Example

```json
[
  {
    "displayName": "Example Wireless Mouse",
    "deviceType": "Mouse",
    "hardwareIds": ["VID_1234&PID_ABCD"],
    "aliases": ["Example Mouse", "Example 2.4G"],
    "passiveOnly": true,
    "showWhenDetectedWithoutBattery": true,
    "notes": "Passive detection only. Battery appears if Windows exposes it."
  }
]
```

## Device types

Use one of these when possible:

- `Mouse`
- `Keyboard`
- `Headset`
- `Controller`
- `Laptop`
- `Device`

## Safety rules

Do not add broad vendor-only tokens such as only `Logitech`, `Razer`, `Corsair`, or `USB`. They can match unrelated devices.

Good tokens:

- exact model names: `G733`, `Viper V2 Pro`, `Arctis Nova 7`;
- exact hardware IDs: `VID_046D&PID_0AB5`;
- unique product strings.

Bad tokens:

- `USB`
- `Wireless`
- `Keyboard`
- `Mouse`
- only a vendor name such as `Razer`.

## Real battery support

A profile only makes the app recognize a device. It does not create a battery percentage by itself.

For proprietary dongle devices, a dedicated reader may be needed. Dedicated readers must:

1. target exact VID/PID paths;
2. avoid keyboards unless the protocol is proven safe;
3. have short timeouts;
4. fail gracefully;
5. never run broad HID brute-force scans.
