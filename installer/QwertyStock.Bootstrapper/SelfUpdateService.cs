using System.Diagnostics;

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
        await HttpDownload.DownloadToFileAsync(http, manifest.Url, staged, downloadProgress, ct).ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(manifest.Sha256))
            HttpHash.VerifyFileSha256Hex(staged, manifest.Sha256);

        var batch = Path.Combine(Path.GetTempPath(), "qwertystock-selfupdate-" + Guid.NewGuid().ToString("N") + ".cmd");
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
            $"start \"\" \"{exePath}\"",
            "del \"%~f0\"",
        };
        await File.WriteAllLinesAsync(batch, lines, ct).ConfigureAwait(false);

        Process.Start(new ProcessStartInfo
        {
            FileName = batch,
            UseShellExecute = true,
            WorkingDirectory = dir,
        });

        Environment.Exit(0);
    }
}
