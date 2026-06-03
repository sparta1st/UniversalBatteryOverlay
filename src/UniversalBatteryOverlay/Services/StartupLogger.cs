namespace UniversalBatteryOverlay.Services;

public static class StartupLogger
{
    private static readonly object Sync = new();

    public static string LogFolder
    {
        get
        {
            var folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "UniversalBatteryOverlay",
                "logs");
            Directory.CreateDirectory(folder);
            return folder;
        }
    }

    public static string StartupLogPath => Path.Combine(LogFolder, "startup.log");

    public static void Info(string message) => Write("INFO", message);

    public static void Error(string message, Exception? exception = null)
    {
        Write("ERROR", exception is null ? message : message + Environment.NewLine + exception);
    }

    public static void Write(string level, string message)
    {
        try
        {
            lock (Sync)
            {
                File.AppendAllText(
                    StartupLogPath,
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{level}] {message}{Environment.NewLine}");
            }
        }
        catch
        {
            // Logging must never crash the app.
        }
    }
}
