using System.Diagnostics;

namespace QwertyStock.Bootstrapper;

public static class ProcessRunner
{
    public static async Task<(int ExitCode, string StdOut, string StdErr)> RunAsync(
        string fileName,
        string arguments,
        string? workingDirectory,
        IReadOnlyDictionary<string, string>? extraEnvironment,
        CancellationToken ct)
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
        await p.WaitForExitAsync(ct).ConfigureAwait(false);
        var stdout = await p.StandardOutput.ReadToEndAsync(ct).ConfigureAwait(false);
        var stderr = await p.StandardError.ReadToEndAsync(ct).ConfigureAwait(false);
        return (p.ExitCode, stdout, stderr);
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
