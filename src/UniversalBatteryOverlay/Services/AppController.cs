using System.Windows;
using FormsContextMenuStrip = System.Windows.Forms.ContextMenuStrip;
using FormsMouseButtons = System.Windows.Forms.MouseButtons;
using FormsMouseEventArgs = System.Windows.Forms.MouseEventArgs;
using FormsNotifyIcon = System.Windows.Forms.NotifyIcon;
using FormsToolStripSeparator = System.Windows.Forms.ToolStripSeparator;
using FormsToolTipIcon = System.Windows.Forms.ToolTipIcon;
using UniversalBatteryOverlay.Models;

namespace UniversalBatteryOverlay.Services;

public sealed class AppController : IDisposable
{
    private readonly SettingsService _settingsService;
    private readonly AppSettings _settings;
    private readonly BatteryMonitorService _monitor;
    private readonly OverlayWindow _overlay;
    private readonly MainWindow _mainWindow;
    private FormsNotifyIcon? _tray;
    private readonly HashSet<string> _alreadyNotified = new(StringComparer.OrdinalIgnoreCase);
    private bool _trayTipShown;

    public static AppController? Instance { get; private set; }
    public bool OverlayVisible => _overlay.IsVisible;

    public AppController()
    {
        Instance = this;
        _settingsService = new SettingsService();
        _settings = _settingsService.Load();
        _monitor = new BatteryMonitorService(_settings);
        _overlay = new OverlayWindow();
        _mainWindow = new MainWindow(_settings, _settingsService, _monitor, ApplyOverlaySettings);
        _monitor.DevicesUpdated += Monitor_DevicesUpdated;
    }

    public void Start()
    {
        CreateTrayIcon();
        _overlay.ApplySettings(_settings);
        _overlay.Show();
        _monitor.Start();
        _mainWindow.Show();
    }

    public void ToggleOverlay()
    {
        if (_overlay.IsVisible) _overlay.Hide();
        else
        {
            _overlay.ApplySettings(_settings);
            _overlay.Show();
        }
    }

    public void ApplyOverlaySettings()
    {
        _overlay.ApplySettings(_settings);
    }

    public void ShowSettings()
    {
        if (!_mainWindow.IsVisible) _mainWindow.Show();
        _mainWindow.WindowState = WindowState.Normal;
        _mainWindow.ShowInTaskbar = true;
        _mainWindow.Activate();
    }

    public void HideSettingsToTray()
    {
        _mainWindow.Hide();
        if (!_trayTipShown)
        {
            _trayTipShown = true;
            _tray?.ShowBalloonTip(
                2500,
                "Universal Battery Overlay",
                "Still running in the tray. Double-click the icon to reopen settings.",
                FormsToolTipIcon.Info);
        }
    }

    public void Exit()
    {
        Dispose();
        System.Windows.Application.Current.Shutdown();
    }

    private void Monitor_DevicesUpdated(object? sender, IReadOnlyList<DeviceBatteryInfo> devices)
    {
        _overlay.UpdateDevices(devices);
        UpdateTrayText(devices);

        foreach (var device in devices)
        {
            if (!device.BatteryPercent.HasValue) continue;
            var key = device.Name;
            if (device.BatteryPercent.Value <= _settings.LowBatteryThreshold)
            {
                if (_alreadyNotified.Add(key))
                {
                    _tray?.ShowBalloonTip(5000, "Battery low", $"{device.TypeDisplay}: {device.BatteryPercent}%", FormsToolTipIcon.Warning);
                }
            }
            else if (device.BatteryPercent.Value > _settings.LowBatteryThreshold + 5)
            {
                _alreadyNotified.Remove(key);
            }
        }
    }

    private static System.Drawing.Icon LoadTrayIcon()
    {
        try
        {
            var iconPath = Path.Combine(AppContext.BaseDirectory, "app.ico");
            if (File.Exists(iconPath)) return new System.Drawing.Icon(iconPath);
        }
        catch { }

        return System.Drawing.SystemIcons.Information;
    }

    private void CreateTrayIcon()
    {
        _tray = new FormsNotifyIcon
        {
            Icon = LoadTrayIcon(),
            Text = "Universal Battery Overlay - realtime",
            Visible = true
        };

        var menu = new FormsContextMenuStrip();
        menu.Items.Add("Open settings", null, (_, _) => ShowSettings());
        menu.Items.Add("Refresh now", null, async (_, _) => await _monitor.RefreshAsync());
        menu.Items.Add("Show / hide overlay", null, (_, _) => ToggleOverlay());
        menu.Items.Add("Open custom readers folder", null, (_, _) =>
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = PathsService.ReadersFolder,
                UseShellExecute = true
            });
        });
        menu.Items.Add(new FormsToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => Exit());
        _tray.ContextMenuStrip = menu;
        _tray.DoubleClick += (_, _) => ShowSettings();
        _tray.MouseClick += Tray_MouseClick;
    }

    private void Tray_MouseClick(object? sender, FormsMouseEventArgs e)
    {
        if (e.Button == FormsMouseButtons.Left)
            ShowSettings();
    }

    private void UpdateTrayText(IReadOnlyList<DeviceBatteryInfo> devices)
    {
        if (_tray is null) return;

        var lines = devices
            .Where(d => d.BatteryPercent.HasValue)
            .OrderBy(d => d.DeviceType)
            .Select(d => $"{d.TypeDisplay} {d.BatteryWithChargingDisplay}")
            .ToList();

        var text = lines.Count == 0
            ? "Universal Battery Overlay - realtime"
            : "Universal Battery Overlay - " + string.Join(" | ", lines);

        _tray.Text = text.Length > 63 ? text[..63] : text;
    }

    public void Dispose()
    {
        _monitor.DevicesUpdated -= Monitor_DevicesUpdated;
        _monitor.Dispose();
        if (_tray is not null)
        {
            _tray.Visible = false;
            _tray.Dispose();
            _tray = null;
        }
        try { _overlay.Close(); } catch { }
        try { _mainWindow.AllowClose(); _mainWindow.Close(); } catch { }
        if (Instance == this) Instance = null;
    }
}
