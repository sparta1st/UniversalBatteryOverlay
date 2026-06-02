# Razer direct reader

`RazerViperV2ProHidReader` tries a targeted HID Feature Report battery request for Razer Viper V2 Pro.

Safety rules:

- It targets the known Razer VID/PID only.
- It avoids broad generic HID probing by default.
- Keyboard-like devices are not probed by the generic reader.

If the reader returns a percentage, the overlay displays it as a real value. If it fails, the app keeps the device detected but does not invent a percentage.
