using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace UniversalBatteryOverlay.Models;

public sealed class AppSettings : INotifyPropertyChanged
{
    private string _monitorDeviceName = "Primary";
    private OverlayPosition _position = OverlayPosition.TopLeft;
    private int _offsetX = 10;
    private int _offsetY = 10;
    private int _refreshSeconds = 1;
    private double _overlayOpacity = 0.94;
    private double _overlayBackgroundOpacity = 0.78;
    private double _fontSize = 14;
    private int _overlayPadding = 5;
    private int _overlayRowPaddingX = 7;
    private int _overlayRowPaddingY = 2;
    private int _overlayRowSpacing = 1;
    private int _overlayMinWidth = 112;
    private int _overlayCornerRadius = 13;
    private string _overlayBackgroundColor = "#0B1020";
    private string _overlayRowBackgroundColor = "#111A2E";
    private string _overlayBorderColor = "#334155";
    private string _overlayTextColor = "#F8FAFC";
    private string _overlayValueColor = "#E9D5FF";
    private bool _showOverlayBackground = true;
    private bool _showOverlayShadow = true;
    private bool _clickThrough = true;
    private bool _showUnknownDevices = true;
    private bool _showLaptopBattery = true;
    private bool _startWithWindows = false;
    private bool _enableRazerDirectBatteryReader = true;
    private bool _enableLogitechG733DirectBatteryReader = true;
    private bool _enableUniversalHidBatteryReader = false;
    private bool _enableHeadsetControlCliReader = true;
    private int _lowBatteryThreshold = 20;

    public string MonitorDeviceName { get => _monitorDeviceName; set => Set(ref _monitorDeviceName, value); }
    public OverlayPosition Position { get => _position; set => Set(ref _position, value); }
    public int OffsetX { get => _offsetX; set => Set(ref _offsetX, Math.Clamp(value, 0, 3000)); }
    public int OffsetY { get => _offsetY; set => Set(ref _offsetY, Math.Clamp(value, 0, 3000)); }
    public int RefreshSeconds { get => _refreshSeconds; set => Set(ref _refreshSeconds, Math.Clamp(value, 1, 3600)); }
    public double OverlayOpacity { get => _overlayOpacity; set => Set(ref _overlayOpacity, Math.Clamp(value, 0.25, 1)); }
    public double OverlayBackgroundOpacity { get => _overlayBackgroundOpacity; set => Set(ref _overlayBackgroundOpacity, Math.Clamp(value, 0.0, 1)); }
    public double FontSize { get => _fontSize; set => Set(ref _fontSize, Math.Clamp(value, 10, 28)); }
    public int OverlayPadding { get => _overlayPadding; set => Set(ref _overlayPadding, Math.Clamp(value, 0, 40)); }
    public int OverlayRowPaddingX { get => _overlayRowPaddingX; set => Set(ref _overlayRowPaddingX, Math.Clamp(value, 0, 40)); }
    public int OverlayRowPaddingY { get => _overlayRowPaddingY; set => Set(ref _overlayRowPaddingY, Math.Clamp(value, 0, 30)); }
    public int OverlayRowSpacing { get => _overlayRowSpacing; set => Set(ref _overlayRowSpacing, Math.Clamp(value, 0, 30)); }
    public int OverlayMinWidth { get => _overlayMinWidth; set => Set(ref _overlayMinWidth, Math.Clamp(value, 80, 500)); }
    public int OverlayCornerRadius { get => _overlayCornerRadius; set => Set(ref _overlayCornerRadius, Math.Clamp(value, 0, 40)); }
    public string OverlayBackgroundColor { get => _overlayBackgroundColor; set => Set(ref _overlayBackgroundColor, NormalizeHex(value, "#0B1020")); }
    public string OverlayRowBackgroundColor { get => _overlayRowBackgroundColor; set => Set(ref _overlayRowBackgroundColor, NormalizeHex(value, "#111A2E")); }
    public string OverlayBorderColor { get => _overlayBorderColor; set => Set(ref _overlayBorderColor, NormalizeHex(value, "#334155")); }
    public string OverlayTextColor { get => _overlayTextColor; set => Set(ref _overlayTextColor, NormalizeHex(value, "#F8FAFC")); }
    public string OverlayValueColor { get => _overlayValueColor; set => Set(ref _overlayValueColor, NormalizeHex(value, "#E9D5FF")); }
    public bool ShowOverlayBackground { get => _showOverlayBackground; set => Set(ref _showOverlayBackground, value); }
    public bool ShowOverlayShadow { get => _showOverlayShadow; set => Set(ref _showOverlayShadow, value); }
    public bool ClickThrough { get => _clickThrough; set => Set(ref _clickThrough, value); }
    public bool ShowUnknownDevices { get => _showUnknownDevices; set => Set(ref _showUnknownDevices, value); }
    public bool ShowLaptopBattery { get => _showLaptopBattery; set => Set(ref _showLaptopBattery, value); }
    public bool StartWithWindows { get => _startWithWindows; set => Set(ref _startWithWindows, value); }
    public bool EnableRazerDirectBatteryReader { get => _enableRazerDirectBatteryReader; set => Set(ref _enableRazerDirectBatteryReader, value); }
    public bool EnableLogitechG733DirectBatteryReader { get => _enableLogitechG733DirectBatteryReader; set => Set(ref _enableLogitechG733DirectBatteryReader, value); }
    public bool EnableUniversalHidBatteryReader { get => _enableUniversalHidBatteryReader; set => Set(ref _enableUniversalHidBatteryReader, value); }
    public bool EnableHeadsetControlCliReader { get => _enableHeadsetControlCliReader; set => Set(ref _enableHeadsetControlCliReader, value); }
    public int LowBatteryThreshold { get => _lowBatteryThreshold; set => Set(ref _lowBatteryThreshold, Math.Clamp(value, 1, 99)); }

    public List<string> KnownDeviceHints { get; set; } = new()
    {
        // Keep this list strict. These are passive matching hints only; they do not send HID commands.
        // Broad vendor-only IDs such as VID_0B05 are intentionally not included because they can
        // make the app show unrelated USB devices like RGB controllers and hubs.
        "Razer Viper V2 Pro", "Viper V2 Pro", "VID_1532&PID_00A6", "VID_1532&PID_00A5",
        "Logitech G733", "G733", "Lightspeed", "VID_046D&PID_0AB5",
        "QwertyKey", "QWERTYKEY", "VID_36B0&PID_3002",
        "Xbox Wireless Controller", "DualSense", "DualShock", "8BitDo", "Switch Pro Controller"
    };

    public event PropertyChangedEventHandler? PropertyChanged;

    private static string NormalizeHex(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value)) return fallback;
        var text = value.Trim();
        if (!text.StartsWith("#", StringComparison.Ordinal)) text = "#" + text;
        if (text.Length is not (7 or 9)) return fallback;
        return text.Skip(1).All(Uri.IsHexDigit) ? text.ToUpperInvariant() : fallback;
    }

    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}

public enum OverlayPosition
{
    TopLeft,
    TopRight,
    BottomLeft,
    BottomRight,
    Custom
}
