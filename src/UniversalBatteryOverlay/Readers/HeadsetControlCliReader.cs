using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using UniversalBatteryOverlay.Models;

namespace UniversalBatteryOverlay.Readers;

/// <summary>
/// Optional on-demand reader for Logitech G733 via HeadsetControl CLI.
/// It does NOT require G HUB/Synapse and it is not a background service; the app only starts the CLI for a short battery query.
/// Put headsetcontrol.exe in tools\headsetcontrol\headsetcontrol.exe or install it in PATH.
/// </summary>
public sealed class HeadsetControlCliReader : IBatteryReader
{
    public string Name => "HeadsetControl CLI headset battery";

    public async Task<IReadOnlyList<DeviceBatteryInfo>> ReadAsync(CancellationToken cancellationToken)
    {
        var exe = FindExecutable();
        if (exe is null)
            return Array.Empty<DeviceBatteryInfo>();

        var attempts = new[]
        {
            "-b -o json",
            "-b --output json",
            "-o json -b"
        };

        var last = string.Empty;
        foreach (var args in attempts)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var output = await RunAsync(exe, args, cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(output)) continue;
            last = output.Trim();

            var parsed = TryParseJson(output, exe) ?? TryParseText(output, exe);
            if (parsed is not null)
                return new[] { parsed };
        }

        if (!string.IsNullOrWhiteSpace(last) && last.Contains("g733", StringComparison.OrdinalIgnoreCase))
        {
            return new[]
            {
                new DeviceBatteryInfo
                {
                    Name = "Logitech G733",
                    DeviceType = "Headset",
                    BatteryPercent = null,
                    IsCharging = null,
                    Status = "HeadsetControl found G733, but did not return battery: " + Truncate(last, 180),
                    Reader = Name,
                    RawId = exe,
                    LastUpdated = DateTime.Now
                }
            };
        }

