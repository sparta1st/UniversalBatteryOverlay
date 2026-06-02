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
    private bool _showOverlayBackground = true;
    private bool _showOverlayShadow = true;
    private bool _clickThrough = true;
    private bool _showUnknownDevices = true;
    private bool _showLaptopBattery = true;
    private bool _startWithWindows = false;
    private bool _enableRazerDirectBatteryReader = true;
    private bool _enableUniversalHidBatteryReader = true;
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
    public bool ShowOverlayBackground { get => _showOverlayBackground; set => Set(ref _showOverlayBackground, value); }
    public bool ShowOverlayShadow { get => _showOverlayShadow; set => Set(ref _showOverlayShadow, value); }
    public bool ClickThrough { get => _clickThrough; set => Set(ref _clickThrough, value); }
    public bool ShowUnknownDevices { get => _showUnknownDevices; set => Set(ref _showUnknownDevices, value); }
    public bool ShowLaptopBattery { get => _showLaptopBattery; set => Set(ref _showLaptopBattery, value); }
    public bool StartWithWindows { get => _startWithWindows; set => Set(ref _startWithWindows, value); }
    public bool EnableRazerDirectBatteryReader { get => _enableRazerDirectBatteryReader; set => Set(ref _enableRazerDirectBatteryReader, value); }
    public bool EnableUniversalHidBatteryReader { get => _enableUniversalHidBatteryReader; set => Set(ref _enableUniversalHidBatteryReader, value); }
    public int LowBatteryThreshold { get => _lowBatteryThreshold; set => Set(ref _lowBatteryThreshold, Math.Clamp(value, 1, 99)); }

    public List<string> KnownDeviceHints { get; set; } = new()
    {
        // User devices
        "Razer", "Viper", "Viper V2 Pro", "VID_1532&PID_00A6",
        "Logitech", "Logi", "G733", "Lightspeed", "VID_046D&PID_0AB5",
        "QwertyKey", "QWERTYKEY", "VID_36B0&PID_3002",

        // Big gaming/peripheral brands
        "Corsair", "SteelSeries", "HyperX", "Kingston", "HP", "OMEN",
        "ASUS", "ROG", "TUF", "AURA", "Acer", "Predator", "Lenovo", "Legion",
        "MSI", "Alienware", "Dell", "Cooler Master", "Glorious", "Finalmouse",
        "Pulsar", "Lamzu", "Endgame Gear", "Zowie", "BenQ", "ROCCAT", "Turtle Beach",
        "Keychron", "Akko", "Royal Kludge", "RK", "Epomaker", "Wooting", "Ducky",
        "Anne Pro", "NuPhy", "Redragon", "Razer BlackWidow", "DeathAdder", "Basilisk",
        "Naga", "Orochi", "Cobra", "Barracuda", "BlackShark", "Kraken",
        "G Pro", "PRO X", "G502", "G703", "G903", "G305", "G915", "G915 TKL",
        "G435", "G535", "G733", "G935", "G Pro X Wireless",

        // Controllers / Bluetooth devices
        "Xbox Wireless", "Xbox Controller", "DualSense", "DualShock", "Sony", "Nintendo",
        "Switch Pro", "8BitDo", "Bluetooth", "BLE", "BTH",

        // Common USB vendor IDs for discovery/hints
        "VID_1532", // Razer
        "VID_046D", // Logitech
        "VID_1038", // SteelSeries
        "VID_1B1C", // Corsair
        "VID_0951", // HyperX/Kingston
        "VID_0B05", // ASUS
        "VID_0DB0", // MSI
        "VID_2516", // Cooler Master
        "VID_258A", // SINO WEALTH / many keyboards
        "VID_3434", // Keychron/QMK-like devices
        "VID_3297", // ZSA/QMK-like devices
        "VID_054C", // Sony
        "VID_045E", // Microsoft Xbox
        "VID_057E", // Nintendo
        "VID_2DC8"  // 8BitDo
    };

    public event PropertyChangedEventHandler? PropertyChanged;

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
