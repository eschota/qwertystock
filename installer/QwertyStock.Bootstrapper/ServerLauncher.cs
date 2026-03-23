using System.Diagnostics;
using System.IO;
using System.Net.Http;

namespace QwertyStock.Bootstrapper;

public sealed class ServerLauncher
{
    private readonly InstallerLogger _log;

    public ServerLauncher(InstallerLogger log)
    {
        _log = log;
    }

    public Process Start(InstallerState state)
    {
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
        var p = new Process { StartInfo = psi, EnableRaisingEvents = true };
        if (!p.Start())
            throw new InvalidOperationException("Failed to start Python web server process.");

        _ = Task.Run(() => PumpLog(p.StandardOutput, InstallerPaths.ServerLog));
        _ = Task.Run(() => PumpLog(p.StandardError, InstallerPaths.ServerLog));
        return p;
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

    public static async Task WaitForHttpOkAsync(CancellationToken ct)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        var deadline = DateTime.UtcNow.AddMinutes(2);
        while (DateTime.UtcNow < deadline)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                using var resp = await http.GetAsync(InstallerPaths.LocalServerUrl, ct).ConfigureAwait(false);
                if (resp.IsSuccessStatusCode)
                    return;
            }
            catch
            {
                // retry
            }

            await Task.Delay(500, ct).ConfigureAwait(false);
        }

        throw new InvalidOperationException("Timed out waiting for http://localhost:3000/ to respond.");
    }

    public static void OpenBrowser()
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = InstallerPaths.LocalServerUrl.TrimEnd('/'),
            UseShellExecute = true,
        });
    }
}
