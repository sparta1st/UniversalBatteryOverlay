using Microsoft.Win32;
using UniversalBatteryOverlay.Models;

namespace UniversalBatteryOverlay.Readers;

/// <summary>
/// Passive fallback inventory reader for known wireless devices.
/// It reads Windows registry inventory under HKLM\SYSTEM\CurrentControlSet\Enum only.
/// It never opens HID handles and never sends feature/output reports.
/// </summary>
public sealed class RegistryKnownDeviceReader : IBatteryReader
{
    private readonly Func<IReadOnlyList<DeviceProfile>> _getProfiles;
    public string Name => "Safe Windows registry profile inventory";

    private static readonly string[] EnumRoots =
    {
        @"SYSTEM\CurrentControlSet\Enum\USB",
        @"SYSTEM\CurrentControlSet\Enum\HID",
        @"SYSTEM\CurrentControlSet\Enum\BTHENUM",
        @"SYSTEM\CurrentControlSet\Enum\SWD"
    };

    public RegistryKnownDeviceReader(Func<IReadOnlyList<DeviceProfile>> getProfiles)
    {
        _getProfiles = getProfiles;
    }

    public Task<IReadOnlyList<DeviceBatteryInfo>> ReadAsync(CancellationToken cancellationToken)
    {
        var results = new List<DeviceBatteryInfo>();

        if (!OperatingSystem.IsWindows())
            return Task.FromResult<IReadOnlyList<DeviceBatteryInfo>>(results);

        try
        {
            foreach (var profile in _getProfiles())
            {
                cancellationToken.ThrowIfCancellationRequested();

                var match = FindBestRegistryMatch(profile, cancellationToken);
                if (match is null) continue;

                results.Add(new DeviceBatteryInfo
                {
                    Name = profile.DisplayName,
                    DeviceType = profile.DeviceType,
                    BatteryPercent = null,
                    IsCharging = null,
                    Status = "Detected safely from Windows registry inventory. No HID commands were sent.",
                    Reader = Name,
                    RawId = match,
                    LastUpdated = DateTime.Now
                });
            }
        }
        catch (Exception ex)
        {
            StartupLogger.Error("Registry profile inventory failed", ex);
        }

        return Task.FromResult<IReadOnlyList<DeviceBatteryInfo>>(results);
    }

    private static string? FindBestRegistryMatch(DeviceProfile profile, CancellationToken cancellationToken)
    {
        foreach (var enumRoot in EnumRoots)
        {
            using var root = Registry.LocalMachine.OpenSubKey(enumRoot);
            if (root is null) continue;

            foreach (var deviceKeyName in root.GetSubKeyNames())
            {
                cancellationToken.ThrowIfCancellationRequested();

                var topMatches = profile.MatchTokens.Any(t => deviceKeyName.Contains(t, StringComparison.OrdinalIgnoreCase));

                using var deviceKey = root.OpenSubKey(deviceKeyName);
                if (deviceKey is null) continue;

                var instances = deviceKey.GetSubKeyNames();
                var instanceMatches = new List<RegistryMatch>();

                foreach (var instance in instances)
                {
                    using var instKey = deviceKey.OpenSubKey(instance);
                    if (instKey is null) continue;

                    var friendly = Convert.ToString(instKey.GetValue("FriendlyName"));
                    var desc = Convert.ToString(instKey.GetValue("DeviceDesc"));
                    var className = Convert.ToString(instKey.GetValue("Class"));
                    var text = $"{deviceKeyName} {instance} {friendly} {desc} {className}";
                    if (!topMatches && !profile.MatchTokens.Any(t => text.Contains(t, StringComparison.OrdinalIgnoreCase))) continue;

                    instanceMatches.Add(new RegistryMatch(
                        Path: enumRoot + @"\" + deviceKeyName + @"\" + instance,
                        FriendlyName: friendly,
                        DeviceDesc: desc,
                        ClassName: className,
                        Score: ScoreRegistryMatch(profile, text, friendly, desc, className)));
                }

                var best = instanceMatches.OrderByDescending(x => x.Score).FirstOrDefault();
                if (best is not null) return best.Path;

                if (topMatches) return enumRoot + @"\" + deviceKeyName;
            }
        }

        return null;
    }

    private static int ScoreRegistryMatch(DeviceProfile profile, string text, string? friendly, string? desc, string? className)
    {
        var score = 0;
        foreach (var id in profile.HardwareIds)
            if (text.Contains(id, StringComparison.OrdinalIgnoreCase)) score += 1000;
        foreach (var alias in profile.Aliases)
            if (text.Contains(alias, StringComparison.OrdinalIgnoreCase)) score += 100;
        if (!string.IsNullOrWhiteSpace(friendly)) score += 50;
        if (!string.IsNullOrWhiteSpace(desc)) score += 15;
        if (!string.IsNullOrWhiteSpace(className) && className.Contains(profile.DeviceType, StringComparison.OrdinalIgnoreCase)) score += 25;
        return score;
    }

    private sealed record RegistryMatch(string Path, string? FriendlyName, string? DeviceDesc, string? ClassName, int Score);
}
