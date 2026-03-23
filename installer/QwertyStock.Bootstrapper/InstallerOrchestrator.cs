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

    public async Task RunAsync(IProgress<InstallProgress> progress, CancellationToken ct)
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException(InstallerStrings.ErrorWindowsOnly);

        progress.Report(new InstallProgress(2, InstallerStrings.ProgressPreparing, Indeterminate: false));
        Directory.CreateDirectory(InstallerPaths.Root);
        Directory.CreateDirectory(InstallerPaths.LogsDir);

        progress.Report(new InstallProgress(4, InstallerStrings.ProgressNetwork, Indeterminate: false));
        await OutboundProxySetup.EnsureAsync(_log, ct).ConfigureAwait(false);

        var state = _store.LoadOrCreate();
        var manifestUrl = string.IsNullOrWhiteSpace(state.UpdateManifestUrl)
            ? InstallerPaths.DefaultManifestUrl
            : state.UpdateManifestUrl!;

        progress.Report(new InstallProgress(5, InstallerStrings.ProgressSelfUpdate, Indeterminate: true));
        await _selfUpdate.ApplyIfNewerAsync(manifestUrl, ct).ConfigureAwait(false);

        progress.Report(new InstallProgress(18, InstallerStrings.ProgressPythonGit, Indeterminate: true));
        await Task.WhenAll(_python.EnsureAsync(state, ct), _git.EnsureAsync(state, ct)).ConfigureAwait(false);
        _store.Save(state);

        progress.Report(new InstallProgress(45, InstallerStrings.ProgressRepo, Indeterminate: true));
        await _repo.SyncAsync(state.GitBranch, ct).ConfigureAwait(false);

        if (!Directory.Exists(InstallerPaths.WebServerDir))
            throw new InvalidOperationException(InstallerStrings.ErrorRepoMissingWebServer);

        progress.Report(new InstallProgress(60, InstallerStrings.ProgressPip, Indeterminate: true));
        await _pip.InstallIfNeededAsync(state, ct).ConfigureAwait(false);
        _store.Save(state);

        progress.Report(new InstallProgress(75, InstallerStrings.ProgressPort, Indeterminate: false));
        if (await PortChecker.IsLocalPortInUseAsync(InstallerPaths.ServerPort, ct).ConfigureAwait(false))
            throw new InvalidOperationException(InstallerStrings.ErrorPortInUse);

        progress.Report(new InstallProgress(85, InstallerStrings.ProgressServer, Indeterminate: false));
        _server.Start(state);
        _store.Save(state);

        progress.Report(new InstallProgress(92, InstallerStrings.ProgressWaitHttp, Indeterminate: true));
        await ServerLauncher.WaitForHttpOkAsync(ct).ConfigureAwait(false);

        progress.Report(new InstallProgress(98, InstallerStrings.ProgressBrowser, Indeterminate: false));
        ServerLauncher.OpenBrowser();

        progress.Report(new InstallProgress(100, InstallerStrings.ProgressDone, Indeterminate: false));
        _log.Info("Pipeline completed successfully.");
    }
}
