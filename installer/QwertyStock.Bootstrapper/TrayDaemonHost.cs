using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Timers;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Threading;
using Application = System.Windows.Application;

namespace QwertyStock.Bootstrapper;

/// <summary>Tray icon, Python watchdog, background git/pip sync.</summary>
public sealed class TrayDaemonHost : IDisposable
{
    private readonly InstallerLogger _log;
    private readonly InstallerOrchestrator _orch;
    private readonly InstallerStateStore _store = new();

    /// <summary>Сериализация долгого git/manifest/pip (BackgroundSyncRepositoryAsync) и ручного обновления.</summary>
    private readonly SemaphoreSlim _repoSyncGate = new(1, 1);

    /// <summary>Сериализация остановки/запуска embed-python (watchdog и перезапуск после sync). Не зависит от долгого git.</summary>
    private readonly SemaphoreSlim _pythonRestartGate = new(1, 1);

    private Mutex? _singletonMutex;
    private NotifyIcon? _notifyIcon;
    private FileSystemWatcher? _settingsWatcher;
    private readonly DaemonSettingsStore _daemonSettingsStore = new();
    private volatile bool _shutdownRequested;
    private volatile bool _shutdownForRestart;

    private int _pollIntervalSeconds = 60;

    /// <summary>Текущий прогон BackgroundSyncRepositoryAsync — отменяется при ручном «Обновить».</summary>
    private volatile CancellationTokenSource? _backgroundSyncRunCts;

    private CancellationTokenSource? _repoSyncLoopCts;
    private Task? _repoSyncLoopTask;

    public TrayDaemonHost(InstallerLogger log, InstallerOrchestrator orchestrator)
    {
        _log = log;
        _orch = orchestrator;
    }

    /// <summary>Returns false if another daemon instance is already running (browser opened).</summary>
    public bool TryEnterTrayDaemon()
    {
        if (!TryAcquireSingletonMutex())
        {
            ServerLauncher.OpenBrowser();
            return false;
        }

        try
        {
            Application.Current.Exit += OnApplicationExit;
            var settings = _daemonSettingsStore.LoadOrCreate();
            Interlocked.Exchange(ref _pollIntervalSeconds, Math.Max(1, settings.RepoPollIntervalSeconds));
            CreateNotifyIcon();
            EnsureWatchdog();
            StartRepoSyncLoop();
            StartSettingsWatcher();
            return true;
        }
        catch
        {
            ReleaseSingletonMutex();
            throw;
        }
    }

    private void StartRepoSyncLoop()
    {
        try
        {
            _repoSyncLoopCts?.Cancel();
        }
        catch
        {
            // ignore
        }

        try
        {
            _repoSyncLoopTask?.Wait(TimeSpan.FromSeconds(3));
        }
        catch
        {
            // ignore
        }

        try
        {
            _repoSyncLoopCts?.Dispose();
        }
        catch
        {
            // ignore
        }

        _repoSyncLoopCts = new CancellationTokenSource();
        var ct = _repoSyncLoopCts.Token;
        _log.Info("Tray: sequential repo sync loop started (single worker; no overlapping timer ticks).");
        _repoSyncLoopTask = Task.Run(() => RepoSyncLoopAsync(ct), CancellationToken.None);
    }

