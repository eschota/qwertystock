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

    public ServerLauncher ServerLauncher => _server;

    /// <summary>
    /// Logon / <c>--startup</c> path: manifest (no exe self-update), then open cabinet if install is complete; otherwise full UI.
    /// </summary>
    public async Task<StartupLaunchResult> RunStartupLaunchAsync(CancellationToken ct)
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException(InstallerStrings.ErrorWindowsOnly);

        Directory.CreateDirectory(InstallerPaths.Root);
        Directory.CreateDirectory(InstallerPaths.LogsDir);

        await LoadManifestAsync(false, null, null, ct).ConfigureAwait(false);

        var state = _store.LoadOrCreate();
        if (!await CanQuickLaunchCabinetAsync(state, ct).ConfigureAwait(false))
            return StartupLaunchResult.NeedFullInstallerUi;

        if (await PortChecker.IsLocalPortInUseAsync(InstallerPaths.ServerPort, ct).ConfigureAwait(false))
        {
            if (await ServerLauncher.TryHttpOkAsync(ct).ConfigureAwait(false))
            {
                var attached = _server.TryAttachFromPidFile();
                if (attached != null)
                {
                    StartMenuShortcut.CreateOrUpdate();
                    ServerLauncher.OpenBrowser();
                    return StartupLaunchResult.RunTrayDaemon;
                }

                _log.Info(
                    "Port responds but our Python PID could not be attached — terminating the process that holds the port.");
            }

            await EnsurePortFreeAsync(null, ct).ConfigureAwait(false);
        }

        _server.Start(state);
        _store.Save(state);
        await ServerLauncher.WaitForHttpOkAsync(ct).ConfigureAwait(false);
        StartMenuShortcut.CreateOrUpdate();
        ServerLauncher.OpenBrowser();
        return StartupLaunchResult.RunTrayDaemon;
    }

    public async Task RunAsync(IProgress<InstallProgress>? progress, CancellationToken ct)
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException(InstallerStrings.ErrorWindowsOnly);

        progress?.Report(new InstallProgress(ProgressSegments.PrepareEnd, InstallerStrings.ProgressPreparing, false));
        Directory.CreateDirectory(InstallerPaths.Root);
        Directory.CreateDirectory(InstallerPaths.LogsDir);

        progress?.Report(new InstallProgress(ProgressSegments.PrepareEnd + 0.5, InstallerStrings.ProgressCheckingDisk, false));
        DiskSpaceChecker.EnsureFreeSpaceForInstall();

        IProgress<TransferProgress>? selfDl = null;
        if (progress != null)
        {
            var ui = progress;
            selfDl = new Progress<TransferProgress>(tp =>
            {
                var frac = tp.TotalBytes is > 0 ? tp.BytesReceived / (double)tp.TotalBytes.Value : 0;
                var pct = ProgressSegments.SelfUpdateStart +
                          (ProgressSegments.SelfUpdateEnd - ProgressSegments.SelfUpdateStart) * frac;
                ui.Report(new InstallProgress(
                    pct,
                    InstallerStrings.ProgressSelfUpdate,
                    false,
                    null,
                    TransferProgressFormatter.FormatSpeed(tp.BytesPerSecond),
                    TransferProgressFormatter.FormatEta(tp.EtaSeconds),
                    TransferProgressFormatter.FormatBytesOfTotal(tp.BytesReceived, tp.TotalBytes)));
            });
        }
        var manifest = await LoadManifestAsync(true, progress, selfDl, ct).ConfigureAwait(false);

        progress?.Report(new InstallProgress(ProgressSegments.RuntimeStart, InstallerStrings.ProgressPythonGit, false));

        var state = _store.LoadOrCreate();
        var needPython = !File.Exists(InstallerPaths.PythonExe)
                         || state.PythonEmbedVersion != InstallerPaths.PythonEmbedVersion;
        var needGit = !File.Exists(InstallerPaths.GitExe)
                      || state.MinGitVersion != InstallerPaths.MinGitVersion;
        var router = new RuntimeDownloadRouter(progress, needPython, needGit);
        var repoRemote = RuntimeAssetUrls.RepoRemoteUrl(manifest);
        await Task.WhenAll(
            _python.EnsureAsync(state, router.Python, manifest, ct),
            _git.EnsureAsync(state, router.Git, manifest, ct)).ConfigureAwait(false);
        _store.Save(state);

        progress?.Report(new InstallProgress(ProgressSegments.RepoStart, InstallerStrings.ProgressRepo, false));
        await _repo.SyncAsync(state.GitBranch, repoRemote, progress, ct).ConfigureAwait(false);

        if (!Directory.Exists(InstallerPaths.WebServerDir))
            throw new InvalidOperationException(InstallerStrings.ErrorRepoMissingWebServer);

        var lastPipDetail = (string?)null;
        var pipProgress = new Progress<PipSubProgress>(e =>
        {
            if (!string.IsNullOrEmpty(e.DetailLine))
                lastPipDetail = e.DetailLine;
            var pct = ProgressSegments.PipStart +
                      (ProgressSegments.PipEnd - ProgressSegments.PipStart) * Math.Clamp(e.Fraction01, 0, 1);
            var eta = string.IsNullOrEmpty(e.EtaHint) ? "—" : e.EtaHint;
            progress?.Report(new InstallProgress(
                pct,
                InstallerStrings.ProgressPip,
                false,
                e.DetailLine ?? lastPipDetail,
                SpeedText: "—",
                EtaText: eta,
                BytesText: null));
        });
        var pipIndex = string.IsNullOrWhiteSpace(manifest.PipIndexUrl) ? null : manifest.PipIndexUrl.Trim();
        await _pip.InstallIfNeededAsync(state, pipProgress, pipIndex, ct).ConfigureAwait(false);
        _store.Save(state);

        progress?.Report(new InstallProgress(ProgressSegments.PortEnd, InstallerStrings.ProgressPort, false));
        await EnsurePortFreeAsync(progress, ct).ConfigureAwait(false);

        progress?.Report(new InstallProgress(ProgressSegments.ServerEnd, InstallerStrings.ProgressServer, false));
        _server.Start(state);
        _store.Save(state);

        progress?.Report(new InstallProgress(ProgressSegments.HttpWaitEnd - 1, InstallerStrings.ProgressWaitHttp, false));
        await ServerLauncher.WaitForHttpOkAsync(ct).ConfigureAwait(false);

        progress?.Report(new InstallProgress(99, InstallerStrings.ProgressBrowser, false));
        ServerLauncher.OpenBrowser();

        progress?.Report(new InstallProgress(100, InstallerStrings.ProgressDone, false));
        WindowsStartup.Register();
        StartMenuShortcut.CreateOrUpdate();
        _log.Info("Pipeline completed successfully.");
    }

    /// <summary>
    /// Background git sync + pip. Returns true if the FastAPI process should be restarted (repo or deps changed).
    /// </summary>
    public async Task<bool> BackgroundSyncRepositoryAsync(CancellationToken ct)
    {
        await OutboundProxySetup.EnsureAsync(_log, ct).ConfigureAwait(false);
        var state = _store.LoadOrCreate();
        var manifestUrl = string.IsNullOrWhiteSpace(state.UpdateManifestUrl)
            ? InstallerPaths.DefaultManifestUrl
            : state.UpdateManifestUrl!;
        var manifest = await InstallerManifestFetcher.FetchAsync(manifestUrl, ct).ConfigureAwait(false);

        var headBefore = await GitRepositoryHelper.ReadHeadAsync(ct).ConfigureAwait(false);
        var repoRemote = RuntimeAssetUrls.RepoRemoteUrl(manifest);
        await _repo.SyncAsync(state.GitBranch, repoRemote, null, ct).ConfigureAwait(false);
        _store.Save(state);

        var headAfter = await GitRepositoryHelper.ReadHeadAsync(ct).ConfigureAwait(false);
        var headChanged = !string.Equals(headBefore, headAfter, StringComparison.Ordinal);

        var reqPath = Path.Combine(InstallerPaths.WebServerDir, "requirements.txt");
        if (!File.Exists(reqPath))
            throw new InvalidOperationException("Missing qwertystock_web_server/requirements.txt after sync.");

        var bytes = await File.ReadAllBytesAsync(reqPath, ct).ConfigureAwait(false);
        var reqHash = HttpHash.Sha256Hex(bytes);
        var requirementsMismatch = string.IsNullOrWhiteSpace(state.RequirementsTxtSha256)
                                   || !HttpHash.EqualsHex(state.RequirementsTxtSha256, reqHash);

        var pipIndex = string.IsNullOrWhiteSpace(manifest.PipIndexUrl) ? null : manifest.PipIndexUrl.Trim();
        await _pip.InstallIfNeededAsync(state, null, pipIndex, ct).ConfigureAwait(false);
        _store.Save(state);

        return headChanged || requirementsMismatch;
    }

    private async Task<InstallerManifest> LoadManifestAsync(
        bool applySelfUpdateExe,
        IProgress<InstallProgress>? progress,
        IProgress<TransferProgress>? selfDl,
        CancellationToken ct)
    {
        progress?.Report(new InstallProgress(ProgressSegments.NetworkEnd, InstallerStrings.ProgressNetwork, false));
        await OutboundProxySetup.EnsureAsync(_log, ct).ConfigureAwait(false);
        var state = _store.LoadOrCreate();
        var manifestUrl = string.IsNullOrWhiteSpace(state.UpdateManifestUrl)
            ? InstallerPaths.DefaultManifestUrl
            : state.UpdateManifestUrl!;
        progress?.Report(new InstallProgress(ProgressSegments.NetworkEnd + 0.5, InstallerStrings.ProgressManifest, false));
        var manifest = await InstallerManifestFetcher.FetchAsync(manifestUrl, ct).ConfigureAwait(false);
        if (applySelfUpdateExe)
            await _selfUpdate.ApplyIfNewerAsync(manifest, selfDl, ct).ConfigureAwait(false);
        return manifest;
    }

    private static async Task<bool> CanQuickLaunchCabinetAsync(InstallerState state, CancellationToken ct)
    {
        if (!File.Exists(InstallerPaths.PythonExe))
            return false;
        if (!Directory.Exists(InstallerPaths.WebServerDir))
            return false;
        var mainPy = Path.Combine(InstallerPaths.WebServerDir, "main.py");
        if (!File.Exists(mainPy))
            return false;
        var req = Path.Combine(InstallerPaths.WebServerDir, "requirements.txt");
        if (!File.Exists(req))
            return false;
        var bytes = await File.ReadAllBytesAsync(req, ct).ConfigureAwait(false);
        var hash = HttpHash.Sha256Hex(bytes);
        if (string.IsNullOrWhiteSpace(state.RequirementsTxtSha256))
            return false;
        return HttpHash.EqualsHex(state.RequirementsTxtSha256, hash);
    }

    /// <summary>Kills the process listening on <see cref="InstallerPaths.ServerPort"/> if the port is still busy.</summary>
    private async Task EnsurePortFreeAsync(IProgress<InstallProgress>? progress, CancellationToken ct)
    {
        if (!await PortChecker.IsLocalPortInUseAsync(InstallerPaths.ServerPort, ct).ConfigureAwait(false))
            return;

        progress?.Report(new InstallProgress(ProgressSegments.PortEnd, InstallerStrings.ProgressPortFreeing, false));
        _log.Info($"Port {InstallerPaths.ServerPort} is in use — attempting to terminate the owning process.");
        PortProcessTerminator.TryKillProcessUsingPort(InstallerPaths.ServerPort, _log);
        await Task.Delay(750, ct).ConfigureAwait(false);
        for (var i = 0; i < 25; i++)
        {
            if (!await PortChecker.IsLocalPortInUseAsync(InstallerPaths.ServerPort, ct).ConfigureAwait(false))
                return;
            await Task.Delay(150, ct).ConfigureAwait(false);
        }

        if (await PortChecker.IsLocalPortInUseAsync(InstallerPaths.ServerPort, ct).ConfigureAwait(false))
            throw new InvalidOperationException(InstallerStrings.ErrorPortInUse);
    }
}
