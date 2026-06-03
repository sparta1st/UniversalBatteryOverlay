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
    private bool _disposed;

    public static AppController? Instance { get; private set; }
    public bool OverlayVisible => _overlay.IsVisible;
    public bool TrayAvailable => _tray is not null;

    public AppController()
    {
        Instance = this;
        StartupLogger.Info("Creating services/windows.");
        _settingsService = new SettingsService();
        _settings = _settingsService.Load();
        _monitor = new BatteryMonitorService(_settings);
        _overlay = new OverlayWindow();
        _mainWindow = new MainWindow(_settings, _settingsService, _monitor, ApplyOverlaySettings);
        _monitor.DevicesUpdated += Monitor_DevicesUpdated;
    }

    public void Start()
    {
        StartupLogger.Info("Starting controller.");

        // The settings window is shown first. If tray creation fails for any reason,
        // the app still remains visible instead of disappearing silently.
        _mainWindow.Show();
        StartupLogger.Info("Settings window shown.");

        try
        {
            CreateTrayIcon();
            StartupLogger.Info("Tray icon created.");
        }
        catch (Exception ex)
        {
            StartupLogger.Error("Tray icon could not be created", ex);
            _mainWindow.ShowTrayWarning(StartupLogger.StartupLogPath);
        }

        try
        {
            _overlay.ApplySettings(_settings);
            _overlay.Show();
            StartupLogger.Info("Overlay shown.");
        }
        catch (Exception ex)
        {
            StartupLogger.Error("Overlay could not be shown", ex);
            _mainWindow.ShowOverlayWarning(StartupLogger.StartupLogPath);
        }

        try
        {
            _monitor.Start();
            StartupLogger.Info("Monitor started.");
        }
        catch (Exception ex)
        {
            StartupLogger.Error("Battery monitor could not be started", ex);
            _mainWindow.ShowMonitorWarning(StartupLogger.StartupLogPath);
        }
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
        try
        {
            _overlay.ApplySettings(_settings);
        }
        catch (Exception ex)
        {
            StartupLogger.Error("Apply overlay settings failed", ex);
        }
    }

    public void ShowSettings()
    {
        try
        {
            if (!_mainWindow.IsVisible) _mainWindow.Show();
            _mainWindow.WindowState = WindowState.Normal;
            _mainWindow.ShowInTaskbar = true;
            _mainWindow.Activate();
        }
        catch (Exception ex)
        {
            StartupLogger.Error("Show settings failed", ex);
        }
    }

    public void HideSettingsToTray()
    {
        if (_tray is null)
        {
            // Never hide the only usable window if the tray is unavailable.
            _mainWindow.WindowState = WindowState.Minimized;
            _mainWindow.ShowInTaskbar = true;
            StartupLogger.Info("Tray unavailable; minimized settings window instead of hiding it.");
            return;
        }

        _mainWindow.Hide();
        if (!_trayTipShown)
        {
            _trayTipShown = true;
            SafeTrayTip(
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
        try { _overlay.UpdateDevices(devices); }
        catch (Exception ex) { StartupLogger.Error("Overlay update failed", ex); }

        UpdateTrayText(devices);

        foreach (var device in devices)
        {
            if (!device.BatteryPercent.HasValue) continue;
            var key = device.Name;
            if (device.BatteryPercent.Value <= _settings.LowBatteryThreshold)
            {
                if (_alreadyNotified.Add(key))
                    SafeTrayTip(5000, "Battery low", $"{device.TypeDisplay}: {device.BatteryPercent}%", FormsToolTipIcon.Warning);
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
        catch (Exception ex)
        {
            StartupLogger.Error("Could not load tray icon", ex);
        }

        return System.Drawing.SystemIcons.Information;
    }

    private void CreateTrayIcon()
    {
        _tray = new FormsNotifyIcon
        {
            Icon = LoadTrayIcon(),
            Text = "Universal Battery Overlay",
            Visible = true
        };

        var menu = new FormsContextMenuStrip();
        menu.Items.Add("Open settings", null, (_, _) => ShowSettings());
        menu.Items.Add("Refresh now", null, async (_, _) => await _monitor.RefreshAsync());
        menu.Items.Add("Show / hide overlay", null, (_, _) => ToggleOverlay());
        menu.Items.Add("Open logs folder", null, (_, _) =>
        {
            Directory.CreateDirectory(StartupLogger.LogFolder);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = StartupLogger.LogFolder,
                UseShellExecute = true
            });
        });
        menu.Items.Add("Report bug on GitHub", null, (_, _) =>
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "https://github.com/sparta1st/UniversalBatteryOverlay/issues/new/choose",
                UseShellExecute = true
            });
        });
        menu.Items.Add(new FormsToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => Exit());
        _tray.ContextMenuStrip = menu;
        _tray.DoubleClick += (_, _) => ShowSettings();
        _tray.MouseClick += Tray_MouseClick;
        _tray.Visible = true;
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
            ? "Universal Battery Overlay"
            : "Universal Battery Overlay - " + string.Join(" | ", lines);

        try { _tray.Text = text.Length > 63 ? text[..63] : text; }
        catch (Exception ex) { StartupLogger.Error("Update tray text failed", ex); }
    }

    private void SafeTrayTip(int timeout, string title, string text, FormsToolTipIcon icon)
    {
        try { _tray?.ShowBalloonTip(timeout, title, text, icon); }
        catch (Exception ex) { StartupLogger.Error("Show tray balloon failed", ex); }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        StartupLogger.Info("Disposing controller.");

        try { _monitor.DevicesUpdated -= Monitor_DevicesUpdated; } catch { }
        try { _monitor.Dispose(); } catch (Exception ex) { StartupLogger.Error("Monitor dispose failed", ex); }
        if (_tray is not null)
        {
            try { _tray.Visible = false; } catch { }
            try { _tray.Dispose(); } catch (Exception ex) { StartupLogger.Error("Tray dispose failed", ex); }
            _tray = null;
        }
        try { _overlay.Close(); } catch (Exception ex) { StartupLogger.Error("Overlay close failed", ex); }
        try { _mainWindow.AllowClose(); _mainWindow.Close(); } catch (Exception ex) { StartupLogger.Error("Main window close failed", ex); }
        if (Instance == this) Instance = null;
    }
}
