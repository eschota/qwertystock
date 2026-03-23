using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net.Http;

namespace QwertyStock.Bootstrapper;

public sealed class ServerLauncher
{
    private static readonly string PidPath = Path.Combine(InstallerPaths.Root, "server.pid");

    private readonly InstallerLogger _log;

    public ServerLauncher(InstallerLogger log)
    {
        _log = log;
    }

    /// <summary>Last started or attached Python process (watchdog).</summary>
    public Process? LastProcess { get; private set; }

    public Process Start(InstallerState state)
    {
        Directory.CreateDirectory(InstallerPaths.Root);
        InstallerLogger.PrepareServerLogSession();
        var psi = new ProcessStartInfo
        {
            FileName = InstallerPaths.PythonExe,
            Arguments = state.LaunchArgs,
            WorkingDirectory = InstallerPaths.WebServerDir,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        psi.Environment["PORT"] = InstallerPaths.ServerPort.ToString(CultureInfo.InvariantCulture);
        psi.Environment["QS_DAEMON_SETTINGS_PATH"] = InstallerPaths.DaemonSettingsPath;
        psi.Environment["QS_PRODUCT_VERSION"] = ProductVersion.ReadFromRepoOrAssembly();
        var p = new Process { StartInfo = psi, EnableRaisingEvents = true };
        if (!p.Start())
            throw new InvalidOperationException("Failed to start Python web server process.");

        try
        {
            File.WriteAllText(PidPath, p.Id.ToString(CultureInfo.InvariantCulture));
        }
        catch
        {
            // ignore pid persistence failures
        }

        _ = Task.Run(() => PumpLog(p.StandardOutput, InstallerPaths.ServerLog));
        _ = Task.Run(() => PumpLog(p.StandardError, InstallerPaths.ServerLog));
        LastProcess = p;
        return p;
    }

    /// <summary>Attach to an already running server (e.g. port was in use and HTTP responded).</summary>
    public Process? TryAttachFromPidFile()
    {
        if (!File.Exists(PidPath))
            return null;
        try
        {
            var text = File.ReadAllText(PidPath).Trim();
            if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var pid))
                return null;
            var p = Process.GetProcessById(pid);
            if (p.HasExited)
                return null;
            var path = p.MainModule?.FileName;
            if (path == null
                || !path.Equals(InstallerPaths.PythonExe, StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    p.Dispose();
                }
                catch
                {
                    // ignore
                }

                return null;
            }

            p.EnableRaisingEvents = true;
            LastProcess = p;
            return p;
        }
        catch (Exception ex)
        {
            _log.Info($"Attach from PID file: {ex.Message}");
            return null;
        }
    }

    public void StopServerProcess()
    {
        var p = LastProcess;
        LastProcess = null;
        try
        {
            if (p is { HasExited: false })
            {
                p.Kill(entireProcessTree: true);
                p.WaitForExit(8000);
            }
        }
        catch (Exception ex)
        {
            _log.Info($"Stop server process: {ex.Message}");
        }
        finally
        {
            try
            {
                p?.Dispose();
            }
            catch
            {
                // ignore
            }
        }

        try
        {
            if (File.Exists(PidPath))
                File.Delete(PidPath);
        }
        catch
        {
            // ignore
        }
    }

    private static async Task PumpLog(StreamReader reader, string path)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            string? line;
            while ((line = await reader.ReadLineAsync().ConfigureAwait(false)) != null)
            {
                await File.AppendAllTextAsync(path, line + Environment.NewLine).ConfigureAwait(false);
            }
        }
        catch
        {
            // ignore logging failures
        }
    }

    /// <summary>Single GET to local server root; returns true if HTTP 2xx.</summary>
    public static async Task<bool> TryHttpOkAsync(CancellationToken ct)
    {
        var http = InstallerHttp.Client;
        try
        {
            using var attempt = CancellationTokenSource.CreateLinkedTokenSource(ct);
            attempt.CancelAfter(TimeSpan.FromSeconds(5));
            using var resp = await http.GetAsync(InstallerPaths.LocalServerUrl, attempt.Token).ConfigureAwait(false);
            return resp.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public static async Task WaitForHttpOkAsync(CancellationToken ct)
    {
        var http = InstallerHttp.Client;
        var deadline = DateTime.UtcNow.AddMinutes(2);
        while (DateTime.UtcNow < deadline)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                using var attempt = CancellationTokenSource.CreateLinkedTokenSource(ct);
                attempt.CancelAfter(TimeSpan.FromSeconds(5));
                using var resp = await http.GetAsync(InstallerPaths.LocalServerUrl, attempt.Token).ConfigureAwait(false);
                if (resp.IsSuccessStatusCode)
                    return;
            }
            catch
            {
                // retry
            }

            await Task.Delay(500, ct).ConfigureAwait(false);
        }

        throw new InvalidOperationException(
            string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                "Timed out waiting for {0} to respond.",
                InstallerPaths.LocalServerUrl.TrimEnd('/')));
    }

    public static void OpenBrowser()
    {
        // Только корень `/`: тот же HTML, что и `/cabinet`, но маршрут `/cabinet` появляется только после git sync нового main.py.
        Process.Start(new ProcessStartInfo
        {
            FileName = InstallerPaths.LocalServerUrl.TrimEnd('/'),
            UseShellExecute = true,
        });
    }
}
