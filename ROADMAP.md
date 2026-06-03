# Roadmap

This roadmap tracks planned improvements for Universal Battery Overlay.

## Short-term

- Improve the settings UI spacing and layout based on real desktop screenshots.
- Add more overlay presets:
  - Minimal;
  - Gaming;
  - Compact glass;
  - High contrast.
- Add a reset button for overlay appearance settings.
- Add clearer device status messages in the Devices tab.
- Add a button to open the logs folder from the UI.
- Improve Logitech G733 stability while charging.

## Device support goals

- Add more targeted readers only when they can be implemented safely.
- Avoid generic HID probing that can interfere with keyboard input.
- Add device support through GitHub device-support requests.
- Document hardware IDs for tested devices.

Potential future targets:

- More Logitech Lightspeed devices.
- More Razer wireless devices.
- SteelSeries wireless headsets.
- Corsair wireless devices.
- Xbox and PlayStation controllers when Windows exposes reliable battery data.
- Bluetooth devices that expose standard battery services.

## UI goals

- Cleaner modern dashboard.
- Better visual hierarchy for the Overlay tab.
- Better scrollbar styling.
- Better color picker experience.
- Theme presets.
- Import/export settings profile.

## Packaging goals

- Add a GitHub Actions artifact for published builds.
- Add versioned GitHub Releases.
- Add a simple installer in the future.
- Add signed releases if the project grows.

## Safety rules that will stay

- No generic HID scanner enabled by default.
- No unknown commands sent to keyboards.
- QwertyKey and unknown keyboards stay passive-only.
- Direct readers must be targeted to exact known VID/PID paths.
- If a device does not expose battery and no safe reader exists, the app should say that clearly instead of inventing a percentage.
