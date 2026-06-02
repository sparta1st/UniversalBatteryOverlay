# Custom readers

Custom readers let you support additional devices without recompiling the app.

## Location

Put `.ps1` scripts here:

```text
readers/
```

The app runs these scripts and reads JSON from standard output.

## JSON schema

A script should return an array:

```json
[
  {
    "name": "Example Mouse",
    "deviceType": "Mouse",
    "batteryPercent": 74,
    "isCharging": false,
    "status": "OK",
    "reader": "Example custom reader",
    "rawId": "optional device id"
  }
]
```

## Fields

| Field | Type | Required | Description |
|---|---:|---:|---|
| `name` | string | yes | Device name. |
| `deviceType` | string | yes | `Mouse`, `Keyboard`, `Headset`, `Laptop`, `Battery`, or `Device`. |
| `batteryPercent` | number/null | no | Real battery percent from 0 to 100. Use null if unavailable. |
| `isCharging` | boolean/null | no | Charging state if known. |
| `status` | string | no | Short reader status. |
| `reader` | string | no | Reader name. |
| `rawId` | string | no | VID/PID, path, or other debug id. |

## Rules

- Do not invent battery percentages.
- Keep readers fast; aim for under 2 seconds.
- Avoid sending unknown HID commands to keyboards.
- Use `status` for diagnostics.
