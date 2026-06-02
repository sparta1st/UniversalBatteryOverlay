using System.Diagnostics;
using System.Text.Json;
using UniversalBatteryOverlay.Models;

namespace UniversalBatteryOverlay.Readers;

public sealed class PnpPresenceReader : IBatteryReader
{
    private readonly Func<IReadOnlyList<string>> _getHints;
    public string Name => "Windows PnP presence";

    public PnpPresenceReader(Func<IReadOnlyList<string>> getHints)
    {
        _getHints = getHints;
    }

    public async Task<IReadOnlyList<DeviceBatteryInfo>> ReadAsync(CancellationToken cancellationToken)
    {
        var hints = _getHints()
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (hints.Length == 0) return Array.Empty<DeviceBatteryInfo>();

        try
        {
            var script = "Get-PnpDevice -PresentOnly -ErrorAction SilentlyContinue | " +
                         "Select-Object FriendlyName,Class,InstanceId,Status | ConvertTo-Json -Depth 3 -Compress";
            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{script}\"",
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

            var devices = ParseDevices(stdout);
            var matched = new List<DeviceBatteryInfo>();

            foreach (var device in devices)
            {
                var name = string.IsNullOrWhiteSpace(device.FriendlyName) ? "Unknown USB/HID Device" : device.FriendlyName!;
                var searchable = $"{name} {device.Class} {device.InstanceId}";
                if (!IsLikelyPeripheral(searchable, device.Class)) continue;
                if (!hints.Any(h => searchable.Contains(h, StringComparison.OrdinalIgnoreCase))) continue;

                matched.Add(new DeviceBatteryInfo
                {
                    Name = CleanName(PreferKnownName(name, device.InstanceId)),
                    DeviceType = GuessType(searchable, device.Class),
                    BatteryPercent = null,
                    IsCharging = null,
                    Status = "Detected safely. Battery not exposed by Windows.",
                    Reader = Name,
                    RawId = device.InstanceId,
                    LastUpdated = DateTime.Now
                });
            }

            return matched
                .GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .ToList();
        }
        catch
        {
            return Array.Empty<DeviceBatteryInfo>();
        }
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
        Status = GetString(item, "Status")
    };

    private static string? GetString(JsonElement item, string name)
    {
        if (!item.TryGetProperty(name, out var v) || v.ValueKind == JsonValueKind.Null) return null;
        return v.ToString();
    }

    private static bool IsLikelyPeripheral(string friendlyName, string? cls)
    {
        var text = $"{friendlyName} {cls}".ToLowerInvariant();
        return text.Contains("mouse") || text.Contains("keyboard") || text.Contains("hid") ||
               text.Contains("headset") || text.Contains("headphone") || text.Contains("audio") ||
               text.Contains("razer") || text.Contains("logitech") || text.Contains("qwertykey") ||
               text.Contains("wireless") || text.Contains("dongle") || text.Contains("receiver");
    }

    private static string GuessType(string friendlyName, string? cls)
    {
        var text = $"{friendlyName} {cls}".ToLowerInvariant();
        if (text.Contains("mouse") || text.Contains("viper")) return "Mouse";
        if (text.Contains("keyboard") || text.Contains("qwertykey")) return "Keyboard";
        if (text.Contains("headset") || text.Contains("headphone") || text.Contains("g733") || text.Contains("audio")) return "Headset";
        return "Device";
    }

    private static string CleanName(string name)
        => name.Replace("HID-compliant", "HID", StringComparison.OrdinalIgnoreCase).Trim();

    private static string PreferKnownName(string friendlyName, string? instanceId)
    {
        var text = $"{friendlyName} {instanceId}";
        if (text.Contains("VID_1532&PID_00A6", StringComparison.OrdinalIgnoreCase)) return "Razer Viper V2 Pro";
        if (text.Contains("VID_046D&PID_0AB5", StringComparison.OrdinalIgnoreCase)) return "Logitech G733";
        if (text.Contains("VID_36B0&PID_3002", StringComparison.OrdinalIgnoreCase)) return "QwertyKey Keyboard";
        return friendlyName;
    }

    private sealed class PnpDevice
    {
        public string? FriendlyName { get; set; }
        public string? Class { get; set; }
        public string? InstanceId { get; set; }
        public string? Status { get; set; }
    }
}
