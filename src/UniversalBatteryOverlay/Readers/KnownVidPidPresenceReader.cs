using System.Text.Json;
using UniversalBatteryOverlay.Models;
using UniversalBatteryOverlay.Utils;

namespace UniversalBatteryOverlay.Readers;

public sealed class KnownVidPidPresenceReader : IBatteryReader
{
    public string Name => "Safe wireless device presence";

    private static readonly KnownDevice[] KnownDevices =
    {
        new("Razer Viper V2 Pro", "Mouse", new[] { "VID_1532&PID_00A6", "VID_1532&PID_00A5", "Razer Viper V2 Pro", "Viper V2 Pro" }),
        new("Logitech G733", "Headset", new[] { "VID_046D&PID_0AB5", "Logitech G733", "G733", "G733 Gaming Headset" }),
        new("QwertyKey Keyboard", "Keyboard", new[] { "VID_36B0&PID_3002", "QwertyKey", "QWERTYKEY" })
    };

    public async Task<IReadOnlyList<DeviceBatteryInfo>> ReadAsync(CancellationToken cancellationToken)
    {
        const string script = @"
$ErrorActionPreference = 'SilentlyContinue'
$script:items = @()
$needles = 'VID_1532&PID_00A6|VID_1532&PID_00A5|VID_046D&PID_0AB5|VID_36B0&PID_3002|Razer Viper V2 Pro|Viper V2 Pro|Logitech G733|G733|G733 Gaming Headset|QwertyKey|QWERTYKEY'

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

# Source 1: present PnP inventory. Passive: it does not open HID handles.
Get-PnpDevice -PresentOnly -ErrorAction SilentlyContinue | Where-Object {
    ($_.FriendlyName -match $needles) -or ($_.InstanceId -match $needles)
} | ForEach-Object {
    Add-Device $_.FriendlyName $_.Class $_.InstanceId $_.Status 'Get-PnpDevice PresentOnly'
}

# Source 2: full PnP inventory. Some wireless headsets appear here before their audio endpoint refreshes.
Get-PnpDevice -ErrorAction SilentlyContinue | Where-Object {
    ($_.FriendlyName -match $needles) -or ($_.InstanceId -match $needles)
} | ForEach-Object {
    Add-Device $_.FriendlyName $_.Class $_.InstanceId $_.Status 'Get-PnpDevice All'
}

# Source 3: WMI/CIM fallback. Useful for G733 audio/media endpoints that Windows does not always
# expose through the same PnP list until a charging/reconnect event occurs.
Get-CimInstance Win32_PnPEntity -ErrorAction SilentlyContinue | Where-Object {
    ($_.Name -match $needles) -or ($_.DeviceID -match $needles)
} | ForEach-Object {
    Add-Device $_.Name $_.PNPClass $_.DeviceID $_.Status 'Win32_PnPEntity'
}

# Source 4: audio devices fallback for wireless headsets like G733. Still passive.
Get-CimInstance Win32_SoundDevice -ErrorAction SilentlyContinue | Where-Object {
    ($_.Name -match 'G733|Logitech') -or ($_.DeviceID -match 'VID_046D&PID_0AB5')
} | ForEach-Object {
    Add-Device $_.Name 'MEDIA' $_.DeviceID $_.Status 'Win32_SoundDevice'
}

# Source 5: registry fallback for exact known USB/HID dongles. This avoids missing a device when
# Windows does not refresh the audio endpoint until a charging/reconnect status change.
foreach ($enumRoot in @('HKLM:\SYSTEM\CurrentControlSet\Enum\USB','HKLM:\SYSTEM\CurrentControlSet\Enum\HID')) {
    foreach ($top in Get-ChildItem $enumRoot -ErrorAction SilentlyContinue) {
        if ($top.PSChildName -match 'VID_1532&PID_00A6|VID_1532&PID_00A5|VID_046D&PID_0AB5|VID_36B0&PID_3002') {
            foreach ($inst in Get-ChildItem $top.PSPath -ErrorAction SilentlyContinue) {
                $props = Get-ItemProperty $inst.PSPath -ErrorAction SilentlyContinue
                $friendly = $props.FriendlyName
                if ([string]::IsNullOrWhiteSpace($friendly)) { $friendly = $props.DeviceDesc }
                if ([string]::IsNullOrWhiteSpace($friendly)) { $friendly = $top.PSChildName }
                Add-Device $friendly $props.Class ($inst.Name -replace '^HKEY_LOCAL_MACHINE\\','') 'Registry' 'Registry Enum'
            }
        }
    }
}

$script:items | Where-Object { $_.FriendlyName -or $_.InstanceId } | Sort-Object InstanceId,FriendlyName,Source -Unique | ConvertTo-Json -Depth 4 -Compress
";

        try
        {
            var stdout = await PowerShellScriptRunner.RunAsync(script, TimeSpan.FromSeconds(10), cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(stdout) || !stdout.TrimStart().StartsWith("[", StringComparison.Ordinal) && !stdout.TrimStart().StartsWith("{", StringComparison.Ordinal))
                return Array.Empty<DeviceBatteryInfo>();

            var pnpDevices = ParseDevices(stdout);
            var results = new List<DeviceBatteryInfo>();

            foreach (var known in KnownDevices)
            {
                var matches = pnpDevices
                    .Where(d => known.Needles.Any(n => Contains(d.InstanceId, n) || Contains(d.FriendlyName, n)))
                    .OrderBy(d => DeviceRank(d, known))
                    .ToList();

                var best = matches.FirstOrDefault();
                if (best is null) continue;

                results.Add(new DeviceBatteryInfo
                {
                    Name = known.DisplayName,
                    DeviceType = known.DeviceType,
                    BatteryPercent = null,
                    IsCharging = null,
                    Status = $"Detected safely via {best.Source}. Battery percentage depends on a specific reader or Windows exposure.",
                    Reader = Name,
                    RawId = best.InstanceId ?? best.FriendlyName,
                    LastUpdated = DateTime.Now
                });
            }

            return results;
        }
        catch
        {
            return Array.Empty<DeviceBatteryInfo>();
        }
    }

    private static bool Contains(string? text, string needle)
        => !string.IsNullOrWhiteSpace(text) && text.Contains(needle, StringComparison.OrdinalIgnoreCase);

    private static int DeviceRank(PnpDevice device, KnownDevice known)
    {
        var name = device.FriendlyName ?? string.Empty;
        var cls = device.Class ?? string.Empty;
        var source = device.Source ?? string.Empty;
        var text = $"{name} {cls} {source}".ToLowerInvariant();

        if (known.DeviceType == "Headset" && text.Contains("g733") && (text.Contains("media") || text.Contains("audio") || text.Contains("sound"))) return 0;
        if (known.DeviceType == "Headset" && text.Contains("g733")) return 1;
        if (known.DeviceType == "Mouse" && text.Contains("viper")) return 0;
        if (known.DeviceType == "Keyboard" && (text.Contains("keyboard") || text.Contains("qwerty"))) return 0;
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

    private sealed record KnownDevice(string DisplayName, string DeviceType, IReadOnlyList<string> Needles);

    private sealed class PnpDevice
    {
        public string? FriendlyName { get; set; }
        public string? Class { get; set; }
        public string? InstanceId { get; set; }
        public string? Status { get; set; }
        public string? Source { get; set; }
    }
}