    /// <summary>Один последовательный цикл: нет параллельных тиков таймера и ветки WaitAsync(0) «skipped».</summary>
    private async Task RepoSyncLoopAsync(CancellationToken ct)
    {
        try
        {
            await Task.Delay(1500, ct).ConfigureAwait(false);
            while (!ct.IsCancellationRequested && !_shutdownRequested)
            {
                await RunSingleScheduledRepoSyncAsync(ct).ConfigureAwait(false);
                var sec = Volatile.Read(ref _pollIntervalSeconds);
                var ms = Math.Max(1000, sec * 1000);
                await Task.Delay(ms, ct).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // shutdown or ct cancelled
        }
        catch (Exception ex)
        {
            _log.Error("Repo sync loop failed", ex);
        }
    }

    private async Task RunSingleScheduledRepoSyncAsync(CancellationToken ct)
    {
        if (_shutdownRequested)
            return;

        await _repoSyncGate.WaitAsync(ct).ConfigureAwait(false);
        using var runCts = new CancellationTokenSource(TimeSpan.FromMinutes(25));
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, runCts.Token);
        try
        {
            _backgroundSyncRunCts = linked;
            _log.Info("Background repo sync: starting (git fetch/reset + pip)…");
            var sw = System.Diagnostics.Stopwatch.StartNew();
            bool changed;
            try
            {
                changed = await _orch.BackgroundSyncRepositoryAsync(linked.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                _log.Info("Background repo sync: stopped (deadline 25 min, shutdown, or manual cancel).");
                changed = false;
            }

            _log.Info(
                $"Background repo sync completed in {sw.Elapsed.TotalSeconds:F1}s (cabinet restart: {changed}).");

            if (changed)
                await RestartServerForSyncAsync().ConfigureAwait(false);
        }
        finally
        {
            if (ReferenceEquals(_backgroundSyncRunCts, linked))
                _backgroundSyncRunCts = null;
            _repoSyncGate.Release();
        }
    }

    private bool TryAcquireSingletonMutex()
    {
        var createdNew = false;
        try
        {
            _singletonMutex = new Mutex(true, @"Local\QwertyStockTrayDaemon", out createdNew);
        }
        catch
        {
            return false;
        }

        if (!createdNew)
        {
            try
            {
                _singletonMutex?.Dispose();
            }
            catch
            {
                // ignore
            }

            _singletonMutex = null;
            return false;
        }

        return true;
    }

    private void ReleaseSingletonMutex()
    {
        if (_singletonMutex == null)
            return;
        try
        {
            _singletonMutex.ReleaseMutex();
        }
        catch
        {
            // ignore
        }

        try
        {
            _singletonMutex.Dispose();
        }
        catch
        {
            // ignore
        }

        _singletonMutex = null;
    }

    private void OnApplicationExit(object sender, ExitEventArgs e)
    {
        ReleaseSingletonMutex();
    }

    private void CreateNotifyIcon()
    {
        var icon = LoadTrayIcon();
        _notifyIcon = new NotifyIcon
        {
            Icon = icon,
            Text = InstallerStrings.TrayTooltipFormat(ProductVersion.ReadFromRepoOrAssembly()),
            Visible = true,
        };

        var menu = new ContextMenuStrip();
        menu.Items.Add(InstallerStrings.TrayOpenCabinet, null, (_, _) => ServerLauncher.OpenBrowser());
        menu.Items.Add(InstallerStrings.TrayForceUpdate, null, async (_, _) => await ForceUpdateAsync().ConfigureAwait(true));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(InstallerStrings.TrayExit, null, (_, _) => ExitFromTray());

        _notifyIcon.ContextMenuStrip = menu;
        _notifyIcon.DoubleClick += (_, _) => ServerLauncher.OpenBrowser();
    }

    private static Icon LoadTrayIcon()
    {
        try
        {
            var s = Application.GetResourceStream(new Uri("pack://application:,,,/Assets/app.ico"))?.Stream;
            if (s != null)
                return new Icon(s);
        }
        catch
        {
            // fall through
        }

        return SystemIcons.Application;
    }

    private void EnsureWatchdog()
    {
        var p = _orch.ServerLauncher.LastProcess;
        if (p != null && !p.HasExited)
        {
            WireWatchdog(p);
            return;
        }

        try
        {
            var state = _store.LoadOrCreate();
            p = _orch.ServerLauncher.Start(state);
            WireWatchdog(p);
            _ = ServerLauncher.WaitForHttpOkAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            _log.Error("Tray: could not start or attach Python server", ex);
        }
    }

    private void WireWatchdog(Process p)
    {
        p.EnableRaisingEvents = true;
        p.Exited -= OnPythonExited;
        p.Exited += OnPythonExited;
    }

    private void OnPythonExited(object? sender, EventArgs e)
    {
        if (_shutdownRequested || _shutdownForRestart)
            return;
        Application.Current.Dispatcher.BeginInvoke(
            new Action(() => _ = WatchdogRestartAsync()));
    }

    private async Task WatchdogRestartAsync()
    {
        if (_shutdownRequested || _shutdownForRestart)
            return;
        await Task.Delay(1500).ConfigureAwait(true);
        if (_shutdownRequested || _shutdownForRestart)
            return;

        if (!await _pythonRestartGate.WaitAsync(TimeSpan.FromSeconds(30)).ConfigureAwait(true))
        {
            _log.Info("Watchdog: could not acquire python restart lock within 30s.");
            return;
        }

        try
        {
            if (_shutdownRequested)
                return;
            var state = _store.LoadOrCreate();
            var p = _orch.ServerLauncher.Start(state);
            WireWatchdog(p);
            await ServerLauncher.WaitForHttpOkAsync(CancellationToken.None).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _log.Error("Watchdog: restart failed", ex);
        }
        finally
        {
            try
            {
                _pythonRestartGate.Release();
            }
            catch
            {
                // ignore
            }
        }
    }

    private async Task RestartServerForSyncAsync()
    {
        await _pythonRestartGate.WaitAsync(CancellationToken.None).ConfigureAwait(false);
        _shutdownForRestart = true;
        try
        {
            _orch.ServerLauncher.StopServerProcess();
            var state = _store.LoadOrCreate();
            var p = _orch.ServerLauncher.Start(state);
            WireWatchdog(p);
            await ServerLauncher.WaitForHttpOkAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _log.Error("Restart after repo sync failed", ex);
        }
        finally
        {
            _shutdownForRestart = false;
            try
            {
                _pythonRestartGate.Release();
            }
            catch
            {
                // ignore
            }
        }
    }

    private async Task ForceUpdateAsync()
    {
        void Balloon(string title, string body, ToolTipIcon icon)
        {
            var d = Application.Current?.Dispatcher;
            if (d == null)
                return;
            d.Invoke(() =>
            {
                if (_shutdownRequested || _notifyIcon == null)
                    return;
                try
                {
#pragma warning disable CA1416
                    _notifyIcon.ShowBalloonTip(8000, title, body, icon);
#pragma warning restore CA1416
                }
                catch
                {
                    // ignore
                }
            }, DispatcherPriority.Normal);
        }

        static string TruncateErr(string s, int max)
        {
            if (string.IsNullOrEmpty(s))
                return "?";
            s = s.Trim();
            return s.Length <= max ? s : s[..(max - 1)] + "…";
        }

        try
        {
            Balloon(InstallerStrings.TrayBalloonTitle, InstallerStrings.TrayForceUpdateStartedBody, ToolTipIcon.Info);
            _log.Info("Tray: manual repository sync started");

            _backgroundSyncRunCts?.Cancel();
            _log.Info("Tray: manual sync — cancelling in-flight background sync if any; waiting for repo sync lock…");

            if (!await _repoSyncGate.WaitAsync(TimeSpan.FromSeconds(120)).ConfigureAwait(true))
            {
                _log.Error("Tray: manual sync — repo sync lock not released within 120s after cancel.");
                Balloon(
                    InstallerStrings.TrayBalloonTitle,
                    InstallerStrings.TrayForceUpdateLockWaitTimeoutBody,
                    ToolTipIcon.Warning);
                return;
            }

            try
            {
                using var manualCts = new CancellationTokenSource(TimeSpan.FromMinutes(45));
                try
                {
                    var changed = await _orch.BackgroundSyncRepositoryAsync(manualCts.Token).ConfigureAwait(false);
                    if (changed)
                        await RestartServerForSyncAsync().ConfigureAwait(false);

                    if (changed)
                        Balloon(InstallerStrings.TrayBalloonTitle, InstallerStrings.TrayForceUpdateUpdatedBody, ToolTipIcon.Info);
                    else
                        Balloon(InstallerStrings.TrayBalloonTitle, InstallerStrings.TrayForceUpdateAlreadyCurrentBody, ToolTipIcon.Info);
                }
                catch (OperationCanceledException)
                {
                    _log.Info("Tray: manual repository sync cancelled or timed out.");
                    Balloon(
                        InstallerStrings.TrayBalloonTitle,
                        InstallerStrings.TrayForceUpdateCancelledOrTimeoutBody,
                        ToolTipIcon.Warning);
                }
            }
            finally
            {
                _repoSyncGate.Release();
            }
        }
        catch (Exception ex)
        {
            _log.Error("Force update failed", ex);
            Balloon(
                InstallerStrings.TrayForceUpdateErrorTitle,
                TruncateErr(ex.Message, 220),
                ToolTipIcon.Error);
        }
    }

    private void StartSettingsWatcher()
    {
        try
        {
            var dir = Path.GetDirectoryName(InstallerPaths.DaemonSettingsPath)!;
            var name = Path.GetFileName(InstallerPaths.DaemonSettingsPath);
            Directory.CreateDirectory(dir);
            _settingsWatcher = new FileSystemWatcher(dir, name) { NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size };
            _settingsWatcher.Changed += OnDaemonSettingsChanged;
            _settingsWatcher.EnableRaisingEvents = true;
        }
        catch (Exception ex)
        {
            _log.Info($"Settings watcher: {ex.Message}");
        }
    }

    private System.Timers.Timer? _debounceReload;

    private void OnDaemonSettingsChanged(object sender, FileSystemEventArgs e)
    {
        Application.Current.Dispatcher.BeginInvoke(new Action(() =>
        {
            _debounceReload?.Stop();
            _debounceReload?.Dispose();
            _debounceReload = new System.Timers.Timer(500) { AutoReset = false };
            _debounceReload.Elapsed += (_, _) =>
            {
                Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    ReloadPollIntervalFromDisk();
                    _debounceReload?.Dispose();
                    _debounceReload = null;
                }));
            };
            _debounceReload.Start();
        }));
    }

    private void ReloadPollIntervalFromDisk()
    {
        try
        {
            var s = _daemonSettingsStore.LoadOrCreate();
            Interlocked.Exchange(ref _pollIntervalSeconds, Math.Max(1, s.RepoPollIntervalSeconds));
        }
        catch (Exception ex)
        {
            _log.Info($"Reload settings: {ex.Message}");
        }
    }

    private void ExitFromTray()
    {
        _shutdownRequested = true;
        _backgroundSyncRunCts?.Cancel();
        try
        {
            _repoSyncLoopCts?.Cancel();
        }
        catch
        {
            // ignore
        }

        try
        {
            _repoSyncLoopTask?.Wait(TimeSpan.FromSeconds(5));
        }
        catch
        {
            // ignore
        }

        try
        {
            _repoSyncLoopCts?.Dispose();
        }
        catch
        {
            // ignore
        }

        _repoSyncLoopCts = null;
        _repoSyncLoopTask = null;

        try
        {
            _settingsWatcher?.Dispose();
        }
        catch
        {
            // ignore
        }

        _settingsWatcher = null;
        if (_notifyIcon != null)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _notifyIcon = null;
        }

        _orch.ServerLauncher.StopServerProcess();
        ReleaseSingletonMutex();
        Application.Current.Shutdown(0);
    }

    public void Dispose()
    {
        try
        {
            _repoSyncLoopCts?.Cancel();
        }
        catch
        {
            // ignore
        }

        try
        {
            _repoSyncLoopCts?.Dispose();
        }
        catch
        {
            // ignore
        }

        try
        {
            _settingsWatcher?.Dispose();
        }
        catch
        {
            // ignore
        }

        try
        {
            _notifyIcon?.Dispose();
        }
        catch
        {
            // ignore
        }

        _repoSyncGate.Dispose();
        _pythonRestartGate.Dispose();
    }
}
