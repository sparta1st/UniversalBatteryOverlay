using System.Collections.ObjectModel;
using System.Windows.Threading;
using UniversalBatteryOverlay.Models;
using UniversalBatteryOverlay.Readers;

namespace UniversalBatteryOverlay.Services;

public sealed class BatteryMonitorService : IDisposable
{
    private readonly AppSettings _settings;
    private readonly IReadOnlyList<IBatteryReader> _readers;
    private readonly DispatcherTimer _timer;
    private CancellationTokenSource? _refreshCts;
    private int _refreshInProgress;
    private readonly BatteryValueStabilizer _stabilizer = new();

    public ObservableCollection<DeviceBatteryInfo> Devices { get; } = new();
    public event EventHandler<IReadOnlyList<DeviceBatteryInfo>>? DevicesUpdated;

    public BatteryMonitorService(AppSettings settings)
    {
        _settings = settings;
        // Realtime edition:
        // - targeted active readers only for known devices (Razer mouse / Logitech headset).
        // - QwertyKey stays passive to avoid input interference.
        // - fast polling is protected by a re-entrancy guard, so readers never stack on top of each other.
        var readers = new List<IBatteryReader>
        {
            new SystemBatteryReader()
        };

        // Universal safe readers: these only ask Windows/HID for standard battery data.
        // They do not send vendor commands to unknown devices.
        if (_settings.EnableUniversalHidBatteryReader)
        {
            readers.Add(new StandardHidBatteryReader(() => _settings.KnownDeviceHints));
        }

        // Direct readers: enabled for known devices only. These improve accuracy for models
        // that do not expose a normal Windows battery percentage.
        if (_settings.EnableRazerDirectBatteryReader)
        {
            readers.Add(new RazerViperV2ProHidReader());
            readers.Add(new HeadsetControlCliReader());
            readers.Add(new LogitechG733DirectFrameReader());
        }

        readers.Add(new KnownVidPidPresenceReader());
        readers.Add(new PnpBatteryPropertyReader(() => _settings.KnownDeviceHints));
        readers.Add(new PnpPresenceReader(() => _settings.KnownDeviceHints));
        readers.Add(new ExternalScriptReader(PathsService.ReadersFolder));

        _readers = readers;

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(Math.Max(1, _settings.RefreshSeconds)) };
        _timer.Tick += async (_, _) => await RefreshAsync();
    }

    public void Start()
    {
        _timer.Start();
        _ = RefreshAsync();
    }

    public void Stop() => _timer.Stop();

    public void UpdateInterval()
    {
        _timer.Interval = TimeSpan.FromSeconds(Math.Max(1, _settings.RefreshSeconds));
    }

    public async Task RefreshAsync()
    {
        // Realtime mode can tick every 1-2 seconds. Never start a second HID poll while one is still running.
        if (Interlocked.Exchange(ref _refreshInProgress, 1) == 1)
            return;

        try
        {
            _refreshCts?.Cancel();
            _refreshCts?.Dispose();
            _refreshCts = new CancellationTokenSource(TimeSpan.FromSeconds(Math.Max(4, _settings.RefreshSeconds * 3)));
            var token = _refreshCts.Token;

            var all = new List<DeviceBatteryInfo>();
            foreach (var reader in _readers)
            {
                try
                {
                    var items = await reader.ReadAsync(token).ConfigureAwait(true);
                    all.AddRange(items);
                }
                catch (OperationCanceledException)
                {
                    // Fast realtime polling may cancel slow optional readers. Keep the last successful devices on screen.
                }
                catch
                {
                    // Readers are isolated. One broken reader must not break the overlay.
                }
            }

            var merged = all
                .Where(d => _settings.ShowLaptopBattery || !d.DeviceType.Equals("Laptop", StringComparison.OrdinalIgnoreCase))
                .Where(d => _settings.ShowUnknownDevices || d.BatteryPercent.HasValue || d.IsCharging == true)
                .GroupBy(NormalizeKey, StringComparer.OrdinalIgnoreCase)
                .Select(g =>
                {
                    var picked = PickBest(g.ToList());
                    return _stabilizer.Apply(NormalizeKey(picked), picked);
                })
                .OrderBy(d => SortType(d.DeviceType))
                .ThenBy(d => d.Name)
                .ToList();

            Devices.Clear();
            foreach (var device in merged)
                Devices.Add(device);

            DevicesUpdated?.Invoke(this, merged);
        }
        finally
        {
            Interlocked.Exchange(ref _refreshInProgress, 0);
        }
    }

    private static string NormalizeKey(DeviceBatteryInfo d)
    {
        var name = d.Name.ToLowerInvariant();
        var raw = (d.RawId ?? string.Empty).ToLowerInvariant();
        var combined = name + " " + raw;

        if (combined.Contains("vid_046d&pid_0ab5") || combined.Contains("g733")) return "logitech-g733";
        if (combined.Contains("vid_1532&pid_00a6") || combined.Contains("viper") || combined.Contains("razer")) return "razer-viper-v2-pro";
        if (combined.Contains("vid_36b0&pid_3002") || combined.Contains("qwertykey") || combined.Contains("qwerty key")) return "qwertykey-keyboard";
        if (combined.Contains("vid_046d") && (combined.Contains("mouse") || combined.Contains("keyboard") || combined.Contains("headset") || combined.Contains("logitech"))) return CompactKey("logitech", name, raw);
        if (combined.Contains("vid_1038") || combined.Contains("steelseries")) return CompactKey("steelseries", name, raw);
        if (combined.Contains("vid_1b1c") || combined.Contains("corsair")) return CompactKey("corsair", name, raw);
        if (combined.Contains("vid_0951") || combined.Contains("hyperx") || combined.Contains("kingston")) return CompactKey("hyperx", name, raw);
        if (combined.Contains("vid_0b05") || combined.Contains("asus") || combined.Contains("rog")) return CompactKey("asus", name, raw);
        if (combined.Contains("vid_054c") || combined.Contains("dualsense") || combined.Contains("dualshock") || combined.Contains("sony")) return CompactKey("sony-controller", name, raw);
        if (combined.Contains("vid_045e") || combined.Contains("xbox")) return CompactKey("xbox", name, raw);
        if (combined.Contains("vid_057e") || combined.Contains("nintendo") || combined.Contains("switch pro")) return CompactKey("nintendo", name, raw);
        if (name.Contains("laptop battery")) return "system-laptop";
        return name;
    }

    private static string CompactKey(string prefix, string name, string raw)
    {
        var id = ExtractVidPid(raw);
        if (!string.IsNullOrWhiteSpace(id)) return $"{prefix}-{id}";
        return $"{prefix}-{name}";
    }

    private static string ExtractVidPid(string raw)
    {
        var match = System.Text.RegularExpressions.Regex.Match(raw, "vid_[0-9a-f]{4}.*?pid_[0-9a-f]{4}", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        return match.Success ? match.Value.Replace("&", "-").ToLowerInvariant() : string.Empty;
    }

    private static DeviceBatteryInfo PickBest(List<DeviceBatteryInfo> devices)
    {
        return devices
            .OrderByDescending(x => x.BatteryPercent.HasValue)
            .ThenByDescending(x => x.IsCharging == true)
            .ThenByDescending(x => !string.IsNullOrWhiteSpace(x.Status) && x.Status.StartsWith("OK", StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(x => x.Reader.Contains("native", StringComparison.OrdinalIgnoreCase) || x.Reader.Contains("HID++", StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(x => x.Reader.Contains("direct", StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(x => x.Reader.Contains("Known USB/HID", StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(x => !x.Name.StartsWith("HID", StringComparison.OrdinalIgnoreCase) && !x.Name.Equals("USB Input Device", StringComparison.OrdinalIgnoreCase))
            .First();
    }

    private static int SortType(string deviceType) => deviceType.ToLowerInvariant() switch
    {
        "laptop" => 0,
        "mouse" => 1,
        "keyboard" => 2,
        "headset" => 3,
        _ => 9
    };

    public void Dispose()
    {
        _timer.Stop();
        _refreshCts?.Cancel();
        _refreshCts?.Dispose();
    }
}
