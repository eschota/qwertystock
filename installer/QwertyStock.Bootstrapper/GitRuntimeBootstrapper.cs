using System.IO.Compression;

namespace QwertyStock.Bootstrapper;

public sealed class GitRuntimeBootstrapper
{
    private readonly InstallerLogger _log;

    public GitRuntimeBootstrapper(InstallerLogger log)
    {
        _log = log;
    }

    public async Task EnsureAsync(
        InstallerState state,
        IProgress<TransferProgress>? gitZipProgress,
        InstallerManifest manifest,
        CancellationToken ct)
    {
        Directory.CreateDirectory(InstallerPaths.RuntimeDir);
        var http = InstallerHttp.Client;

        var needDownload = !File.Exists(InstallerPaths.GitExe)
                           || state.MinGitVersion != InstallerPaths.MinGitVersion;

        if (!needDownload)
            return;

        _log.Info("Downloading MinGit…");
        var zipUrl = RuntimeAssetUrls.MinGitZipUrl(manifest);
        var expectedSha = RuntimeAssetUrls.RequireMinGitZipSha256(zipUrl, manifest);
        var zipPath = Path.Combine(Path.GetTempPath(), "mingit-" + Guid.NewGuid().ToString("N") + ".zip");
        await HttpDownload.DownloadToFileAsync(http, zipUrl, zipPath, gitZipProgress, ct).ConfigureAwait(false);
        HttpHash.VerifyFileSha256Hex(zipPath, expectedSha);

        if (Directory.Exists(InstallerPaths.GitDir))
            Directory.Delete(InstallerPaths.GitDir, true);
        Directory.CreateDirectory(InstallerPaths.GitDir);
        ZipFile.ExtractToDirectory(zipPath, InstallerPaths.GitDir);
        File.Delete(zipPath);

        if (!File.Exists(InstallerPaths.GitExe))
            throw new InvalidOperationException("MinGit layout unexpected: git.exe not found under cmd\\.");

        state.MinGitVersion = InstallerPaths.MinGitVersion;
    }
}
