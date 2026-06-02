# Safety notes

v16 keeps **generic active HID scanning disabled**, because that is what can interfere with keyboards and dongles.

Disabled by default:

- `StandardHidBatteryReader`
- `LogitechG733NativeHidReader`

Enabled:

- `RazerViperV2ProHidReader`, but only for `VID_1532&PID_00A6/00A5` + `MI_00`.

The Razer reader does not touch QwertyKey (`VID_36B0&PID_3002`), Logitech G733 (`VID_046D&PID_0AB5`) or Razer `MI_01/MI_02` keyboard/media-like interfaces.
