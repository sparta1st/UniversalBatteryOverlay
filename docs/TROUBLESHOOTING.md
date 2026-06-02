# Troubleshooting

## The app opens in Notepad

You probably opened a `.ps1` file directly. Use the `.bat` files instead:

```bat
START_APP.bat
```

## `No .NET SDKs were found`

Install the .NET 8 SDK, then reopen CMD/PowerShell:

```bat
winget install -e --id Microsoft.DotNet.SDK.8
```

## Device is detected but battery shows `—`

The device is visible to Windows, but no reader returned a real percentage. This is common for some 2.4 GHz wireless dongles.

Try:

- Reconnect the dongle.
- Use a direct motherboard USB port instead of a hub.
- Run `DEBUG_DEVICES.bat`.
- Add a custom reader if the device has a known protocol.

## The settings window disappears when I press X

That is expected. The app hides to the tray. Double-click the tray icon to reopen it.

## The overlay does not respond to clicks

Click-through mode is enabled. Disable it in the Overlay tab if you want the overlay to receive mouse clicks.

## Headset percentage jumps while charging

Some headsets return unstable charging values. The app includes stabilization, but the only perfectly correct value is the real value returned by the device. If the device/helper returns fake spikes, the app can only filter them.
