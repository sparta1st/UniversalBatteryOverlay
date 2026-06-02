using System.IO;
using System.Diagnostics;
using System.Text.Json;
using UniversalBatteryOverlay.Models;

namespace UniversalBatteryOverlay.Readers;

public sealed class ExternalScriptReader : IBatteryReader
{
    private readonly string _scriptsFolder;
    public string Name => "External PowerShell readers";

    public ExternalScriptReader(string scriptsFolder)
    {
        _scriptsFolder = scriptsFolder;
        Directory.CreateDirectory(_scriptsFolder);
    }

    public async Task<IReadOnlyList<DeviceBatteryInfo>> ReadAsync(CancellationToken cancellationToken)
    {
        var results = new List<DeviceBatteryInfo>();
        if (!Directory.Exists(_scriptsFolder)) return results;

        foreach (var script in Directory.EnumerateFiles(_scriptsFolder, "*.ps1", SearchOption.TopDirectoryOnly))
        {
            if (Path.GetFileName(script).StartsWith("sample", StringComparison.OrdinalIgnoreCase)) continue;

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{script}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };

                using var process = Process.Start(psi);
                if (process is null) continue;

                var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
                var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
                await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

                var stdout = await stdoutTask.ConfigureAwait(false);
                var stderr = await stderrTask.ConfigureAwait(false);

                if (process.ExitCode != 0)
                {
                    results.Add(new DeviceBatteryInfo
                    {
                        Name = Path.GetFileNameWithoutExtension(script),
                        DeviceType = "Device",
                        BatteryPercent = null,
                        Status = $"Reader error: {Trim(stderr, 120)}",
                        Reader = Name,
                        RawId = script,
                        LastUpdated = DateTime.Now
                    });
                    continue;
                }

                var parsed = Parse(stdout, Path.GetFileName(script));
                results.AddRange(parsed);
            }
            catch (Exception ex)
            {
                results.Add(new DeviceBatteryInfo
                {
                    Name = Path.GetFileNameWithoutExtension(script),
                    DeviceType = "Device",
                    BatteryPercent = null,
                    Status = $"Reader exception: {ex.Message}",
                    Reader = Name,
                    RawId = script,
                    LastUpdated = DateTime.Now
                });
            }
        }

        return results;
    }

    private static List<DeviceBatteryInfo> Parse(string stdout, string scriptName)
    {
        var list = new List<DeviceBatteryInfo>();
        if (string.IsNullOrWhiteSpace(stdout)) return list;

        using var doc = JsonDocument.Parse(stdout);
        var root = doc.RootElement;
        if (root.ValueKind == JsonValueKind.Object)
        {
            list.Add(ToInfo(root, scriptName));
        }
        else if (root.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in root.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.Object)
                    list.Add(ToInfo(item, scriptName));
            }
        }

        return list;
    }

    private static DeviceBatteryInfo ToInfo(JsonElement item, string scriptName)
    {
        int? battery = null;
        if (TryGet(item, "batteryPercent", out var bp) && bp.ValueKind == JsonValueKind.Number && bp.TryGetInt32(out var i))
            battery = Math.Clamp(i, 0, 100);
        if (TryGet(item, "battery", out var bp2) && bp2.ValueKind == JsonValueKind.Number && bp2.TryGetInt32(out var i2))
            battery = Math.Clamp(i2, 0, 100);

        bool? charging = null;
        if (TryGet(item, "isCharging", out var ch) && (ch.ValueKind == JsonValueKind.True || ch.ValueKind == JsonValueKind.False))
            charging = ch.GetBoolean();

        return new DeviceBatteryInfo
        {
            Name = GetString(item, "name") ?? Path.GetFileNameWithoutExtension(scriptName),
            DeviceType = GetString(item, "deviceType") ?? GetString(item, "type") ?? "Device",
            BatteryPercent = battery,
            IsCharging = charging,
            Status = GetString(item, "status") ?? (battery.HasValue ? "OK" : "Unknown"),
            Reader = GetString(item, "reader") ?? $"Script: {scriptName}",
            RawId = GetString(item, "rawId") ?? scriptName,
            LastUpdated = DateTime.Now
        };
    }

    private static string? GetString(JsonElement item, string name)
        => TryGet(item, name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static bool TryGet(JsonElement item, string name, out JsonElement value)
    {
        foreach (var property in item.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }
        value = default;
        return false;
    }

    private static string Trim(string value, int max)
        => string.IsNullOrWhiteSpace(value) ? "unknown" : value.Trim().Length <= max ? value.Trim() : value.Trim()[..max] + "...";
}
