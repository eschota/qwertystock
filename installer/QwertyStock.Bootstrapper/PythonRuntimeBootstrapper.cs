using System.IO.Compression;

namespace QwertyStock.Bootstrapper;

public sealed class PythonRuntimeBootstrapper
{
    private readonly InstallerLogger _log;

    public PythonRuntimeBootstrapper(InstallerLogger log)
    {
        _log = log;
    }

    public async Task EnsureAsync(
        InstallerState state,
        IProgress<TransferProgress>? pythonZipProgress,
        InstallerManifest manifest,
        CancellationToken ct)
    {
        Directory.CreateDirectory(InstallerPaths.RuntimeDir);
        var http = InstallerHttp.Client;

        var needDownload = !File.Exists(InstallerPaths.PythonExe)
                           || state.PythonEmbedVersion != InstallerPaths.PythonEmbedVersion;

        if (needDownload)
        {
            _log.Info("Downloading embeddable Python runtime…");
            var zipUrl = RuntimeAssetUrls.PythonZipUrl(manifest);
            var expectedSha = RuntimeAssetUrls.RequirePythonZipSha256(zipUrl, manifest);
            var zipPath = Path.Combine(Path.GetTempPath(), "python-embed-" + Guid.NewGuid().ToString("N") + ".zip");
            await HttpDownload.DownloadToFileAsync(http, zipUrl, zipPath, pythonZipProgress, ct)
                .ConfigureAwait(false);
            HttpHash.VerifyFileSha256Hex(zipPath, expectedSha);

            if (Directory.Exists(InstallerPaths.PythonRuntimeDir))
                Directory.Delete(InstallerPaths.PythonRuntimeDir, true);
            Directory.CreateDirectory(InstallerPaths.PythonRuntimeDir);
            ZipFile.ExtractToDirectory(zipPath, InstallerPaths.PythonRuntimeDir);
            File.Delete(zipPath);
            state.PythonEmbedVersion = InstallerPaths.PythonEmbedVersion;
        }

        EnableSitePackages();
        await EnsurePipAsync(manifest, ct).ConfigureAwait(false);
    }

    private static void EnableSitePackages()
    {
        foreach (var pth in Directory.GetFiles(InstallerPaths.PythonRuntimeDir, "python*._pth"))
        {
            var lines = File.ReadAllLines(pth)
                .Where(l =>
                {
                    var t = l.Trim();
                    return t is not ("import site" or "#import site");
                })
                .Append("import site")
                .ToArray();
            File.WriteAllLines(pth, lines);
        }
    }

    private async Task EnsurePipAsync(InstallerManifest manifest, CancellationToken ct)
    {
        var proxyEnv = ProxySession.GetProcessEnvironment();
        var check = await ProcessRunner.RunAsync(
            InstallerPaths.PythonExe,
            "-m pip --version",
            InstallerPaths.PythonRuntimeDir,
            proxyEnv.Count > 0 ? proxyEnv : null,
            ct).ConfigureAwait(false);
        if (check.ExitCode == 0)
            return;

        _log.Info("Installing pip…");
        var http = InstallerHttp.Client;
        var getPipUrl = RuntimeAssetUrls.GetPipUrl(manifest);
        var expectedSha = RuntimeAssetUrls.RequireGetPipSha256(getPipUrl, manifest);
        var getPip = Path.Combine(Path.GetTempPath(), "get-pip-" + Guid.NewGuid().ToString("N") + ".py");
        await HttpDownload.DownloadToFileAsync(http, getPipUrl, getPip, null, ct)
            .ConfigureAwait(false);
        HttpHash.VerifyFileSha256Hex(getPip, expectedSha);

        var install = await ProcessRunner.RunAsync(
            InstallerPaths.PythonExe,
            $"\"{getPip}\"",
            InstallerPaths.PythonRuntimeDir,
            proxyEnv.Count > 0 ? proxyEnv : null,
            ct).ConfigureAwait(false);
        try
        {
            File.Delete(getPip);
        }
        catch
        {
            // ignore
        }

        if (install.ExitCode != 0)
            throw new InvalidOperationException(
                $"pip bootstrap failed (exit {install.ExitCode}). stderr: {install.StdErr}");
    }
}
