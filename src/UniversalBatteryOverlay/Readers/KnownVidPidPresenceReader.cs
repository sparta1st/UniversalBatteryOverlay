using System.Text.Json;
using UniversalBatteryOverlay.Models;
using UniversalBatteryOverlay.Utils;

namespace UniversalBatteryOverlay.Readers;

/// <summary>
/// Passive wireless-device inventory reader.
/// It does not open HID handles and does not send reports. It only asks Windows which devices exist.
/// </summary>
public sealed class KnownVidPidPresenceReader : IBatteryReader
{
    private readonly Func<IReadOnlyList<DeviceProfile>> _getProfiles;
    public string Name => "Safe wireless profile detector";

    public KnownVidPidPresenceReader(Func<IReadOnlyList<DeviceProfile>> getProfiles)
    {
        _getProfiles = getProfiles;
    }

    public async Task<IReadOnlyList<DeviceBatteryInfo>> ReadAsync(CancellationToken cancellationToken)
    {
        var profiles = _getProfiles()
            .Where(p => p.MatchTokens.Any())
            .ToList();

        if (profiles.Count == 0)
            return Array.Empty<DeviceBatteryInfo>();

        var exactHardwareRegex = RegexFromTokens(profiles.SelectMany(p => p.HardwareIds));
        var aliasRegex = RegexFromTokens(profiles.SelectMany(p => p.Aliases));
        var rx = string.IsNullOrWhiteSpace(exactHardwareRegex)
            ? aliasRegex
            : string.IsNullOrWhiteSpace(aliasRegex)
                ? exactHardwareRegex
                : exactHardwareRegex + "|" + aliasRegex;

        if (string.IsNullOrWhiteSpace(rx))
            return Array.Empty<DeviceBatteryInfo>();

        var script = $$"""
$ErrorActionPreference = 'SilentlyContinue'
$script:items = @()
$rx = '{{rx}}'

function Add-Device($friendlyName, $className, $instanceId, $status, $source) {
    if ([string]::IsNullOrWhiteSpace($friendlyName) -and [string]::IsNullOrWhiteSpace($instanceId)) { return }
    $script:items += [pscustomobject]@{
        FriendlyName = [string]$friendlyName
        Class        = [string]$className
        InstanceId   = [string]$instanceId
        Status       = [string]$status
        Source       = [string]$source
    }
}

# Present PnP inventory. Passive only.
Get-PnpDevice -PresentOnly -ErrorAction SilentlyContinue | Where-Object {
    ($_.FriendlyName -match $rx) -or ($_.InstanceId -match $rx)
} | ForEach-Object {
    Add-Device $_.FriendlyName $_.Class $_.InstanceId $_.Status 'Get-PnpDevice PresentOnly'
}

# Full PnP inventory fallback. Some wireless dongles/headsets do not show in PresentOnly until a state change.
Get-PnpDevice -ErrorAction SilentlyContinue | Where-Object {
    ($_.FriendlyName -match $rx) -or ($_.InstanceId -match $rx)
} | ForEach-Object {
    Add-Device $_.FriendlyName $_.Class $_.InstanceId $_.Status 'Get-PnpDevice All'
}

# CIM PnP fallback.
Get-CimInstance Win32_PnPEntity -ErrorAction SilentlyContinue | Where-Object {
    ($_.Name -match $rx) -or ($_.DeviceID -match $rx)
} | ForEach-Object {
    Add-Device $_.Name $_.PNPClass $_.DeviceID $_.Status 'Win32_PnPEntity'
}

# Audio endpoint fallback for wireless headsets.
Get-CimInstance Win32_SoundDevice -ErrorAction SilentlyContinue | Where-Object {
    ($_.Name -match $rx) -or ($_.DeviceID -match $rx)
} | ForEach-Object {
    Add-Device $_.Name 'MEDIA' $_.DeviceID $_.Status 'Win32_SoundDevice'
}

$script:items | Where-Object { $_.FriendlyName -or $_.InstanceId } | Sort-Object InstanceId,FriendlyName,Source -Unique | ConvertTo-Json -Depth 4 -Compress
""";

        try
        {
            var stdout = await PowerShellScriptRunner.RunAsync(script, TimeSpan.FromSeconds(10), cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(stdout) || !stdout.TrimStart().StartsWith("[", StringComparison.Ordinal) && !stdout.TrimStart().StartsWith("{", StringComparison.Ordinal))
                return Array.Empty<DeviceBatteryInfo>();

            var pnpDevices = ParseDevices(stdout);
            var results = new List<DeviceBatteryInfo>();

            foreach (var profile in profiles)
            {
                var matches = pnpDevices
                    .Where(d => profile.MatchTokens.Any(t => Contains(d.InstanceId, t) || Contains(d.FriendlyName, t)))
                    .OrderBy(d => DeviceRank(d, profile))
                    .ToList();

                var best = matches.FirstOrDefault();
                if (best is null) continue;

                results.Add(new DeviceBatteryInfo
                {
                    Name = profile.DisplayName,
                    DeviceType = profile.DeviceType,
                    BatteryPercent = null,
                    IsCharging = null,
                    Status = $"Detected safely via {best.Source}. Battery requires Windows exposure or a dedicated reader.",
                    Reader = Name,
                    RawId = best.InstanceId ?? best.FriendlyName,
                    LastUpdated = DateTime.Now
                });
            }

            return results;
        }
        catch (Exception ex)
        {
            StartupLogger.Error("Safe profile detector failed", ex);
            return Array.Empty<DeviceBatteryInfo>();
        }
    }