        return Array.Empty<DeviceBatteryInfo>();
    }

    private static string? FindExecutable()
    {
        var baseDir = AppContext.BaseDirectory;
        var currentDir = Directory.GetCurrentDirectory();
        var candidates = new List<string>
        {
            Path.Combine(baseDir, "tools", "headsetcontrol", "headsetcontrol.exe"),
            Path.Combine(baseDir, "tools", "HeadsetControl", "headsetcontrol.exe"),
            Path.Combine(currentDir, "tools", "headsetcontrol", "headsetcontrol.exe"),
            Path.Combine(currentDir, "tools", "HeadsetControl", "headsetcontrol.exe")
        };

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate)) return candidate;
        }

        var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (var dir in path.Split(Path.PathSeparator).Where(x => !string.IsNullOrWhiteSpace(x)))
        {
            try
            {
                var candidate = Path.Combine(dir.Trim(), "headsetcontrol.exe");
                if (File.Exists(candidate)) return candidate;
            }
            catch { }
        }

        return null;
    }

    private static async Task<string> RunAsync(string exe, string args, CancellationToken cancellationToken)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = exe,
                    Arguments = args,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WorkingDirectory = Path.GetDirectoryName(exe) ?? AppContext.BaseDirectory
                },
                EnableRaisingEvents = true
            };

            process.Start();
            var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(5));

            try
            {
                await process.WaitForExitAsync(timeoutCts.Token).ConfigureAwait(false);
            }
            catch
            {
                try { if (!process.HasExited) process.Kill(true); } catch { }
            }

            var stdout = await stdoutTask.ConfigureAwait(false);
            var stderr = await stderrTask.ConfigureAwait(false);
            return string.IsNullOrWhiteSpace(stdout) ? stderr : stdout + Environment.NewLine + stderr;
        }
        catch (Exception ex)
        {
            return ex.GetType().Name + ": " + ex.Message;
        }
    }

    private DeviceBatteryInfo? TryParseJson(string output, string exe)
    {
        var start = output.IndexOf('{');
        var end = output.LastIndexOf('}');
        if (start < 0 || end <= start) return null;

        try
        {
            using var doc = JsonDocument.Parse(output.Substring(start, end - start + 1));
            if (!doc.RootElement.TryGetProperty("devices", out var devices) || devices.ValueKind != JsonValueKind.Array)
                return null;

            foreach (var device in devices.EnumerateArray())
            {
                var name = GetString(device, "device") ?? GetString(device, "name") ?? "Logitech G733";
                var vendor = GetString(device, "id_vendor") ?? string.Empty;
                var product = GetString(device, "id_product") ?? string.Empty;
                var isLikelyG733 = name.Contains("G733", StringComparison.OrdinalIgnoreCase)
                                  || name.Contains("Logitech", StringComparison.OrdinalIgnoreCase)
                                  || vendor.Contains("046d", StringComparison.OrdinalIgnoreCase)
                                  || product.Contains("0ab5", StringComparison.OrdinalIgnoreCase);

                if (!isLikelyG733) continue;
                if (!device.TryGetProperty("battery", out var battery) || battery.ValueKind != JsonValueKind.Object)
                    continue;

                var status = GetString(battery, "status") ?? string.Empty;
                int? level = GetInt(battery, "level") ?? GetInt(battery, "level_percent") ?? GetInt(battery, "percentage");
                if (level.HasValue && level.Value < 0) level = null;
                if (level.HasValue) level = Math.Clamp(level.Value, 0, 100);

                var charging = status.Contains("CHARG", StringComparison.OrdinalIgnoreCase)
                    ? true
                    : status.Contains("AVAILABLE", StringComparison.OrdinalIgnoreCase)
                        ? false
                        : (bool?)null;

                if (level.HasValue || charging == true)
                {
                    return new DeviceBatteryInfo
                    {
                        Name = "Logitech G733",
                        DeviceType = "Headset",
                        BatteryPercent = level,
                        IsCharging = charging,
                        Status = $"OK · HeadsetControl · {status}".Trim(),
                        Reader = Name,
                        RawId = exe,
                        LastUpdated = DateTime.Now
                    };
                }
            }
        }
        catch
        {
            return null;
        }

        return null;
    }

    private DeviceBatteryInfo? TryParseText(string output, string exe)
    {
        if (!output.Contains("g733", StringComparison.OrdinalIgnoreCase)
            && !output.Contains("logitech", StringComparison.OrdinalIgnoreCase)
            && !output.Contains("battery", StringComparison.OrdinalIgnoreCase))
            return null;

        int? percent = null;
        var match = Regex.Match(output, @"(?i)(battery|level|charge)[^0-9]{0,20}(\d{1,3})\s*%?");
        if (match.Success && int.TryParse(match.Groups[2].Value, out var value))
            percent = Math.Clamp(value, 0, 100);

        var charging = output.Contains("charging", StringComparison.OrdinalIgnoreCase)
            ? true
            : output.Contains("discharging", StringComparison.OrdinalIgnoreCase) || output.Contains("available", StringComparison.OrdinalIgnoreCase)
                ? false
                : (bool?)null;

        if (!percent.HasValue && charging != true) return null;

        return new DeviceBatteryInfo
        {
            Name = "Logitech G733",
            DeviceType = "Headset",
            BatteryPercent = percent,
            IsCharging = charging,
            Status = "OK · HeadsetControl text · " + Truncate(output, 140),
            Reader = Name,
            RawId = exe,
            LastUpdated = DateTime.Now
        };
    }

    private static string? GetString(JsonElement element, string name)
        => element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static int? GetInt(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value)) return null;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var i)) return i;
        if (value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out var s)) return s;
        return null;
    }

    private static string Truncate(string text, int max)
    {
        text = text.Replace("\r", " ").Replace("\n", " ").Trim();
        return text.Length <= max ? text : text.Substring(0, max) + "...";
    }
}
