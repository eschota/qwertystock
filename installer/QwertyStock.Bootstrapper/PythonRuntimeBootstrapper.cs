using System.IO.Compression;

namespace QwertyStock.Bootstrapper;

public sealed class PythonRuntimeBootstrapper
{
    private readonly InstallerLogger _log;

    public PythonRuntimeBootstrapper(InstallerLogger log)
    {
        _log = log;
    }

    public async Task EnsureAsync(InstallerState state, CancellationToken ct)
    {
        Directory.CreateDirectory(InstallerPaths.RuntimeDir);
        var http = InstallerHttp.Client;

        var needDownload = !File.Exists(InstallerPaths.PythonExe)
                           || state.PythonEmbedVersion != InstallerPaths.PythonEmbedVersion;

        if (needDownload)
        {
            _log.Info("Downloading embeddable Python runtime…");
            var zipPath = Path.Combine(Path.GetTempPath(), "python-embed-" + Guid.NewGuid().ToString("N") + ".zip");
            await HttpDownload.DownloadToFileAsync(http, InstallerPaths.PythonEmbedZipUrl, zipPath, ct)
                .ConfigureAwait(false);

            if (Directory.Exists(InstallerPaths.PythonRuntimeDir))
                Directory.Delete(InstallerPaths.PythonRuntimeDir, true);
            Directory.CreateDirectory(InstallerPaths.PythonRuntimeDir);
            ZipFile.ExtractToDirectory(zipPath, InstallerPaths.PythonRuntimeDir);
            File.Delete(zipPath);
            state.PythonEmbedVersion = InstallerPaths.PythonEmbedVersion;
        }

        EnableSitePackages();
        await EnsurePipAsync(ct).ConfigureAwait(false);
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

    private async Task EnsurePipAsync(CancellationToken ct)
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
        var getPip = Path.Combine(Path.GetTempPath(), "get-pip-" + Guid.NewGuid().ToString("N") + ".py");
        await HttpDownload.DownloadToFileAsync(http, new Uri("https://bootstrap.pypa.io/get-pip.py"), getPip, ct)
            .ConfigureAwait(false);

        var install = await ProcessRunner.RunAsync(
            InstallerPaths.PythonExe,
            $"\"{getPip}\"",
            InstallerPaths.PythonRuntimeDir,
            proxyEnv.Count > 0 ? proxyEnv : null,
            ct).ConfigureAwait(false);
        try { File.Delete(getPip); } catch { /* ignore */ }

        if (install.ExitCode != 0)
            throw new InvalidOperationException(
                $"pip bootstrap failed (exit {install.ExitCode}). stderr: {install.StdErr}");
    }
}
