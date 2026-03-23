namespace QwertyStock.Bootstrapper;

public sealed class PipInstallService
{
    private readonly InstallerLogger _log;
    private const int MaxDetailLength = 220;
    private const int TailLinesForError = 12;

    public PipInstallService(InstallerLogger log)
    {
        _log = log;
    }

    public async Task InstallIfNeededAsync(InstallerState state, IProgress<string>? pipActivity, CancellationToken ct)
    {
        var req = Path.Combine(InstallerPaths.WebServerDir, "requirements.txt");
        if (!File.Exists(req))
            throw new InvalidOperationException("Missing qwertystock_web_server/requirements.txt in the repository.");

        var bytes = await File.ReadAllBytesAsync(req, ct).ConfigureAwait(false);
        var hash = HttpHash.Sha256Hex(bytes);
        if (state.RequirementsTxtSha256 == hash)
        {
            _log.Info("requirements.txt hash unchanged — skipping pip install.");
            return;
        }

        var reqLines = await File.ReadAllLinesAsync(req, ct).ConfigureAwait(false);
        var topLevel = 0;
        foreach (var raw in reqLines)
        {
            var t = raw.Trim();
            if (t.Length == 0 || t.StartsWith('#'))
                continue;
            if (t.StartsWith('-'))
                continue;
            topLevel++;
        }

        pipActivity?.Report(InstallerStrings.PipStartingSummary(topLevel));

        _log.Info("pip install -r requirements.txt …");
        var proxyEnv = ProxySession.GetProcessEnvironment();
        var tail = new List<string>();

        var exit = await ProcessRunner.RunWithStderrLinesAsync(
            InstallerPaths.PythonExe,
            "-m pip install --no-cache-dir -r requirements.txt",
            InstallerPaths.WebServerDir,
            proxyEnv.Count > 0 ? proxyEnv : null,
            line =>
            {
                _log.Info($"pip: {line}");
                tail.Add(line);
                if (tail.Count > 40)
                    tail.RemoveAt(0);
                if (IsInterestingPipLine(line))
                    pipActivity?.Report(Truncate(line, MaxDetailLength));
            },
            ct).ConfigureAwait(false);

        if (exit != 0)
        {
            var errTail = string.Join(Environment.NewLine, tail.TakeLast(TailLinesForError));
            throw new InvalidOperationException(InstallerStrings.PipInstallFailed(exit, errTail));
        }

        state.RequirementsTxtSha256 = hash;
    }

    private static bool IsInterestingPipLine(string line)
    {
        var t = line.Trim();
        if (t.Length == 0)
            return false;
        return t.Contains("Downloading", StringComparison.OrdinalIgnoreCase)
               || t.Contains("Installing", StringComparison.OrdinalIgnoreCase)
               || t.Contains("Collecting", StringComparison.OrdinalIgnoreCase)
               || t.Contains("Requirement already satisfied", StringComparison.OrdinalIgnoreCase)
               || t.Contains("Successfully installed", StringComparison.OrdinalIgnoreCase)
               || t.Contains("Using cached", StringComparison.OrdinalIgnoreCase)
               || t.Contains("Building wheel", StringComparison.OrdinalIgnoreCase)
               || t.Contains("Running setup", StringComparison.OrdinalIgnoreCase)
               || t.Contains("ERROR:", StringComparison.OrdinalIgnoreCase)
               || t.Contains("WARNING:", StringComparison.OrdinalIgnoreCase);
    }

    private static string Truncate(string s, int max)
    {
        if (s.Length <= max)
            return s;
        return s[..(max - 1)] + "…";
    }
}
