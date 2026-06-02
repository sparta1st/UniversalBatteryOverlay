using System.Diagnostics;
using System.Text.Json;
using UniversalBatteryOverlay.Models;

namespace UniversalBatteryOverlay.Readers;

public sealed class PnpBatteryPropertyReader : IBatteryReader
{
    private readonly Func<IReadOnlyList<string>> _getHints;
    public string Name => "Windows PnP battery properties";

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

        var regex = hints.Length == 0 ? "" : string.Join("|", hints);

        var ps = $$"""
$ErrorActionPreference = 'SilentlyContinue'
$rx = '{{regex}}'
$items = @()
# v16: scan likely peripheral classes too, but only return devices that expose a real 0-100 battery value.
# This keeps keyboard/headset support broad without sending HID commands or cluttering the overlay.
$devices = Get-PnpDevice -PresentOnly | Where-Object {
  $_.FriendlyName -and (
    ($rx -and ($_.FriendlyName -match $rx -or $_.InstanceId -match $rx)) -or
    ($_.Class -match 'Bluetooth|HIDClass|Keyboard|Mouse|MEDIA|AudioEndpoint') -or
    ($_.InstanceId -match 'BTH|HID|USB')
  )
}
foreach ($d in $devices) {
  $props = Get-PnpDeviceProperty -InstanceId $d.InstanceId | Where-Object {
    $_.KeyName -match 'Battery|Charge|Capacity|Power' -and $_.Data -ne $null
  }
  foreach ($p in $props) {
    $val = $null
    try { $val = [int]$p.Data } catch { $val = $null }
    if ($val -ne $null -and $val -ge 0 -and $val -le 100) {
      $items += [pscustomobject]@{
        name = $d.FriendlyName
        deviceType = $d.Class
        batteryPercent = $val
        status = 'OK'
        reader = 'Windows PnP battery property'
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
            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{EscapeForCommand(ps)}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            using var process = Process.Start(psi);
            if (process is null) return Array.Empty<DeviceBatteryInfo>();
            var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            var stdout = await stdoutTask.ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(stdout)) return Array.Empty<DeviceBatteryInfo>();

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
        var name = GetString(item, "name") ?? "PnP battery device";
        int? percent = null;
        if (item.TryGetProperty("batteryPercent", out var bp) && bp.TryGetInt32(out var i)) percent = Math.Clamp(i, 0, 100);
        return new DeviceBatteryInfo
        {
            Name = name,
            DeviceType = GuessType(name, GetString(item, "deviceType")),
            BatteryPercent = percent,
            Status = GetString(item, "status") ?? "OK",
            Reader = GetString(item, "reader") ?? "Windows PnP battery property",
            RawId = GetString(item, "rawId"),
            LastUpdated = DateTime.Now
        };
    }

    private static string? GetString(JsonElement item, string name)
        => item.TryGetProperty(name, out var v) && v.ValueKind != JsonValueKind.Null ? v.ToString() : null;

    private static string GuessType(string friendlyName, string? cls)
    {
        var text = $"{friendlyName} {cls}".ToLowerInvariant();
        if (text.Contains("mouse") || text.Contains("viper")) return "Mouse";
        if (text.Contains("keyboard") || text.Contains("qwertykey")) return "Keyboard";
        if (text.Contains("headset") || text.Contains("headphone") || text.Contains("g733") || text.Contains("audio")) return "Headset";
        return "Device";
    }

    private static string EscapeRegex(string value)
        => System.Text.RegularExpressions.Regex.Escape(value).Replace("'", "''");

    private static string EscapeForCommand(string script)
        => script.Replace("\"", "\\\"").Replace("\r", " ").Replace("\n", " ");
}
