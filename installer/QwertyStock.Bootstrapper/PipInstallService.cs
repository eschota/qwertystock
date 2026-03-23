namespace QwertyStock.Bootstrapper;

public readonly record struct PipSubProgress(double Fraction01, string? DetailLine, string? EtaHint);

public sealed class PipInstallService
{
    private readonly InstallerLogger _log;
    private const int MaxDetailLength = 220;
    private const int TailLinesForError = 12;

    public PipInstallService(InstallerLogger log)
    {
        _log = log;
    }

    /// <param name="force">
    /// When true, runs <c>pip install -r requirements.txt</c> even if the saved hash matches
    /// (e.g. after <c>git pull</c> changed code — re-syncs the venv with the pinned list).
    /// </param>
    public async Task InstallIfNeededAsync(
        InstallerState state,
        IProgress<PipSubProgress>? pipProgress,
        string? pipIndexUrl,
        CancellationToken ct,
        bool force = false)
    {
        var req = Path.Combine(InstallerPaths.WebServerDir, "requirements.txt");
        if (!File.Exists(req))
            throw new InvalidOperationException("Missing qwertystock_web_server/requirements.txt in the repository.");

        var bytes = await File.ReadAllBytesAsync(req, ct).ConfigureAwait(false);
        var hash = HttpHash.Sha256Hex(bytes);
        if (state.RequirementsTxtSha256 == hash)
        {
            if (!force)
            {
                _log.Info("requirements.txt hash unchanged — skipping pip install.");
                return;
            }

            _log.Info("requirements.txt hash unchanged — running pip install anyway (forced dependency sync).");
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

        var start = DateTime.UtcNow;
        var estSeconds = Math.Max(120.0, 30.0 + topLevel * 40.0);
        var estLineEvents = Math.Max(20, topLevel * 4);
        var lineEvents = 0;

        pipProgress?.Report(new PipSubProgress(0, InstallerStrings.PipStartingSummary(topLevel),
            InstallerStrings.PipEtaRough((int)estSeconds)));

        _log.Info("pip install -r requirements.txt …");
        var proxyEnv = ProxySession.GetProcessEnvironment();
        var tail = new List<string>();

        var indexArg = string.IsNullOrWhiteSpace(pipIndexUrl)
            ? ""
            : $" -i \"{pipIndexUrl.Trim()}\"";
        var exit = await ProcessRunner.RunWithStderrLinesAsync(
            InstallerPaths.PythonExe,
            "-m pip install --no-cache-dir -r requirements.txt" + indexArg,
            InstallerPaths.WebServerDir,
            proxyEnv.Count > 0 ? proxyEnv : null,
            line =>
            {
                _log.Info($"pip: {line}");
                tail.Add(line);
                if (tail.Count > 40)
                    tail.RemoveAt(0);
                lineEvents++;
                var elapsed = (DateTime.UtcNow - start).TotalSeconds;
                var lineFrac = Math.Min(0.97, lineEvents / (double)estLineEvents);
                var timeFrac = Math.Min(0.97, elapsed / estSeconds);
                var frac = Math.Max(lineFrac, timeFrac);
                var remaining = Math.Max(0, estSeconds - elapsed);
                var etaHint = remaining > 1
                    ? InstallerStrings.PipEtaRemaining((int)remaining)
                    : null;
                var detail = IsInterestingPipLine(line) ? Truncate(line, MaxDetailLength) : null;
                pipProgress?.Report(new PipSubProgress(frac, detail, etaHint));
            },
            ct).ConfigureAwait(false);

        if (exit != 0)
        {
            var errTail = string.Join(Environment.NewLine, tail.TakeLast(TailLinesForError));
            throw new InvalidOperationException(InstallerStrings.PipInstallFailed(exit, errTail));
        }

        pipProgress?.Report(new PipSubProgress(1, null, null));
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
