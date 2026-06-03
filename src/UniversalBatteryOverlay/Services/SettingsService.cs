using System.IO;
using System.Text.Json;
using Microsoft.Win32;
using UniversalBatteryOverlay.Models;

namespace UniversalBatteryOverlay.Services;

public sealed class SettingsService
{
    private readonly string _settingsPath;
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppRunName = "UniversalBatteryOverlay";

    public SettingsService()
    {
        var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "UniversalBatteryOverlay");
        Directory.CreateDirectory(folder);
        _settingsPath = Path.Combine(folder, "settings.json");
    }

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(_settingsPath)) return new AppSettings();
            var json = File.ReadAllText(_settingsPath);
            var settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();

            // Safety migration: older builds could leave experimental generic HID readers
            // enabled in %APPDATA%. Force ONLY the generic/universal HID reader off so
            // keyboard dongles are never probed automatically after upgrading.
            // The Razer reader is narrowly targeted to the Viper mouse VID/PID and mouse collection.
            settings.EnableUniversalHidBatteryReader = false;
            settings.EnableRazerDirectBatteryReader = true;
            settings.EnableLogitechG733DirectBatteryReader = true;
            settings.EnableHeadsetControlCliReader = true;
            settings.ShowUnknownDevices = true;
            return settings;
        }
        catch
        {
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        var json = JsonSerializer.Serialize(settings, _jsonOptions);
        File.WriteAllText(_settingsPath, json);
    }

    public void SetAutostart(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, true);
            if (key is null) return;
            if (enabled)
            {
                var exe = Environment.ProcessPath ?? System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
                if (!string.IsNullOrWhiteSpace(exe))
                    key.SetValue(AppRunName, $"\"{exe}\"");
            }
            else
            {
                key.DeleteValue(AppRunName, false);
            }
        }
        catch
        {
            // Non-critical. The app still runs without startup registration.
        }
    }
}
