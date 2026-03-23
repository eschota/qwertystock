using System.Diagnostics;
using System.Globalization;
using System.Linq;

namespace QwertyStock.Bootstrapper;

/// <summary>Windows: find processes listening on a TCP port and terminate them; optional kill via server.pid.</summary>
public static class PortProcessTerminator
{
    private static readonly string ServerPidPath = Path.Combine(InstallerPaths.Root, "server.pid");

    /// <summary>Kills every non-system process found listening on <paramref name="port"/>.</summary>
    public static bool TryKillProcessUsingPort(int port, InstallerLogger log)
    {
        if (!OperatingSystem.IsWindows())
            return false;

        var pids = CollectListeningPids(port);
        if (pids.Count == 0)
        {
            log.Info($"Port {port}: could not resolve owning PID (port may clear on its own).");
            return false;
        }

        var any = false;
        foreach (var pid in pids)
        {
            if (pid is 0 or 4)
            {
                log.Info($"Port {port}: skip system PID {pid}.");
                continue;
            }

            try
            {
                using var p = Process.GetProcessById(pid);
                var name = p.ProcessName;
                log.Info($"Port {port}: terminating PID {pid} ({name}) to free the port.");
                p.Kill(entireProcessTree: true);
                p.WaitForExit(8000);
                any = true;
            }
            catch (Exception ex)
            {
                log.Error($"Port {port}: failed to terminate PID {pid}", ex);
            }
        }

        return any;
    }

    /// <summary>
    /// Завершает embed-python из <c>server.pid</c>, если путь к процессу совпадает с рантаймом — надёжно при сбое Get-NetTCPConnection.
    /// </summary>
    /// <param name="log">Опционально; без логгера вызывать можно из деинсталлятора.</param>
    public static bool TryKillPythonServerFromPidFile(InstallerLogger? log = null)
    {
        if (!OperatingSystem.IsWindows() || !File.Exists(ServerPidPath))
            return false;

        try
        {
            var text = File.ReadAllText(ServerPidPath).Trim();
            if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var pid))
                return false;

            using var p = Process.GetProcessById(pid);
            var path = p.MainModule?.FileName;
            if (path == null
                || !path.Equals(InstallerPaths.PythonExe, StringComparison.OrdinalIgnoreCase))
            {
                log?.Info($"server.pid PID {pid} is not embed python — skipping.");
                return false;
            }

            log?.Info($"Terminating Python server from server.pid (PID {pid}) to free the cabinet port.");
            p.Kill(entireProcessTree: true);
            p.WaitForExit(8000);
            return true;
        }
        catch (Exception ex)
        {
            log?.Info($"server.pid kill: {ex.Message}");
            return false;
        }
    }

    internal static int? TryFindListeningPid(int port)
    {
        var list = CollectListeningPids(port);
        return list.Count > 0 ? list[0] : null;
    }

    private static List<int> CollectListeningPids(int port)
    {
        var set = new HashSet<int>();
        foreach (var pid in TryFindPidsViaPowerShell(port))
            set.Add(pid);
        foreach (var pid in TryFindPidsViaNetstat(port))
            set.Add(pid);
        return set.OrderBy(x => x).ToList();
    }

    private static List<int> TryFindPidsViaPowerShell(int port)
    {
        var list = new List<int>();
        try
        {
            using var p = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                },
            };
            p.StartInfo.ArgumentList.Add("-NoProfile");
            p.StartInfo.ArgumentList.Add("-NonInteractive");
            p.StartInfo.ArgumentList.Add("-ExecutionPolicy");
            p.StartInfo.ArgumentList.Add("Bypass");
            p.StartInfo.ArgumentList.Add("-Command");
            p.StartInfo.ArgumentList.Add(
                $"Get-NetTCPConnection -LocalPort {port.ToString(CultureInfo.InvariantCulture)} -State Listen -ErrorAction SilentlyContinue " +
                "| ForEach-Object { $_.OwningProcess } | Sort-Object -Unique");
            p.Start();
            var stdout = p.StandardOutput.ReadToEnd();
            p.WaitForExit(15000);
            foreach (var line in stdout.Split('\n'))
            {
                var t = line.Trim().TrimEnd('\r');
                if (t.Length == 0)
                    continue;
                if (int.TryParse(t, NumberStyles.Integer, CultureInfo.InvariantCulture, out var pid) && pid > 0)
                    list.Add(pid);
            }
        }
        catch
        {
            // ignore
        }

        return list;
    }

    private static List<int> TryFindPidsViaNetstat(int port)
    {
        var list = new List<int>();
        try
        {
            using var p = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "netstat.exe",
                    Arguments = "-ano",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true,
                },
            };
            p.Start();
            var output = p.StandardOutput.ReadToEnd();
            p.WaitForExit(30000);
            var portToken = ":" + port.ToString(CultureInfo.InvariantCulture);
            foreach (var line in output.Split('\n'))
            {
                var trimmed = line.TrimEnd('\r');
                if (!trimmed.Contains("LISTENING", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (!trimmed.Contains(portToken, StringComparison.Ordinal))
                    continue;
                var parts = trimmed.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 5)
                    continue;
                if (!parts[0].Equals("TCP", StringComparison.OrdinalIgnoreCase))
                    continue;
                var local = parts[1];
                if (!local.EndsWith(portToken, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!int.TryParse(parts[^1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var pid))
                    continue;
                if (pid <= 0)
                    continue;
                list.Add(pid);
            }
        }
        catch
        {
            // ignore
        }

        return list;
    }
}
