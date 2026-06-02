# Device reader guide

Readers are isolated. If one reader fails, the app keeps running.

## Built-in safe readers

- System battery reader
- Windows PnP battery property reader
- Known USB/HID presence reader
- Razer Viper V2 Pro direct HID reader
- Logitech G733 direct reader
- Optional HeadsetControl CLI reader

## Custom readers

You can add PowerShell scripts inside:

`readers\`

A script should output JSON objects with these fields:

```json
{
  "name": "My Device",
  "deviceType": "Mouse",
  "batteryPercent": 75,
  "isCharging": false,
  "status": "OK",
  "reader": "My custom reader"
}
```

The overlay only displays devices with a real `batteryPercent` value.
