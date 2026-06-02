using System.IO;
namespace UniversalBatteryOverlay.Services;

public static class PathsService
{
    public static string AppDataFolder
    {
        get
        {
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "UniversalBatteryOverlay");
            Directory.CreateDirectory(folder);
            return folder;
        }
    }

    public static string ReadersFolder
    {
        get
        {
            var folder = Path.Combine(AppDataFolder, "readers");
            Directory.CreateDirectory(folder);
            EnsureSampleReader(folder);
            return folder;
        }
    }

    private static void EnsureSampleReader(string folder)
    {
        var sample = Path.Combine(folder, "sample-device-reader.ps1");
        if (File.Exists(sample)) return;

        File.WriteAllText(sample, """
# Sample external reader for Universal Battery Overlay.
# Rename this file to something not starting with 'sample' to activate it.
# Output one object or an array of objects as JSON:
# [
#   { "name":"My Device", "deviceType":"Mouse", "batteryPercent":72, "isCharging":false, "status":"OK", "reader":"My custom reader" }
# ]
@(
  [pscustomobject]@{
    name = "Example Wireless Device"
    deviceType = "Device"
    batteryPercent = 72
    isCharging = $false
    status = "Demo only"
    reader = "Sample PowerShell reader"
  }
) | ConvertTo-Json -Compress
""");
    }
}
