using System.Diagnostics;
using System.Globalization;

namespace QwertyStock.Bootstrapper;

/// <summary>Windows: find which process is listening on a TCP port and terminate it.</summary>
public static class PortProcessTerminator
{
    /// <summary>Returns true if a process was killed or port appears free afterward.</summary>
    public static bool TryKillProcessUsingPort(int port, InstallerLogger log)
    {
        if (!OperatingSystem.IsWindows())
            return false;

        var pid = TryFindListeningPid(port);
        if (pid is null)
        {
            log.Info($"Port {port}: could not resolve owning PID (port may clear on its own).");
            return false;
        }

        if (pid is 0 or 4)
        {
            log.Info($"Port {port}: owning PID {pid} is a system process — not terminating.");
            return false;
        }

        try
        {
            using var p = Process.GetProcessById(pid.Value);
            var name = p.ProcessName;
            log.Info($"Port {port}: terminating PID {pid} ({name}) to free the port.");
            p.Kill(entireProcessTree: true);
            p.WaitForExit(8000);
            return true;
        }
        catch (Exception ex)
        {
            log.Error($"Port {port}: failed to terminate PID {pid}", ex);
            return false;
        }
    }

    internal static int? TryFindListeningPid(int port)
    {
        var fromPs = TryFindPidViaPowerShell(port);
        if (fromPs.HasValue)
            return fromPs;
        return TryFindPidViaNetstat(port);
    }

    private static int? TryFindPidViaPowerShell(int port)
    {
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
                $"(Get-NetTCPConnection -LocalPort {port.ToString(CultureInfo.InvariantCulture)} -State Listen -ErrorAction SilentlyContinue | Select-Object -First 1 -ExpandProperty OwningProcess)");
            p.Start();
            var stdout = p.StandardOutput.ReadToEnd();
            p.WaitForExit(15000);
            var t = stdout.Trim();
            if (string.IsNullOrEmpty(t) || !int.TryParse(t, NumberStyles.Integer, CultureInfo.InvariantCulture, out var pid))
                return null;
            return pid;
        }
        catch
        {
            return null;
        }
    }

    private static int? TryFindPidViaNetstat(int port)
    {
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
                return pid;
            }
        }
        catch
        {
            // ignore
        }

        return null;
    }
}