    private static string RegexFromTokens(IEnumerable<string> tokens)
        => string.Join("|", tokens
            .Where(t => !string.IsNullOrWhiteSpace(t) && t.Trim().Length >= 3)
            .Select(EscapeRegex)
            .Distinct(StringComparer.OrdinalIgnoreCase));

    private static string EscapeRegex(string value)
        => System.Text.RegularExpressions.Regex.Escape(value.Trim()).Replace("'", "''");

    private static bool Contains(string? text, string needle)
        => !string.IsNullOrWhiteSpace(text) && text.Contains(needle, StringComparison.OrdinalIgnoreCase);

    private static int DeviceRank(PnpDevice device, DeviceProfile profile)
    {
        var name = device.FriendlyName ?? string.Empty;
        var cls = device.Class ?? string.Empty;
        var source = device.Source ?? string.Empty;
        var id = device.InstanceId ?? string.Empty;
        var text = $"{name} {cls} {source} {id}".ToLowerInvariant();

        if (profile.HardwareIds.Any(h => id.Contains(h, StringComparison.OrdinalIgnoreCase))) return 0;
        if (profile.DeviceType.Equals("Headset", StringComparison.OrdinalIgnoreCase) && (text.Contains("media") || text.Contains("audio") || text.Contains("sound"))) return 1;
        if (profile.DeviceType.Equals("Mouse", StringComparison.OrdinalIgnoreCase) && text.Contains("mouse")) return 1;
        if (profile.DeviceType.Equals("Keyboard", StringComparison.OrdinalIgnoreCase) && text.Contains("keyboard")) return 1;
        if (profile.DeviceType.Equals("Controller", StringComparison.OrdinalIgnoreCase) && (text.Contains("controller") || text.Contains("gamepad"))) return 1;
        if (text.Contains("vendor-defined")) return 5;
        if (text.Contains("usb input device")) return 8;
        return 9;
    }

    private static IReadOnlyList<PnpDevice> ParseDevices(string json)
    {
        var list = new List<PnpDevice>();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (root.ValueKind == JsonValueKind.Object)
        {
            list.Add(ToDevice(root));
        }
        else if (root.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in root.EnumerateArray())
                if (item.ValueKind == JsonValueKind.Object) list.Add(ToDevice(item));
        }

        return list;
    }

    private static PnpDevice ToDevice(JsonElement item) => new()
    {
        FriendlyName = GetString(item, "FriendlyName"),
        Class = GetString(item, "Class"),
        InstanceId = GetString(item, "InstanceId"),
        Status = GetString(item, "Status"),
        Source = GetString(item, "Source")
    };

    private static string? GetString(JsonElement item, string name)
    {
        if (!item.TryGetProperty(name, out var v) || v.ValueKind == JsonValueKind.Null) return null;
        return v.ToString();
    }

    private sealed class PnpDevice
    {
        public string? FriendlyName { get; set; }
        public string? Class { get; set; }
        public string? InstanceId { get; set; }
        public string? Status { get; set; }
        public string? Source { get; set; }
    }
}
