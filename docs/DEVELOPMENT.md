# Development

## Stack

- C#
- .NET 8
- WPF
- Windows Forms NotifyIcon for tray behavior
- Windows HID / PnP APIs for device discovery

## Build locally

```bat
CHECK_BUILD.bat
```

or:

```powershell
dotnet build .\src\UniversalBatteryOverlay\UniversalBatteryOverlay.csproj -c Release
```

## Add a C# reader

1. Create a class in `src/UniversalBatteryOverlay/Readers`.
2. Implement `IBatteryReader`.
3. Register it in `BatteryMonitorService`.
4. Return `DeviceBatteryInfo` objects.

## Reader guidelines

- Never block the UI thread.
- Keep reads short and cancellable.
- Use a device lock for active HID commands.
- Return clear diagnostic status.
- Do not show fake battery values.
