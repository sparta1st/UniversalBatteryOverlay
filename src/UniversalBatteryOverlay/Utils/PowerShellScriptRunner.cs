using System.Diagnostics;

namespace UniversalBatteryOverlay.Utils;

public static class PowerShellScriptRunner
{
    public static async Task<string> RunAsync(string script, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var tempFile = Path.Combine(Path.GetTempPath(), "ubo_" + Guid.NewGuid().ToString("N") + ".ps1");

        try
        {
            await File.WriteAllTextAsync(tempFile, script, cancellationToken).ConfigureAwait(false);

            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            linkedCts.CancelAfter(timeout);

            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{tempFile}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            using var process = Process.Start(psi);
            if (process is null) return string.Empty;

            var stdoutTask = process.StandardOutput.ReadToEndAsync(linkedCts.Token);
            var stderrTask = process.StandardError.ReadToEndAsync(linkedCts.Token);

            try
            {
                await process.WaitForExitAsync(linkedCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                try { if (!process.HasExited) process.Kill(entireProcessTree: true); } catch { }
                throw;
            }

            var stdout = await stdoutTask.ConfigureAwait(false);
            var stderr = await stderrTask.ConfigureAwait(false);

            return string.IsNullOrWhiteSpace(stdout) ? stderr : stdout;
        }
        finally
        {
            try { if (File.Exists(tempFile)) File.Delete(tempFile); } catch { }
        }
    }
}
