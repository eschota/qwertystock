using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Text;

namespace QwertyStock.Bootstrapper;

public sealed class SelfUpdateService
{
    /// <summary>Окно после старта загрузки: если меньше байт — маршрут считаем мёртвым, пробуем прокси из каталога.</summary>
    private const int SlowTransferWindowSeconds = 15;

    private const long MinBytesInSlowWindow = 64 * 1024;

    private readonly InstallerLogger _log;

    public SelfUpdateService(InstallerLogger log)
    {
        _log = log;
    }

    /// <summary>
    /// If a newer build is published, downloads it and restarts the process. Returns only when no update was applied.
    /// </summary>
    public async Task ApplyIfNewerAsync(InstallerManifest manifest, IProgress<TransferProgress>? downloadProgress, CancellationToken ct)
    {
        var current = AppVersion.Semantic;
        if (!SemverHelper.IsNewer(manifest.Version.Trim(), current))
        {
            _log.Info($"Self-update: current {current}, manifest {manifest.Version} — no update.");
            return;
        }

        _log.Info($"Self-update: downloading {manifest.Url}");
        var exePath = Environment.ProcessPath;
        if (string.IsNullOrEmpty(exePath))
            throw new InvalidOperationException("Cannot determine current executable path.");
        var dir = Path.GetDirectoryName(exePath)!;
        var name = Path.GetFileName(exePath);
        var staged = Path.Combine(dir, Path.GetFileNameWithoutExtension(name) + ".pending" + Path.GetExtension(name));

        var effectiveProgress = MergeDownloadProgress(downloadProgress);
        await DownloadWithProxyRoutesAsync(manifest, staged, effectiveProgress, ct).ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(manifest.Sha256))
            HttpHash.VerifyFileSha256Hex(staged, manifest.Sha256);

        var batch = Path.Combine(Path.GetTempPath(), "qwertystock-selfupdate-" + Guid.NewGuid().ToString("N") + ".cmd");
        var restartArgs = FormatArgsForCmdRestart(Environment.GetCommandLineArgs());
        // Single-file exe + AV can keep a lock briefly after Exit(0). Retry move instead of one 2s wait.
        var lines = new[]
        {
            "@echo off",
            "setlocal",
            "set \"LOG=%LOCALAPPDATA%\\QwertyStock\\logs\\installer.log\"",
            "set retries=0",
            ":retry",
            "timeout /t 2 /nobreak >nul",
            $"move /y \"{staged}\" \"{exePath}\"",
            "if not errorlevel 1 goto ok",
            "set /a retries+=1",
            "if %retries% lss 12 goto retry",
            "echo Self-update: move failed after retries (see installer.log) >> \"%LOG%\"",
            $"if exist \"{staged}\" del /f /q \"{staged}\" 2>nul",
            "exit /b 1",
            ":ok",
            $"start \"\" \"{exePath}\"{restartArgs}",
            "del \"%~f0\"",
        };
        await File.WriteAllLinesAsync(batch, lines, ct).ConfigureAwait(false);

        _log.Info($"Self-update: newer build {manifest.Version.Trim()} installed — restarting process (same command line).");

        Process.Start(new ProcessStartInfo
        {
            FileName = batch,
            UseShellExecute = true,
            WorkingDirectory = dir,
        });

