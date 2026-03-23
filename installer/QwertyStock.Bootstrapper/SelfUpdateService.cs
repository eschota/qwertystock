using System.Diagnostics;
using System.Net.Http;
using System.Text;

namespace QwertyStock.Bootstrapper;

public sealed class SelfUpdateService
{
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

        var http = InstallerHttp.Client;
        var effectiveProgress = MergeDownloadProgress(downloadProgress);
        await DownloadWithRetriesAsync(http, manifest, staged, effectiveProgress, ct).ConfigureAwait(false);

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
            // Пульс: даже если байты не растут (сеть «висит»), раз в ~20 с видно состояние.
            var heartbeat = sinceLastLog.ElapsedMilliseconds >= 20_000 && received > 0;
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

    private async Task DownloadWithRetriesAsync(
        HttpClient http,
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
                if (File.Exists(staged))
                {
                    try
                    {
                        File.Delete(staged);
                    }
                    catch
                    {
                        // ignore
                    }
                }

                await HttpDownload.DownloadToFileAsync(http, manifest.Url, staged, downloadProgress, ct).ConfigureAwait(false);
                return;
            }
            catch (Exception ex)
            {
                if (attempt >= maxAttempts)
                    throw;

                _log.Info($"Self-update: download attempt {attempt}/{maxAttempts} failed ({ex.Message}); retrying…");
                await Task.Delay(TimeSpan.FromSeconds(4 * attempt), ct).ConfigureAwait(false);
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
