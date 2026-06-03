using Microsoft.Win32;
using UniversalBatteryOverlay.Models;

namespace UniversalBatteryOverlay.Readers;

/// <summary>
/// Passive fallback inventory reader for exact known wireless devices.
/// It only reads Windows registry inventory under HKLM\SYSTEM\CurrentControlSet\Enum.
/// It never opens HID handles and never sends feature/output reports, so it cannot take over keyboard input.
/// </summary>
public sealed class RegistryKnownDeviceReader : IBatteryReader
{
    public string Name => "Safe Windows registry inventory";

    private static readonly KnownDevice[] KnownDevices =
    {
        new("Razer Viper V2 Pro", "Mouse", new[] { "VID_1532&PID_00A6", "VID_1532&PID_00A5" }),
        new("Logitech G733", "Headset", new[] { "VID_046D&PID_0AB5" }),
        new("QwertyKey Keyboard", "Keyboard", new[] { "VID_36B0&PID_3002" })
    };

    private static readonly string[] EnumRoots =
    {
        @"SYSTEM\CurrentControlSet\Enum\USB",
        @"SYSTEM\CurrentControlSet\Enum\HID"
    };

    public Task<IReadOnlyList<DeviceBatteryInfo>> ReadAsync(CancellationToken cancellationToken)
    {
        var results = new List<DeviceBatteryInfo>();

        if (!OperatingSystem.IsWindows())
            return Task.FromResult<IReadOnlyList<DeviceBatteryInfo>>(results);

        try
        {
            foreach (var known in KnownDevices)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var match = FindBestRegistryMatch(known, cancellationToken);
                if (match is null) continue;

                results.Add(new DeviceBatteryInfo
                {
                    Name = known.DisplayName,
                    DeviceType = known.DeviceType,
                    BatteryPercent = null,
                    IsCharging = null,
                    Status = "Detected safely from Windows registry inventory. No HID commands were sent.",
                    Reader = Name,
                    RawId = match,
                    LastUpdated = DateTime.Now
                });
            }
        }
        catch
        {
            // Registry inventory is best-effort only. Never break the app because of access/registry errors.
        }

        return Task.FromResult<IReadOnlyList<DeviceBatteryInfo>>(results);
    }

    private static string? FindBestRegistryMatch(KnownDevice known, CancellationToken cancellationToken)
    {
        foreach (var enumRoot in EnumRoots)
        {
            using var root = Registry.LocalMachine.OpenSubKey(enumRoot);
            if (root is null) continue;

            foreach (var deviceKeyName in root.GetSubKeyNames())
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!known.HardwareIds.Any(id => deviceKeyName.Contains(id, StringComparison.OrdinalIgnoreCase)))
                    continue;

                using var deviceKey = root.OpenSubKey(deviceKeyName);
                if (deviceKey is null) continue;

                var instances = deviceKey.GetSubKeyNames();
                if (instances.Length == 0)
                    return enumRoot + @"\" + deviceKeyName;

                // Prefer entries with a friendly name/device description. These are usually the active/current ones.
                var best = instances
                    .Select(instance => new RegistryMatch(
                        Path: enumRoot + @"\" + deviceKeyName + @"\" + instance,
                        Score: ScoreInstance(deviceKey.OpenSubKey(instance))))
                    .OrderByDescending(x => x.Score)
                    .FirstOrDefault();

                return best?.Path ?? enumRoot + @"\" + deviceKeyName + @"\" + instances[0];
            }
        }

        return null;
    }

    private static int ScoreInstance(RegistryKey? instanceKey)
    {
        if (instanceKey is null) return 0;
        using (instanceKey)
        {
            var friendly = instanceKey.GetValue("FriendlyName")?.ToString() ?? string.Empty;
            var desc = instanceKey.GetValue("DeviceDesc")?.ToString() ?? string.Empty;
            var cls = instanceKey.GetValue("Class")?.ToString() ?? string.Empty;
            var problem = instanceKey.GetValue("Problem")?.ToString() ?? string.Empty;
            var configFlags = instanceKey.GetValue("ConfigFlags")?.ToString() ?? string.Empty;

            var score = 0;
            if (!string.IsNullOrWhiteSpace(friendly)) score += 10;
            if (!string.IsNullOrWhiteSpace(desc)) score += 3;
            if (cls.Contains("MEDIA", StringComparison.OrdinalIgnoreCase) || cls.Contains("HIDClass", StringComparison.OrdinalIgnoreCase)) score += 3;
            if (string.IsNullOrWhiteSpace(problem) || problem == "0") score += 2;
            if (string.IsNullOrWhiteSpace(configFlags) || configFlags == "0") score += 2;
            return score;
        }
    }

    private sealed record KnownDevice(string DisplayName, string DeviceType, IReadOnlyList<string> HardwareIds);
    private sealed record RegistryMatch(string Path, int Score);
}