        Environment.Exit(0);
    }

    /// <summary>Фоновый трей передаёт <c>null</c> — пишем прогресс в лог (раз в ~2 MiB или ~20 с), иначе «тишина» при долгом скачивании.</summary>
    private IProgress<TransferProgress>? MergeDownloadProgress(IProgress<TransferProgress>? ui)
    {
        if (ui != null)
            return ui;
        long bytesAtLastLog = -1;
        var sinceLastLog = Stopwatch.StartNew();
        return new Progress<TransferProgress>(tp =>
        {
            var received = tp.BytesReceived;
            var total = tp.TotalBytes;
            var done = total is long t && received >= t;
            var first = bytesAtLastLog < 0 && received > 0;
            var step2MiB = received - Math.Max(0, bytesAtLastLog) >= 2L * 1024 * 1024;
            // Пульс: даже если байты не растут (сеть «висит»), раз в ~5 с видно состояние.
            var heartbeat = sinceLastLog.ElapsedMilliseconds >= 5_000 && received > 0;
            if (!done && !first && !step2MiB && !heartbeat)
                return;
            bytesAtLastLog = received;
            sinceLastLog.Restart();
            var pct = total is > 0 ? 100.0 * received / total.Value : 0;
            var stats = total is > 0
                ? $"{TransferProgressFormatter.FormatBytesOfTotal(received, total)} ({pct:F1}%) — {TransferProgressFormatter.FormatSpeed(tp.BytesPerSecond)} ETA {TransferProgressFormatter.FormatEta(tp.EtaSeconds)}"
                : $"{TransferProgressFormatter.FormatBytes(received)} — {TransferProgressFormatter.FormatSpeed(tp.BytesPerSecond)}";
            _log.Info($"Self-update: {stats}");
        });
    }

    /// <summary>
    /// Короткий probe к манифесту уже прошёл (EnsureAsync), но большой exe может идти с нулевой скоростью.
    /// Перебираем тот же каталог прокси, что и инсталлер; при успехе через другой маршрут — обновляем ProxySession + InstallerHttp для git.
    /// </summary>
    private async Task DownloadWithProxyRoutesAsync(
        InstallerManifest manifest,
        string staged,
        IProgress<TransferProgress>? downloadProgress,
        CancellationToken ct)
    {
        var routes = BuildSelfUpdateDownloadRoutes();
        for (var i = 0; i < routes.Count; i++)
        {
            var route = routes[i];
            _log.Info($"Self-update: route {i + 1}/{routes.Count} — {route.Label}");
            var ok = await TryDownloadRouteWithRetriesAsync(route, manifest, staged, downloadProgress, ct).ConfigureAwait(false);
            if (!ok)
                continue;

            AlignSessionWithSuccessfulRoute(route);
            return;
        }

        throw new InvalidOperationException("Self-update: не удалось скачать exe ни по одному маршруту (прямой + прокси из каталога).");
    }

    private readonly record struct DownloadRoute(
        string Label,
        IWebProxy? Proxy,
        /// <summary>null = прямой доступ; иначе URI для <see cref="ProxySession.SetProxy"/>.</summary>
        string? SessionUri,
        bool IsHttpProxy);

    private static List<DownloadRoute> BuildSelfUpdateDownloadRoutes()
    {
        var catalog = ProxyCatalog.Load();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var list = new List<DownloadRoute>();

        void TryAdd(string dedupeKey, DownloadRoute route)
        {
            if (!seen.Add(dedupeKey))
                return;
            list.Add(route);
        }

        if (ProxySession.IsDirect)
            TryAdd("direct", new DownloadRoute("прямое подключение", null, null, false));
        else if (!string.IsNullOrEmpty(ProxySession.ActiveProxyUri)
                 && Uri.TryCreate(ProxySession.ActiveProxyUri, UriKind.Absolute, out var curU))
        {
            TryAdd(
                "cur:" + ProxySession.ActiveProxyUri,
                new DownloadRoute(
                    $"текущий ({ProxySession.ActiveProxyUri})",
                    new WebProxy(curU),
                    ProxySession.ActiveProxyUri,
                    ProxySession.IsHttpProxy));
        }

        foreach (var s in catalog.Http)
        {
            if (!Uri.TryCreate(s, UriKind.Absolute, out var u))
                continue;
            TryAdd("http:" + u.GetLeftPart(UriPartial.Authority), new DownloadRoute($"HTTP {s}", new WebProxy(u), s, true));
        }

        foreach (var s in catalog.Socks5)
        {
            if (!Uri.TryCreate(s, UriKind.Absolute, out var u))
                continue;
            TryAdd("socks:" + u.GetLeftPart(UriPartial.Authority), new DownloadRoute($"SOCKS5 {s}", new WebProxy(u), s, false));
        }

        return list;
    }

    private static void AlignSessionWithSuccessfulRoute(DownloadRoute route)
    {
        if (route.SessionUri == null)
        {
            if (!ProxySession.IsDirect)
            {
                ProxySession.SetDirect();
                InstallerHttp.ReplaceWithProxy(null);
            }

            return;
        }

        if (!ProxySession.IsDirect
            && string.Equals(ProxySession.ActiveProxyUri, route.SessionUri, StringComparison.OrdinalIgnoreCase))
            return;

        ProxySession.SetProxy(route.SessionUri, route.IsHttpProxy);
        InstallerHttp.ReplaceWithProxy(new WebProxy(new Uri(route.SessionUri)));
    }

    private async Task<bool> TryDownloadRouteWithRetriesAsync(
        DownloadRoute route,
        InstallerManifest manifest,
        string staged,
        IProgress<TransferProgress>? downloadProgress,
        CancellationToken ct)
    {
        const int maxAttempts = 3;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                TryDeleteStaged(staged);
                using var http = InstallerHttp.CreateLargeBinaryDownloadClient(route.Proxy);
                await DownloadWithSlowTransferWatchdogAsync(http, manifest.Url, staged, downloadProgress, ct).ConfigureAwait(false);
                return true;
            }
            catch (OperationCanceledException)
            {
                if (ct.IsCancellationRequested)
                    throw;
                _log.Info($"Self-update: на маршруте «{route.Label}» скорость почти нулевая (< {MinBytesInSlowWindow} B за {SlowTransferWindowSeconds} с) — следующий маршрут.");
                return false;
            }
            catch (Exception ex)
            {
                if (attempt >= maxAttempts)
                {
                    _log.Info($"Self-update: маршрут «{route.Label}» — попытка {attempt}/{maxAttempts} не удалась ({ex.Message}).");
                    return false;
                }

                _log.Info($"Self-update: маршрут «{route.Label}» — попытка {attempt}/{maxAttempts} ({ex.Message}), повтор…");
                await Task.Delay(TimeSpan.FromSeconds(4 * attempt), ct).ConfigureAwait(false);
            }
        }

        return false;
    }

    private static void TryDeleteStaged(string staged)
    {
        if (!File.Exists(staged))
            return;
        try
        {
            File.Delete(staged);
        }
        catch
        {
            // ignore
        }
    }

    /// <summary>Отмена загрузки, если за окно получено слишком мало данных (как при «живом» probe и мёртвом потоке).</summary>
    private async Task DownloadWithSlowTransferWatchdogAsync(
        HttpClient http,
        string url,
        string path,
        IProgress<TransferProgress>? progress,
        CancellationToken ct)
    {
        using var slowCts = new CancellationTokenSource();
        using var downloadDone = new CancellationTokenSource();
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, slowCts.Token);
        var received = 0L;
        var wrapped = new Progress<TransferProgress>(tp =>
        {
            Interlocked.Exchange(ref received, tp.BytesReceived);
            progress?.Report(tp);
        });

        var watchdog = Task.Run(
            async () =>
            {
                try
                {
                    using var w = CancellationTokenSource.CreateLinkedTokenSource(ct, downloadDone.Token);
                    await Task.Delay(TimeSpan.FromSeconds(SlowTransferWindowSeconds), w.Token).ConfigureAwait(false);
                    if (Volatile.Read(ref received) < MinBytesInSlowWindow)
                        slowCts.Cancel();
                }
                catch (OperationCanceledException)
                {
                    // отмена ct, завершение загрузки или нормальный таймер
                }
            },
            ct);

        try
        {
            await HttpDownload.DownloadLargeBinaryToFileAsync(http, url, path, wrapped, linked.Token).ConfigureAwait(false);
        }
        finally
        {
            downloadDone.Cancel();
            slowCts.Cancel();
            try
            {
                await watchdog.ConfigureAwait(false);
            }
            catch
            {
                // ignore
            }
        }
    }

    /// <summary>Аргументы после пути к exe для строки <c>start "" "exe" …</c> в cmd.</summary>
    internal static string FormatArgsForCmdRestart(string[] argv)
    {
        if (argv.Length <= 1)
            return "";

        var sb = new StringBuilder();
        for (var i = 1; i < argv.Length; i++)
        {
            sb.Append(' ');
            var a = argv[i];
            if (a.Length == 0)
            {
                sb.Append("\"\"");
                continue;
            }

            if (a.IndexOfAny([' ', '\t', '"']) >= 0)
            {
                sb.Append('"');
                sb.Append(a.Replace("\"", "\\\"", StringComparison.Ordinal));
                sb.Append('"');
            }
            else
            {
                sb.Append(a);
            }
        }

        return sb.ToString();
    }
}
