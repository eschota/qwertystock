namespace QwertyStock.Bootstrapper;

public sealed class PipInstallService
{
    private readonly InstallerLogger _log;

    public PipInstallService(InstallerLogger log)
    {
        _log = log;
    }

    public async Task InstallIfNeededAsync(InstallerState state, CancellationToken ct)
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

        _log.Info("pip install -r requirements.txt …");
        var proxyEnv = ProxySession.GetProcessEnvironment();
        var r = await ProcessRunner.RunAsync(
            InstallerPaths.PythonExe,
            "-m pip install --no-cache-dir -r requirements.txt",
            InstallerPaths.WebServerDir,
            proxyEnv.Count > 0 ? proxyEnv : null,
            ct).ConfigureAwait(false);
        if (r.ExitCode != 0)
            throw new InvalidOperationException($"pip install failed (exit {r.ExitCode}): {r.StdErr}");

        state.RequirementsTxtSha256 = hash;
    }
}
