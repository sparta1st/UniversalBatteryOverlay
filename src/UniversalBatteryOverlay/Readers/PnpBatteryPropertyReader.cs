using System.Text.Json;
using UniversalBatteryOverlay.Models;
using UniversalBatteryOverlay.Utils;

namespace UniversalBatteryOverlay.Readers;

public sealed class PnpBatteryPropertyReader : IBatteryReader
{
    private readonly Func<IReadOnlyList<string>> _getHints;
    public string Name => "Passive Windows battery properties";

    public PnpBatteryPropertyReader(Func<IReadOnlyList<string>> getHints)
    {
        _getHints = getHints;
    }

    public async Task<IReadOnlyList<DeviceBatteryInfo>> ReadAsync(CancellationToken cancellationToken)
    {
        var hints = _getHints()
            .Where(x => !string.IsNullOrWhiteSpace(x) && x.Length >= 3)
            .Select(EscapeRegex)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var regex = hints.Length == 0 ? "a^" : string.Join("|", hints);

        var ps = $$"""
$ErrorActionPreference = 'SilentlyContinue'
$rx = '{{regex}}'
$items = @()

$devices = Get-PnpDevice -PresentOnly -ErrorAction SilentlyContinue | Where-Object {
    $_.FriendlyName -and (
        ($_.FriendlyName -match $rx) -or ($_.InstanceId -match $rx) -or
        ($_.InstanceId -match 'BTH|Bluetooth|BLE') -or
        ($_.FriendlyName -match 'Wireless|Bluetooth|Receiver|Dongle|Headset|Headphone|Mouse|Keyboard|Controller')
    )
}

foreach ($d in $devices) {
    $isExactTarget = ($d.FriendlyName -match $rx) -or ($d.InstanceId -match $rx)
    $looksWireless = ($d.InstanceId -match 'BTH|Bluetooth|BLE') -or ($d.FriendlyName -match 'Wireless|Bluetooth|Receiver|Dongle|Lightspeed')
    $looksPeripheral = ($d.FriendlyName -match 'Headset|Headphone|Mouse|Keyboard|Controller') -or ($d.Class -match 'Bluetooth|HIDClass|Keyboard|Mouse|MEDIA|AudioEndpoint|Battery')
    if (-not ($isExactTarget -or ($looksWireless -and $looksPeripheral))) { continue }

    $props = Get-PnpDeviceProperty -InstanceId $d.InstanceId -ErrorAction SilentlyContinue | Where-Object {
        $_.KeyName -match 'Battery|Charge|Capacity|Remaining' -and $_.Data -ne $null
    }

    foreach ($p in $props) {
        $val = $null
        try { $val = [int]$p.Data } catch { $val = $null }
        if ($val -ne $null -and $val -ge 0 -and $val -le 100) {
            $charging = $null
            if ($p.KeyName -match 'Charge|Charging') { $charging = $true }
            $items += [pscustomobject]@{
                name = $d.FriendlyName
                deviceType = $d.Class
                batteryPercent = $val
                isCharging = $charging
                status = 'OK from Windows property: ' + $p.KeyName
                reader = 'Passive Windows battery property'
                rawId = $d.InstanceId
                property = $p.KeyName
            }
            break
        }
    }
}

$items | ConvertTo-Json -Compress -Depth 4
""";

        try
        {
            var stdout = await PowerShellScriptRunner.RunAsync(ps, TimeSpan.FromSeconds(6), cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(stdout) || !stdout.TrimStart().StartsWith("[", StringComparison.Ordinal) && !stdout.TrimStart().StartsWith("{", StringComparison.Ordinal))
                return Array.Empty<DeviceBatteryInfo>();

            return Parse(stdout);
        }
        catch
        {
            return Array.Empty<DeviceBatteryInfo>();
        }
    }

    private static IReadOnlyList<DeviceBatteryInfo> Parse(string json)
    {
        var list = new List<DeviceBatteryInfo>();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (root.ValueKind == JsonValueKind.Object) list.Add(ToInfo(root));
        else if (root.ValueKind == JsonValueKind.Array)
            foreach (var item in root.EnumerateArray()) if (item.ValueKind == JsonValueKind.Object) list.Add(ToInfo(item));
        return list;
    }

    private static DeviceBatteryInfo ToInfo(JsonElement item)
    {
        var name = GetString(item, "name") ?? "Wireless battery device";
        int? percent = null;
        if (item.TryGetProperty("batteryPercent", out var bp) && bp.TryGetInt32(out var i)) percent = Math.Clamp(i, 0, 100);
        bool? charging = null;
        if (item.TryGetProperty("isCharging", out var ch) && ch.ValueKind is JsonValueKind.True or JsonValueKind.False) charging = ch.GetBoolean();
        var rawId = GetString(item, "rawId");
        return new DeviceBatteryInfo
        {
            Name = PreferKnownName(name, rawId),
            DeviceType = GuessType(name, GetString(item, "deviceType"), rawId),
            BatteryPercent = percent,
            IsCharging = charging,
            Status = GetString(item, "status") ?? "OK",
            Reader = GetString(item, "reader") ?? "Passive Windows battery property",
            RawId = rawId,
            LastUpdated = DateTime.Now
        };
    }

    private static string? GetString(JsonElement item, string name)
        => item.TryGetProperty(name, out var v) && v.ValueKind != JsonValueKind.Null ? v.ToString() : null;

    private static string PreferKnownName(string friendlyName, string? rawId)
    {
        var text = $"{friendlyName} {rawId}";
        if (text.Contains("VID_1532&PID_00A6", StringComparison.OrdinalIgnoreCase) || text.Contains("VID_1532&PID_00A5", StringComparison.OrdinalIgnoreCase) || text.Contains("Viper", StringComparison.OrdinalIgnoreCase)) return "Razer Viper V2 Pro";
        if (text.Contains("VID_046D&PID_0AB5", StringComparison.OrdinalIgnoreCase) || text.Contains("G733", StringComparison.OrdinalIgnoreCase)) return "Logitech G733";
        if (text.Contains("VID_36B0&PID_3002", StringComparison.OrdinalIgnoreCase) || text.Contains("QwertyKey", StringComparison.OrdinalIgnoreCase)) return "QwertyKey Keyboard";
        return friendlyName;
    }

    private static string GuessType(string friendlyName, string? cls, string? rawId)
    {
        var text = $"{friendlyName} {cls} {rawId}".ToLowerInvariant();
        if (text.Contains("mouse") || text.Contains("viper")) return "Mouse";
        if (text.Contains("keyboard") || text.Contains("qwertykey")) return "Keyboard";
        if (text.Contains("headset") || text.Contains("headphone") || text.Contains("g733") || text.Contains("audio")) return "Headset";
        if (text.Contains("controller") || text.Contains("xbox") || text.Contains("dualsense") || text.Contains("8bitdo")) return "Controller";
        return "Device";
    }

    private static string EscapeRegex(string value)
        => System.Text.RegularExpressions.Regex.Escape(value).Replace("'", "''");
}
