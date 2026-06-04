## [0.40.0] - True Portable EXE + Charging Stabilization

### Added

* Added a true portable build output: `UniversalBatteryOverlay_Portable.exe`.
* Added stronger startup logging for portable EXE launches.
* Added stricter charging battery stabilization logic.

### Changed

* `publish.bat` now creates a single portable EXE in the project root.
* The portable EXE is intended to be copied anywhere, including Desktop, without requiring the `dist` folder.
* Single-file and self-contained publish settings are now enforced in both the project file and the publish script.
* Embedded application icon support so the EXE no longer depends on external icon files.

### Fixed

* Fixed portable EXE not starting when copied outside the publish folder.
* Fixed dependency issues caused by copying only the EXE from the wrong build directory.
* Improved handling of fake charging battery percentages.
* Prevented sudden unstable jumps such as `55% → 100% → 55%` when a headset is plugged in.

---

## [0.39.0] - Portable EXE Attempt + Charging Battery Fix

### Added

* Added a simplified portable publish output under `dist`.
* Added improved charging state handling for headset and wireless devices.
* Added battery value validation for unstable charging readings.

### Changed

* `publish.bat` was updated to create a self-contained, single-file EXE.
* Charging display now shows the lightning indicator immediately while keeping the last trusted percentage when the reported value is unstable.
* Battery readings are now filtered to avoid unrealistic spikes.

### Fixed

* Fixed false `100%` battery readings while charging.
* Fixed rapid battery percentage jumps during charging.
* Reduced dependency on external files for published builds.

---

## [0.38.0] - Publish EXE Fix

### Added

* Added improved publish flow for Windows EXE builds.
* Added clearer publish output path documentation.
* Added startup behavior improvements so the main window is forced to appear when launching the app.

### Changed

* `publish.bat` now creates a self-contained Windows build.
* Publish output was reorganized to make it easier to find the generated EXE.
* README instructions were updated to explain how to run the published build.

### Fixed

* Fixed published app not opening when launched from the publish folder.
* Fixed confusion between framework-dependent and self-contained builds.
* Fixed cases where the app could launch and immediately hide without clear feedback.

---

## [0.37.0] - Build Fix for Broad Device Support

### Added

* Added missing global service imports required by new broad support readers.

### Changed

* Kept the broad safe device profile system introduced in v36.
* Kept targeted readers for known devices while avoiding generic HID scanning.

### Fixed

* Fixed build error where `StartupLogger` was not found in:

  * `KnownVidPidPresenceReader.cs`
  * `RegistryKnownDeviceReader.cs`
* Fixed project build failure introduced by the broad device support update.

---

## [0.36.0] - Broad Safe Wireless Device Support

### Added

* Added broader safe device profile support for many wireless device families, including:

  * Logitech
  * Razer
  * SteelSeries
  * Corsair
  * HyperX
  * ASUS ROG / TUF
  * Turtle Beach / ROCCAT
  * Glorious
  * Pulsar
  * Lamzu
  * Finalmouse
  * Keychron
  * NuPhy
  * Akko
  * Xbox controllers
  * DualSense / DualShock controllers
  * Nintendo Switch Pro Controller
  * 8BitDo controllers
