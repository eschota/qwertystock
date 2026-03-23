using System.Diagnostics;

namespace QwertyStock.Bootstrapper;

public static class ProcessRunner
{
    /// <param name="killAfter">Если процесс не завершился за это время — Kill(entireProcessTree) и <see cref="TimeoutException"/>.</param>
    public static async Task<(int ExitCode, string StdOut, string StdErr)> RunAsync(
        string fileName,
        string arguments,
        string? workingDirectory,
        IReadOnlyDictionary<string, string>? extraEnvironment,
        CancellationToken ct,
        TimeSpan? killAfter = null)
    {
        var psi = new ProcessStartInfo(fileName, arguments)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = workingDirectory ?? Environment.CurrentDirectory,
        };
        if (extraEnvironment != null)
        {
            foreach (var kv in extraEnvironment)
                psi.Environment[kv.Key] = kv.Value;
        }

        using var p = new Process { StartInfo = psi, EnableRaisingEvents = true };
        if (!p.Start())
            throw new InvalidOperationException($"Failed to start process: {fileName}");

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        if (killAfter is { } t)
            linkedCts.CancelAfter(t);

        // Читать stdout/stderr без отмены по токауту — иначе ReadToEnd обрывается до слива буфера.
        var stdoutTask = p.StandardOutput.ReadToEndAsync(CancellationToken.None);
        var stderrTask = p.StandardError.ReadToEndAsync(CancellationToken.None);
        try
        {
            await p.WaitForExitAsync(linkedCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException ex)
        {
            TryKillProcessTree(p);
            try
            {
                await Task.WhenAll(stdoutTask, stderrTask).ConfigureAwait(false);
            }
            catch
            {
                // ignore drain errors after kill
            }

            if (ct.IsCancellationRequested)
                throw;

            throw new TimeoutException(
                $"Process exceeded {(killAfter?.ToString() ?? "timeout")}: {fileName} {arguments}",
                ex);
        }

        var stdout = await stdoutTask.ConfigureAwait(false);
        var stderr = await stderrTask.ConfigureAwait(false);
        return (p.ExitCode, stdout, stderr);
    }

    /// <summary>Streams stderr line-by-line while the process runs (avoids buffer deadlock with pip).</summary>
    public static async Task<int> RunWithStderrLinesAsync(
        string fileName,
        string arguments,
        string? workingDirectory,
        IReadOnlyDictionary<string, string>? extraEnvironment,
        Action<string> onStderrLine,
        CancellationToken ct,
        TimeSpan? killAfter = null)
    {
        var psi = new ProcessStartInfo(fileName, arguments)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = workingDirectory ?? Environment.CurrentDirectory,
        };
        if (extraEnvironment != null)
        {
            foreach (var kv in extraEnvironment)
                psi.Environment[kv.Key] = kv.Value;
        }

        using var p = new Process { StartInfo = psi, EnableRaisingEvents = true };
        if (!p.Start())
            throw new InvalidOperationException($"Failed to start process: {fileName}");

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        if (killAfter is { } t)
            linkedCts.CancelAfter(t);

        var stderrTask = ReadLinesAsync(p.StandardError, onStderrLine, linkedCts.Token);
        var stdoutTask = p.StandardOutput.ReadToEndAsync(CancellationToken.None);
        try
        {
            await p.WaitForExitAsync(linkedCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException ex)
        {
            TryKillProcessTree(p);
            try
            {
                await Task.WhenAll(stderrTask, stdoutTask).ConfigureAwait(false);
            }
            catch
            {
                // ignore
            }

            if (ct.IsCancellationRequested)
                throw;

            throw new TimeoutException(
                $"Process exceeded {(killAfter?.ToString() ?? "timeout")}: {fileName} {arguments}",
                ex);
        }

        await Task.WhenAll(stderrTask, stdoutTask).ConfigureAwait(false);
        return p.ExitCode;
    }

    private static void TryKillProcessTree(Process p)
    {
        try
        {
            if (p.HasExited)
                return;
            p.Kill(entireProcessTree: true);
            p.WaitForExit(8000);
        }
        catch
        {
            // best effort
        }
    }

    private static async Task ReadLinesAsync(
        StreamReader reader,
        Action<string> onLine,
        CancellationToken ct)
    {
        while (true)
        {
            var line = await reader.ReadLineAsync(ct).ConfigureAwait(false);
            if (line == null)
                break;
            onLine(line);
        }
    }

    public static IReadOnlyDictionary<string, string>? Merge(
        IReadOnlyDictionary<string, string>? a,
        IReadOnlyDictionary<string, string>? b)
    {
        if (a == null && b == null)
            return null;
        if (a == null)
            return b;
        if (b == null)
            return a;
        var d = new Dictionary<string, string>(a, StringComparer.OrdinalIgnoreCase);
        foreach (var kv in b)
            d[kv.Key] = kv.Value;
        return d;
    }
}
