# Changelog

All notable changes to Universal Battery Overlay are documented here.

## 0.34.1 - Clean GitHub release candidate

### Fixed

- Fixed WPF/WinForms namespace ambiguity for `Brush` in `MainWindow.xaml.cs`.
- Prevented `System.Drawing.Brush` and `System.Windows.Media.Brush` conflicts by explicitly using WPF media brushes in UI preview code.

### Changed

- Prepared the project for a clean GitHub replacement upload.
- Updated README with current run/publish instructions.
- Removed outdated root-script instructions from documentation.
- Clarified the current device-support model and keyboard-safety rules.
- Added a clearer roadmap for future development.

## 0.34.0 - Overlay layout and color customization

### Added

- Overlay color customization:
  - background color;
  - row background color;
  - border color;
  - text color;
  - percentage color.
- Improved overlay preview that mirrors the desktop overlay settings more closely.

### Changed

- Reworked the Overlay tab into clearer sections: Placement, Realtime and Visual style.
- Made display and position selectors more compact.
- Increased default app height to reduce cramped layouts.
- Kept the working device-detection logic unchanged.

## 0.33.0 - UI polish and monitor names

### Added

- Friendly monitor names such as `Display 1 · Primary · 1920×1080` instead of raw Windows display IDs.
- Thin dark scrollbars.

### Changed

- Increased the default window height.
- Reduced nested scrolling in settings pages.
- Improved dark UI contrast for ComboBox and TextBox controls.
- Improved the overlay preview layout.

## 0.32.0 - Premium UI refresh

### Added

- Organized tabs:
  - Overview;
  - Overlay;
  - Devices;
  - Safety;
  - Diagnostics;
  - About.
- Cleaner device cards and battery bars.

### Changed

- Refreshed the main UI theme.
- Improved dark input styling.
- Kept the working v31 device readers unchanged.

## 0.31.0 - Build and Logitech reader fix

### Fixed

- Fixed C# overload ambiguity in the Logitech G733 direct reader caused by mixed `int` and HID report-length types.

### Changed

- Kept targeted-safe readers enabled.
- Kept generic HID scanning disabled.

## 0.30.0 - Clean safe complete build

### Changed

- Cleaned the repository structure.
- Kept only simple root scripts: `run.bat` and `publish.bat`.
- Kept targeted device readers.
- Preserved keyboard-safety rules.

## Earlier experimental builds

The project went through several experimental builds to test:

- Razer Viper V2 Pro direct HID battery reading;
- Logitech G733 direct frame reading;
- charging detection;
- realtime refresh;
- tray behavior;
- clean UI iterations;
- keyboard-safe device filtering.

Some older approaches, especially wide/generic HID probing, were removed because they could interfere with keyboards or dongles.
