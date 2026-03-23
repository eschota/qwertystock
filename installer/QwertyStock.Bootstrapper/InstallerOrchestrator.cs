namespace QwertyStock.Bootstrapper;

public sealed class InstallerOrchestrator
{
    private readonly InstallerLogger _log;
    private readonly InstallerStateStore _store = new();
    private readonly SelfUpdateService _selfUpdate;
    private readonly PythonRuntimeBootstrapper _python;
    private readonly GitRuntimeBootstrapper _git;
    private readonly RepositorySyncService _repo;
    private readonly PipInstallService _pip;
    private readonly ServerLauncher _server;

    public InstallerOrchestrator(InstallerLogger log)
    {
        _log = log;
        _selfUpdate = new SelfUpdateService(log);
        _python = new PythonRuntimeBootstrapper(log);
        _git = new GitRuntimeBootstrapper(log);
        _repo = new RepositorySyncService(log);
        _pip = new PipInstallService(log);
        _server = new ServerLauncher(log);
    }

    public async Task RunAsync(IProgress<(int percent, string message)> progress, CancellationToken ct)
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("QwertyStock bootstrapper runs on Windows only.");

        progress.Report((2, "Preparing…"));
        Directory.CreateDirectory(InstallerPaths.Root);
        Directory.CreateDirectory(InstallerPaths.LogsDir);

        var state = _store.LoadOrCreate();
        var manifestUrl = string.IsNullOrWhiteSpace(state.UpdateManifestUrl)
            ? InstallerPaths.DefaultManifestUrl
            : state.UpdateManifestUrl!;

        progress.Report((5, "Checking for installer updates…"));
        await _selfUpdate.ApplyIfNewerAsync(manifestUrl, ct).ConfigureAwait(false);

        progress.Report((15, "Ensuring Python runtime…"));
        await _python.EnsureAsync(state, ct).ConfigureAwait(false);
        _store.Save(state);

        progress.Report((30, "Ensuring Git…"));
        await _git.EnsureAsync(state, ct).ConfigureAwait(false);
        _store.Save(state);

        progress.Report((45, "Synchronizing repository…"));
        await _repo.SyncAsync(state.GitBranch, ct).ConfigureAwait(false);

        if (!Directory.Exists(InstallerPaths.WebServerDir))
            throw new InvalidOperationException(
                "qwertystock_web_server/ is missing from the repository. Ensure it exists on the remote branch.");

        progress.Report((60, "Installing Python dependencies…"));
        await _pip.InstallIfNeededAsync(state, ct).ConfigureAwait(false);
        _store.Save(state);

        progress.Report((75, "Checking port 3000…"));
        if (await PortChecker.IsLocalPortInUseAsync(InstallerPaths.ServerPort, ct).ConfigureAwait(false))
            throw new InvalidOperationException(
                "Port 3000 is already in use. Stop the other application or change the conflicting service.");

        progress.Report((85, "Starting web server…"));
        _server.Start(state);
        _store.Save(state);

        progress.Report((92, "Waiting for http://localhost:3000 …"));
        await ServerLauncher.WaitForHttpOkAsync(ct).ConfigureAwait(false);

        progress.Report((98, "Opening browser…"));
        ServerLauncher.OpenBrowser();

        progress.Report((100, "Done. Server is running."));
        _log.Info("Pipeline completed successfully.");
    }
}
