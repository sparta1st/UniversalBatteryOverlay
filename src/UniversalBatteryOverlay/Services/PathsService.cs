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
}
