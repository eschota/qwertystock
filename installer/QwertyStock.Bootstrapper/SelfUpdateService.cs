using System.Diagnostics;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;

namespace QwertyStock.Bootstrapper;

public sealed class SelfUpdateService
{
    private readonly InstallerLogger _log;
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(5) };

    public SelfUpdateService(InstallerLogger log)
    {
        _log = log;
    }

    /// <summary>
    /// If a newer build is published, downloads it and restarts the process. Returns only when no update was applied.
    /// </summary>
    public async Task ApplyIfNewerAsync(string manifestUrl, CancellationToken ct)
    {
        using var response = await Http.GetAsync(manifestUrl, HttpCompletionOption.ResponseHeadersRead, ct)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        var manifest = await JsonSerializer.DeserializeAsync<UpdateManifest>(stream,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }, ct).ConfigureAwait(false);
        if (manifest == null || string.IsNullOrWhiteSpace(manifest.Version) || string.IsNullOrWhiteSpace(manifest.Url))
            throw new InvalidOperationException("Update manifest is missing version or url.");

        var asm = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(1, 0, 0, 0);
        var current = $"{asm.Major}.{asm.Minor}.{asm.Build}";
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

        await using (var dl = await Http.GetStreamAsync(manifest.Url, ct).ConfigureAwait(false))
        await using (var fs = new FileStream(staged, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await dl.CopyToAsync(fs, ct).ConfigureAwait(false);
        }

        if (!string.IsNullOrWhiteSpace(manifest.Sha256))
        {
            await using var fs = new FileStream(staged, FileMode.Open, FileAccess.Read, FileShare.Read);
            var hex = HttpHash.Sha256Hex(fs);
            if (!HttpHash.EqualsHex(hex, manifest.Sha256))
            {
                File.Delete(staged);
                throw new InvalidOperationException("Downloaded installer failed SHA256 verification.");
            }
        }

        var batch = Path.Combine(Path.GetTempPath(), "qwertystock-selfupdate-" + Guid.NewGuid().ToString("N") + ".cmd");
        var lines = new[]
        {
            "@echo off",
            "timeout /t 2 /nobreak >nul",
            $"move /y \"{staged}\" \"{exePath}\"",
            $"start \"\" \"{exePath}\"",
            $"del \"{batch}\"",
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
