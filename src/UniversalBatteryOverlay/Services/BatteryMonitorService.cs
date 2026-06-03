using System.Collections.ObjectModel;
using System.Windows.Threading;
using UniversalBatteryOverlay.Models;
using UniversalBatteryOverlay.Readers;

namespace UniversalBatteryOverlay.Services;

public sealed class BatteryMonitorService : IDisposable
{
    private readonly AppSettings _settings;
    private readonly IReadOnlyList<IBatteryReader> _readers;
    private readonly DeviceProfileCatalog _profileCatalog;
    private readonly DispatcherTimer _timer;
    private int _refreshInProgress;
    private readonly BatteryValueStabilizer _stabilizer = new();
    private readonly Dictionary<string, ReaderCacheEntry> _readerCache = new(StringComparer.OrdinalIgnoreCase);

    public ObservableCollection<DeviceBatteryInfo> Devices { get; } = new();
    public event EventHandler<IReadOnlyList<DeviceBatteryInfo>>? DevicesUpdated;

    public BatteryMonitorService(AppSettings settings)
    {
        _settings = settings;

        // Absolute safety rule:
        // This build never runs a generic HID probe and never sends commands to unknown HID devices.
        // Active commands are allowed only inside exact, isolated readers for known hardware IDs:
        // - Razer Viper V2 Pro: VID_1532&PID_00A6 / VID_1532&PID_00A5
        // - Logitech G733: VID_046D&PID_0AB5
        // QwertyKey is passive-only. It is detected, but never queried actively.
        _settings.EnableUniversalHidBatteryReader = false;
        _profileCatalog = new DeviceProfileCatalog();
        _settings.KnownDeviceHints = _settings.KnownDeviceHints
            .Concat(_profileCatalog.PassiveBatteryHints)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var readers = new List<IBatteryReader>
        {
            new SystemBatteryReader(),
            new KnownVidPidPresenceReader(() => _profileCatalog.Profiles),
            new RegistryKnownDeviceReader(() => _profileCatalog.Profiles),
            new PnpBatteryPropertyReader(() => _settings.KnownDeviceHints)
        };

        if (_settings.EnableRazerDirectBatteryReader)
            readers.Add(new RazerViperV2ProHidReader());

        // Optional on-demand helper. It is not a background service and it is only used when
        // headsetcontrol.exe exists in PATH or tools\headsetcontrol. It targets headsets only.
        if (_settings.EnableHeadsetControlCliReader)
            readers.Add(new HeadsetControlCliReader());

        if (_settings.EnableLogitechG733DirectBatteryReader)
            readers.Add(new LogitechG733DirectFrameReader());

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
        if (Interlocked.Exchange(ref _refreshInProgress, 1) == 1)
            return;

        try
        {
            var all = new List<DeviceBatteryInfo>();

            foreach (var reader in _readers)
            {
                var readerKey = reader.GetType().FullName ?? reader.Name;
                var minInterval = GetReaderMinInterval(reader);

                if (minInterval > TimeSpan.Zero
                    && _readerCache.TryGetValue(readerKey, out var cached)
                    && DateTime.UtcNow - cached.LastRunUtc < minInterval)
                {
                    all.AddRange(cached.Items);
                    continue;
                }

                try
                {
                    using var readerCts = new CancellationTokenSource(TimeSpan.FromSeconds(GetReaderTimeoutSeconds(reader)));
                    var items = await reader.ReadAsync(readerCts.Token).ConfigureAwait(true);
                    var safeItems = items.ToList();
                    all.AddRange(safeItems);
                    _readerCache[readerKey] = new ReaderCacheEntry(DateTime.UtcNow, safeItems);
                }
                catch (OperationCanceledException)
                {
                    StartupLogger.Error($"Reader timed out safely: {reader.Name}");
                }
                catch (Exception ex)
                {
                    StartupLogger.Error($"Reader failed: {reader.Name}", ex);
                }
            }

            var merged = all
                .Where(d => _settings.ShowLaptopBattery || !d.DeviceType.Equals("Laptop", StringComparison.OrdinalIgnoreCase))
                .Where(IsAllowedDevice)
                .Where(d => d.BatteryPercent.HasValue || d.IsCharging == true || IsExactKnownTarget(d) || (_settings.ShowUnknownDevices && LooksWirelessPeripheral(d)))
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

    private static int GetReaderTimeoutSeconds(IBatteryReader reader)
    {
        var name = reader.Name.ToLowerInvariant();
        if (name.Contains("razer")) return 3;
        if (name.Contains("headsetcontrol")) return 5;
        if (name.Contains("g733") || name.Contains("logitech")) return 4;
        if (name.Contains("pnp") || name.Contains("presence") || name.Contains("known usb") || name.Contains("registry")) return 10;
        return 2;
    }

    private static TimeSpan GetReaderMinInterval(IBatteryReader reader)
    {
        var name = reader.Name.ToLowerInvariant();

        // The UI refresh can run every second, but slow inventory readers are cached.
        // This keeps the overlay realtime for actual battery values while avoiding lag
        // from repeated Windows PnP/CIM scans.
        if (name.Contains("razer")) return TimeSpan.FromSeconds(2);
        if (name.Contains("g733") || name.Contains("logitech")) return TimeSpan.FromSeconds(2);
        if (name.Contains("headsetcontrol")) return TimeSpan.FromSeconds(10);
        if (name.Contains("system")) return TimeSpan.FromSeconds(5);
        if (name.Contains("pnp") || name.Contains("presence") || name.Contains("known usb") || name.Contains("registry") || name.Contains("profile detector")) return TimeSpan.FromSeconds(12);
        return TimeSpan.Zero;
    }

    private bool IsAllowedDevice(DeviceBatteryInfo d)
    {
        if (d.DeviceType.Equals("Laptop", StringComparison.OrdinalIgnoreCase)) return true;
        if (IsExactKnownTarget(d)) return true;

        // Only show broader devices when they expose an actual battery/charging state and look wireless.
        // This prevents random USB hubs, RGB controllers, monitors, storage and wired devices from appearing.
        if (d.BatteryPercent.HasValue || d.IsCharging == true)
            return LooksWirelessPeripheral(d);

        return false;
    }

    private bool IsExactKnownTarget(DeviceBatteryInfo d)
    {
        var text = CombinedText(d);
        return _profileCatalog.Profiles.Any(profile => profile.MatchTokens.Any(token =>
            !string.IsNullOrWhiteSpace(token) && text.Contains(token, StringComparison.OrdinalIgnoreCase)));
    }

    private static bool LooksWirelessPeripheral(DeviceBatteryInfo d)
    {
        var text = CombinedText(d);

        if (text.Contains("bth") || text.Contains("bluetooth") || text.Contains("ble") ||
            text.Contains("wireless") || text.Contains("receiver") || text.Contains("dongle") ||
            text.Contains("lightspeed"))
            return true;

        if (text.Contains("headset") || text.Contains("headphone") || text.Contains("earbud") ||
            text.Contains("mouse") || text.Contains("keyboard") || text.Contains("controller") ||
            text.Contains("gamepad"))
            return true;

        if (text.Contains("razer") || text.Contains("logitech") || text.Contains("steelseries") ||
            text.Contains("hyperx") || text.Contains("corsair") || text.Contains("asus rog") ||
            text.Contains("turtle beach") || text.Contains("sennheiser") || text.Contains("bose"))
            return true;

        if (text.Contains("xbox") || text.Contains("dualsense") || text.Contains("dualshock") ||
            text.Contains("switch pro") || text.Contains("nintendo") || text.Contains("8bitdo") ||
            text.Contains("sony"))
            return true;

        if (text.Contains("airpods") || text.Contains("buds") || text.Contains("earphone"))
            return true;

        return false;
    }

    private static string CombinedText(DeviceBatteryInfo d)
        => $"{d.Name} {d.DeviceType} {d.Status} {d.Reader} {d.RawId}".ToLowerInvariant();

    private static string NormalizeKey(DeviceBatteryInfo d)
    {
        var combined = CombinedText(d);

        if (combined.Contains("vid_046d&pid_0ab5") || combined.Contains("g733")) return "logitech-g733";
        if (combined.Contains("vid_1532&pid_00a6") || combined.Contains("vid_1532&pid_00a5") || combined.Contains("viper v2 pro")) return "razer-viper-v2-pro";
        if (combined.Contains("vid_36b0&pid_3002") || combined.Contains("qwertykey") || combined.Contains("qwerty key")) return "qwertykey-keyboard";
        if (combined.Contains("superlight")) return CompactKey("logitech-superlight", d.Name, d.RawId ?? string.Empty);
        if (combined.Contains("mx master")) return CompactKey("logitech-mx-master", d.Name, d.RawId ?? string.Empty);
        if (combined.Contains("mx keys")) return CompactKey("logitech-mx-keys", d.Name, d.RawId ?? string.Empty);
        if (combined.Contains("g305") || combined.Contains("g304")) return CompactKey("logitech-g305", d.Name, d.RawId ?? string.Empty);
        if (combined.Contains("g502")) return CompactKey("logitech-g502", d.Name, d.RawId ?? string.Empty);
        if (combined.Contains("razer") || combined.Contains("deathadder") || combined.Contains("basilisk") || combined.Contains("cobra pro") || combined.Contains("naga v2")) return CompactKey("razer", d.Name, d.RawId ?? string.Empty);
        if (combined.Contains("steelseries") || combined.Contains("arctis") || combined.Contains("aerox")) return CompactKey("steelseries", d.Name, d.RawId ?? string.Empty);
        if (combined.Contains("hyperx") || combined.Contains("cloud flight") || combined.Contains("cloud alpha")) return CompactKey("hyperx", d.Name, d.RawId ?? string.Empty);
        if (combined.Contains("corsair") || combined.Contains("virtuoso") || combined.Contains("hs80")) return CompactKey("corsair", d.Name, d.RawId ?? string.Empty);
        if (combined.Contains("xbox")) return CompactKey("xbox", d.Name, d.RawId ?? string.Empty);
        if (combined.Contains("dualsense") || combined.Contains("dualshock") || combined.Contains("sony")) return CompactKey("sony-controller", d.Name, d.RawId ?? string.Empty);
        if (combined.Contains("8bitdo")) return CompactKey("8bitdo", d.Name, d.RawId ?? string.Empty);
        if (combined.Contains("switch pro") || combined.Contains("nintendo")) return CompactKey("nintendo", d.Name, d.RawId ?? string.Empty);
        if (combined.Contains("laptop battery")) return "system-laptop";
        return CompactKey("device", d.Name.ToLowerInvariant(), d.RawId ?? string.Empty);
    }

    private static string CompactKey(string prefix, string name, string raw)
    {
        var id = ExtractVidPid(raw);
        if (!string.IsNullOrWhiteSpace(id)) return $"{prefix}-{id}";
        var safeName = new string(name.Where(ch => char.IsLetterOrDigit(ch) || ch == '-' || ch == '_').Take(40).ToArray());
        return $"{prefix}-{safeName}";
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
            .ThenByDescending(x => IsSuccessfulDirectReader(x))
            .ThenByDescending(x => IsDirectReader(x))
            .ThenByDescending(x => !string.IsNullOrWhiteSpace(x.Status) && x.Status.StartsWith("OK", StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(x => !x.Name.StartsWith("HID", StringComparison.OrdinalIgnoreCase) && !x.Name.Equals("USB Input Device", StringComparison.OrdinalIgnoreCase))
            .First();
    }

    private static bool IsSuccessfulDirectReader(DeviceBatteryInfo d)
        => IsDirectReader(d) && d.BatteryPercent.HasValue && !d.Status.Contains("failed", StringComparison.OrdinalIgnoreCase);

    private static bool IsDirectReader(DeviceBatteryInfo d)
    {
        var text = CombinedText(d);
        return text.Contains("direct") || text.Contains("headsetcontrol") || text.Contains("zero-access hid");
    }

    private static int SortType(string deviceType) => deviceType.ToLowerInvariant() switch
    {
        "laptop" => 0,
        "mouse" => 1,
        "headset" => 2,
        "headphones" => 2,
        "keyboard" => 3,
        "controller" => 4,
        _ => 9
    };

    public void Dispose()
    {
        _timer.Stop();
    }


    private sealed record ReaderCacheEntry(DateTime LastRunUtc, IReadOnlyList<DeviceBatteryInfo> Items);
}
