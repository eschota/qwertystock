using System.Text.RegularExpressions;

namespace QwertyStock.Bootstrapper;

public sealed class RepositorySyncService
{
    private static readonly Regex GitPhasePercent = new(
        @"(Receiving objects|Resolving deltas|Writing objects|Compressing objects):\s+(\d+)%",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly InstallerLogger _log;

    public RepositorySyncService(InstallerLogger log)
    {
        _log = log;
    }

    public async Task SyncAsync(string branch, string repoRemoteUrl, IProgress<InstallProgress>? ui, CancellationToken ct)
    {
        var git = InstallerPaths.GitExe;
        var pfx = ProxySession.GitProxyPrefix();
        var env = ProcessRunner.Merge(GitEnvironment.PathWithGit(), ProxySession.GetProcessEnvironment());

        if (Directory.Exists(Path.Combine(InstallerPaths.RepoDir, ".git")))
        {
            _log.Info($"git: set origin → {repoRemoteUrl}");
            var setUrl = await ProcessRunner.RunAsync(
                    git,
                    $"{pfx}remote set-url origin \"{repoRemoteUrl}\"",
                    InstallerPaths.RepoDir,
                    env,
                    ct)
                .ConfigureAwait(false);
            if (setUrl.ExitCode != 0)
                throw new InvalidOperationException($"git remote set-url failed ({setUrl.ExitCode}): {setUrl.StdErr}");
        }

        if (!Directory.Exists(Path.Combine(InstallerPaths.RepoDir, ".git")))
        {
            if (Directory.Exists(InstallerPaths.RepoDir))
            {
                _log.Info("Removing incomplete repo directory…");
                Directory.Delete(InstallerPaths.RepoDir, true);
            }

            Directory.CreateDirectory(InstallerPaths.Root);
            var args = $"{pfx}clone --progress \"{repoRemoteUrl}\" \"{InstallerPaths.RepoDir}\"";
            var exit = await ProcessRunner.RunWithStderrLinesAsync(
                git,
                args,
                InstallerPaths.Root,
                env,
                line =>
                {
                    _log.Info($"git: {line}");
                    ReportRepoLine(ui, line);
                },
                ct).ConfigureAwait(false);
            if (exit != 0)
                throw new InvalidOperationException($"git clone failed ({exit}). See installer.log.");
            ReportRepo(ui, ProgressSegments.RepoEnd, InstallerStrings.ProgressRepo, line: null);
            return;
        }

        ReportRepo(ui, ProgressSegments.RepoStart + (ProgressSegments.RepoEnd - ProgressSegments.RepoStart) * 0.15,
            InstallerStrings.ProgressRepo, "git fetch…");
        var fetch = await ProcessRunner.RunAsync(git, $"{pfx}fetch origin", InstallerPaths.RepoDir, env, ct)
            .ConfigureAwait(false);
        if (fetch.ExitCode != 0)
        {
            _log.Error($"git fetch failed: {fetch.StdErr}");
            await ResetRepoAndCloneAsync(branch, repoRemoteUrl, ui, ct).ConfigureAwait(false);
            return;
        }

        ReportRepo(ui, ProgressSegments.RepoStart + (ProgressSegments.RepoEnd - ProgressSegments.RepoStart) * 0.45,
            InstallerStrings.ProgressRepo, "git reset…");
        var reset = await ProcessRunner.RunAsync(
                git,
                $"{pfx}reset --hard origin/{branch}",
                InstallerPaths.RepoDir,
                env,
                ct)
            .ConfigureAwait(false);
        if (reset.ExitCode != 0)
        {
            _log.Error($"git reset failed: {reset.StdErr}");
            await ResetRepoAndCloneAsync(branch, repoRemoteUrl, ui, ct).ConfigureAwait(false);
            return;
        }

        ReportRepo(ui, ProgressSegments.RepoStart + (ProgressSegments.RepoEnd - ProgressSegments.RepoStart) * 0.75,
            InstallerStrings.ProgressRepo, "git clean…");
        var clean = await ProcessRunner.RunAsync(git, $"{pfx}clean -fd", InstallerPaths.RepoDir, env, ct)
            .ConfigureAwait(false);
        if (clean.ExitCode != 0)
            throw new InvalidOperationException($"git clean failed ({clean.ExitCode}): {clean.StdErr}");

        ReportRepo(ui, ProgressSegments.RepoEnd, InstallerStrings.ProgressRepo, null);
    }

    private static void ReportRepoLine(IProgress<InstallProgress>? ui, string line)
    {
        if (ui == null)
            return;
        var m = GitPhasePercent.Match(line);
        if (!m.Success || !int.TryParse(m.Groups[2].Value, out var pct))
            return;
        pct = Math.Clamp(pct, 0, 100);
        var overall = ProgressSegments.RepoStart + (ProgressSegments.RepoEnd - ProgressSegments.RepoStart) * (pct / 100.0);
        ui.Report(new InstallProgress(
            overall,
            InstallerStrings.ProgressRepo,
            Indeterminate: false,
            Detail: line.Trim(),
            SpeedText: "—",
            EtaText: "—",
            BytesText: null));
    }

    private static void ReportRepo(IProgress<InstallProgress>? ui, double pct, string message, string? line)
    {
        ui?.Report(new InstallProgress(
            pct,
            message,
            Indeterminate: false,
            Detail: line,
            SpeedText: "—",
            EtaText: "—",
            BytesText: null));
    }

    private async Task ResetRepoAndCloneAsync(string branch, string repoRemoteUrl, IProgress<InstallProgress>? ui, CancellationToken ct)
    {
        _log.Info("Deleting repo and cloning fresh…");
        if (Directory.Exists(InstallerPaths.RepoDir))
            Directory.Delete(InstallerPaths.RepoDir, true);
        await SyncAsync(branch, repoRemoteUrl, ui, ct).ConfigureAwait(false);
    }
}
