using System.Diagnostics;
using System.Text.Json;
using UniversalBatteryOverlay.Models;

namespace UniversalBatteryOverlay.Readers;

public sealed class KnownVidPidPresenceReader : IBatteryReader
{
    public string Name => "Known USB/HID device matcher";

    private static readonly KnownDevice[] KnownDevices =
    {
        // Exact devices from the user's PC/debug log
        new("VID_1532&PID_00A6", "Razer Viper V2 Pro", "Mouse"),
        new("VID_046D&PID_0AB5", "Logitech G733", "Headset"),
        new("VID_36B0&PID_3002", "QwertyKey Keyboard", "Keyboard"),

        // Broad brand-level matchers. These are presence/classification helpers;
        // real battery still comes from Windows battery APIs, standard HID, direct readers,
        // HeadsetControl, or custom plugins.
        new("VID_046D", "Logitech wireless device", "Device"),
        new("VID_1532", "Razer wireless device", "Device"),
        new("VID_1038", "SteelSeries device", "Device"),
        new("VID_1B1C", "Corsair device", "Device"),
        new("VID_0951", "HyperX / Kingston device", "Device"),
        new("VID_0B05", "ASUS / ROG device", "Device"),
        new("VID_0DB0", "MSI device", "Device"),
        new("VID_2516", "Cooler Master device", "Device"),
        new("VID_258A", "Wireless keyboard/mouse device", "Device"),
        new("VID_3434", "Keychron / QMK device", "Keyboard"),
        new("VID_3297", "QMK keyboard device", "Keyboard"),
        new("VID_054C", "Sony controller/headset", "Device"),
        new("VID_045E", "Microsoft/Xbox device", "Device"),
        new("VID_057E", "Nintendo device", "Device"),
        new("VID_2DC8", "8BitDo controller", "Device")
    };

    public async Task<IReadOnlyList<DeviceBatteryInfo>> ReadAsync(CancellationToken cancellationToken)
    {
        try
        {
            const string script = "Get-PnpDevice -PresentOnly -ErrorAction SilentlyContinue | Select-Object FriendlyName,Class,InstanceId,Status | ConvertTo-Json -Depth 3 -Compress";
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

            var pnpDevices = ParseDevices(stdout);
            var results = new List<DeviceBatteryInfo>();

            foreach (var known in KnownDevices)
            {
                var matches = pnpDevices
                    .Where(d => !string.IsNullOrWhiteSpace(d.InstanceId) && d.InstanceId.Contains(known.IdNeedle, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(d => DeviceRank(d, known))
                    .ToList();

                var best = matches.FirstOrDefault();
                if (best is null) continue;

                var displayName = ImproveDisplayName(known, best);
                results.Add(new DeviceBatteryInfo
                {
                    Name = displayName,
                    DeviceType = ImproveDeviceType(known, best, displayName),
                    BatteryPercent = null,
                    IsCharging = null,
                    Status = "Detected safely. Battery not exposed by Windows.",
                    Reader = Name,
                    RawId = best.InstanceId,
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

    private static string ImproveDisplayName(KnownDevice known, PnpDevice device)
    {
        var friendly = device.FriendlyName ?? string.Empty;
        var text = $"{friendly} {device.InstanceId}";
        if (known.IdNeedle.Contains("PID_", StringComparison.OrdinalIgnoreCase)) return known.DisplayName;
        if (!string.IsNullOrWhiteSpace(friendly) && !friendly.Equals("USB Input Device", StringComparison.OrdinalIgnoreCase) && !friendly.StartsWith("HID-compliant", StringComparison.OrdinalIgnoreCase))
            return friendly;
        return known.DisplayName;
    }

    private static string ImproveDeviceType(KnownDevice known, PnpDevice device, string name)
    {
        if (!known.DeviceType.Equals("Device", StringComparison.OrdinalIgnoreCase)) return known.DeviceType;
        var text = $"{name} {device.Class} {device.FriendlyName} {device.InstanceId}".ToLowerInvariant();
        if (text.Contains("mouse")) return "Mouse";
        if (text.Contains("keyboard") || text.Contains("kbd")) return "Keyboard";
        if (text.Contains("headset") || text.Contains("headphone") || text.Contains("audio") || text.Contains("speakers")) return "Headset";
        return "Device";
    }

    private static int DeviceRank(PnpDevice device, KnownDevice known)
    {
        var name = device.FriendlyName ?? string.Empty;
        var cls = device.Class ?? string.Empty;
        var text = $"{name} {cls}".ToLowerInvariant();

        if (known.DeviceType == "Mouse" && text.Contains("viper")) return 0;
        if (known.DeviceType == "Headset" && text.Contains("g733") && text.Contains("media")) return 0;
        if (known.DeviceType == "Keyboard" && cls.Equals("Keyboard", StringComparison.OrdinalIgnoreCase)) return 0;
        if (text.Contains("vendor-defined")) return 1;
        if (text.Contains("usb input device")) return 2;
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
        Status = GetString(item, "Status")
    };

    private static string? GetString(JsonElement item, string name)
    {
        if (!item.TryGetProperty(name, out var v) || v.ValueKind == JsonValueKind.Null) return null;
        return v.ToString();
    }

    private sealed record KnownDevice(string IdNeedle, string DisplayName, string DeviceType);

    private sealed class PnpDevice
    {
        public string? FriendlyName { get; set; }
        public string? Class { get; set; }
        public string? InstanceId { get; set; }
        public string? Status { get; set; }
    }
}
