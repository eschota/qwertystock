using System.Diagnostics;
using System.Drawing;
using System.Timers;
using System.Windows;
using System.Windows.Forms;
using Application = System.Windows.Application;

namespace QwertyStock.Bootstrapper;

/// <summary>Tray icon, Python watchdog, background git/pip sync.</summary>
public sealed class TrayDaemonHost : IDisposable
{
    private readonly InstallerLogger _log;
    private readonly InstallerOrchestrator _orch;
    private readonly InstallerStateStore _store = new();
    private readonly SemaphoreSlim _syncGate = new(1, 1);

    private Mutex? _singletonMutex;
    private NotifyIcon? _notifyIcon;
    private System.Timers.Timer? _pollTimer;
    private FileSystemWatcher? _settingsWatcher;
    private readonly DaemonSettingsStore _daemonSettingsStore = new();
    private volatile bool _shutdownRequested;
    private volatile bool _shutdownForRestart;

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
            CreateNotifyIcon();
            EnsureWatchdog();
            StartPollTimer(settings.RepoPollIntervalSeconds);
            StartSettingsWatcher();
            return true;
        }
        catch
        {
            ReleaseSingletonMutex();
            throw;
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
            Text = InstallerStrings.TrayTooltip,
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
        if (!await _syncGate.WaitAsync(TimeSpan.FromSeconds(30)).ConfigureAwait(true))
            return;
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
            _syncGate.Release();
        }
    }

    private void StartPollTimer(int intervalSeconds)
    {
        _pollTimer?.Stop();
        _pollTimer?.Dispose();
        var ms = Math.Max(1000, intervalSeconds * 1000);
        _pollTimer = new System.Timers.Timer(ms) { AutoReset = true };
        _pollTimer.Elapsed += OnPollElapsed;
        _pollTimer.Start();
    }

    private void OnPollElapsed(object? sender, ElapsedEventArgs e)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                if (_shutdownRequested)
                    return;
                if (!await _syncGate.WaitAsync(0).ConfigureAwait(false))
                    return;
                try
                {
                    var changed = await _orch.BackgroundSyncRepositoryAsync(CancellationToken.None).ConfigureAwait(false);
                    if (changed)
                        await RestartServerForSyncAsync().ConfigureAwait(false);
                }
                finally
                {
                    _syncGate.Release();
                }
            }
            catch (Exception ex)
            {
                _log.Error("Background repo sync failed", ex);
            }
        });
    }

    private async Task RestartServerForSyncAsync()
    {
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
        }
    }

    private async Task ForceUpdateAsync()
    {
        try
        {
            await _syncGate.WaitAsync().ConfigureAwait(true);
            try
            {
                var changed = await _orch.BackgroundSyncRepositoryAsync(CancellationToken.None).ConfigureAwait(false);
                if (changed)
                    await RestartServerForSyncAsync().ConfigureAwait(false);
            }
            finally
            {
                _syncGate.Release();
            }
        }
        catch (Exception ex)
        {
            _log.Error("Force update failed", ex);
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
            StartPollTimer(s.RepoPollIntervalSeconds);
        }
        catch (Exception ex)
        {
            _log.Info($"Reload settings: {ex.Message}");
        }
    }

    private void ExitFromTray()
    {
        _shutdownRequested = true;
        _pollTimer?.Stop();
        _pollTimer?.Dispose();
        _pollTimer = null;
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
            _pollTimer?.Dispose();
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

        _syncGate.Dispose();
    }
}
