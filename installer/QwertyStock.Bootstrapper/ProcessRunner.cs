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
}
