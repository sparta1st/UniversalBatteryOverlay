# Contributing

Thanks for helping improve Universal Battery Overlay.

## Bug reports

Use GitHub Issues and include:

- app version;
- Windows version;
- exact device model;
- connection type;
- whether official software shows the battery;
- logs from `%APPDATA%\UniversalBatteryOverlay\logs`;
- screenshots for UI bugs.

## Device support

Device support is safest when based on known hardware IDs and documented/verified commands. Avoid generic active HID probing, especially for keyboards.

## Pull requests

Before opening a pull request:

```bat
dotnet restore .\src\UniversalBatteryOverlay\UniversalBatteryOverlay.csproj --configfile .\NuGet.config -r win-x64
dotnet build .\src\UniversalBatteryOverlay\UniversalBatteryOverlay.csproj -c Release
```

Keep UI clean, reader failures isolated, and unsupported devices safe.
