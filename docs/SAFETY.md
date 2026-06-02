# Safety notes

Wireless keyboards and mice can expose multiple HID interfaces. Sending unknown output or feature reports to the wrong interface can interfere with input.

Universal Battery Overlay follows these rules:

- Keyboard readers are passive by default.
- Unknown HID commands are not sent to keyboards.
- Direct HID commands are targeted to known devices and filtered by VID/PID and HID capabilities.
- Generic HID reading can be disabled in Advanced settings.
- Device-specific readers use locks to avoid overlapping reads.

If a device behaves strangely, disable universal HID reading and restart the app.
