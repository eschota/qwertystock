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
            throw new PlatformNotSupportedException("Этот установщик работает только в Windows.");

        progress.Report((2, "Подготовка…"));
        Directory.CreateDirectory(InstallerPaths.Root);
        Directory.CreateDirectory(InstallerPaths.LogsDir);

        var state = _store.LoadOrCreate();
        var manifestUrl = string.IsNullOrWhiteSpace(state.UpdateManifestUrl)
            ? InstallerPaths.DefaultManifestUrl
            : state.UpdateManifestUrl!;

        progress.Report((5, "Проверка обновлений установщика…"));
        await _selfUpdate.ApplyIfNewerAsync(manifestUrl, ct).ConfigureAwait(false);

        progress.Report((15, "Установка встроенного Python…"));
        await _python.EnsureAsync(state, ct).ConfigureAwait(false);
        _store.Save(state);

        progress.Report((30, "Установка portable Git…"));
        await _git.EnsureAsync(state, ct).ConfigureAwait(false);
        _store.Save(state);

        progress.Report((45, "Синхронизация репозитория…"));
        await _repo.SyncAsync(state.GitBranch, ct).ConfigureAwait(false);

        if (!Directory.Exists(InstallerPaths.WebServerDir))
            throw new InvalidOperationException(
                "В репозитории нет папки qwertystock_web_server/. Убедитесь, что она есть в ветке на сервере.");

        progress.Report((60, "Установка зависимостей Python (pip)…"));
        await _pip.InstallIfNeededAsync(state, ct).ConfigureAwait(false);
        _store.Save(state);

        progress.Report((75, "Проверка порта 3000…"));
        if (await PortChecker.IsLocalPortInUseAsync(InstallerPaths.ServerPort, ct).ConfigureAwait(false))
            throw new InvalidOperationException(
                "Порт 3000 занят. Закройте другое приложение, которое его использует.");

        progress.Report((85, "Запуск веб-сервера…"));
        _server.Start(state);
        _store.Save(state);

        progress.Report((92, "Ожидание ответа http://localhost:3000 …"));
        await ServerLauncher.WaitForHttpOkAsync(ct).ConfigureAwait(false);

        progress.Report((98, "Открытие браузера…"));
        ServerLauncher.OpenBrowser();

        progress.Report((100, "Готово. Сервер запущен."));
        _log.Info("Pipeline completed successfully.");
    }
}
