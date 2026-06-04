using FormsApplication = System.Windows.Forms.Application;
using UniversalBatteryOverlay.Services;

namespace UniversalBatteryOverlay;

public partial class App : System.Windows.Application
{
    private AppController? _controller;

    protected override void OnStartup(System.Windows.StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += (_, args) =>
        {
            StartupLogger.Error("Dispatcher unhandled exception", args.Exception);
            args.Handled = true;
            MessageBoxSafe("Universal Battery Overlay hit an error. The app will try to stay open.\n\nLog file:\n" + StartupLogger.StartupLogPath);
        };

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            StartupLogger.Error("AppDomain unhandled exception", args.ExceptionObject as Exception);
        };

        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            StartupLogger.Error("TaskScheduler unobserved exception", args.Exception);
            args.SetObserved();
        };

        try
        {
            StartupLogger.Info("App startup started. ProcessPath=" + (Environment.ProcessPath ?? "unknown") + "; BaseDir=" + AppContext.BaseDirectory + "; CurrentDir=" + Directory.GetCurrentDirectory());
            TrySetWorkingDirectoryToExeFolder();
            try { FormsApplication.EnableVisualStyles(); } catch { }

            _controller = new AppController();
            _controller.Start();
            StartupLogger.Info("App startup completed.");
        }
        catch (Exception ex)
        {
            StartupLogger.Error("Fatal startup error", ex);
            MessageBoxSafe("Universal Battery Overlay could not start.\n\nLog file:\n" + StartupLogger.StartupLogPath + "\n\nError:\n" + ex.Message);
            Shutdown(1);
        }
    }

    protected override void OnExit(System.Windows.ExitEventArgs e)
    {
        try { _controller?.Dispose(); }
        catch (Exception ex) { StartupLogger.Error("Error while exiting", ex); }
        base.OnExit(e);
    }

    private static void TrySetWorkingDirectoryToExeFolder()
    {
        try
        {
            var exe = Environment.ProcessPath;
            var folder = string.IsNullOrWhiteSpace(exe) ? null : Path.GetDirectoryName(exe);
            if (!string.IsNullOrWhiteSpace(folder) && Directory.Exists(folder))
                Directory.SetCurrentDirectory(folder);
        }
        catch (Exception ex)
        {
            StartupLogger.Error("Could not set working directory to EXE folder", ex);
        }
    }

    private static void MessageBoxSafe(string message)
    {
        try
        {
            System.Windows.MessageBox.Show(
                message,
                "Universal Battery Overlay",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
        }
        catch
        {
            // Ignore, logging already has the error.
        }
    }
}
