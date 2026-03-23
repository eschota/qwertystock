namespace QwertyStock.Bootstrapper;

public sealed class RepositorySyncService
{
    private readonly InstallerLogger _log;

    public RepositorySyncService(InstallerLogger log)
    {
        _log = log;
    }

    public async Task SyncAsync(string branch, CancellationToken ct)
    {
        var git = InstallerPaths.GitExe;
        var env = GitEnvironment.PathWithGit();

        if (!Directory.Exists(Path.Combine(InstallerPaths.RepoDir, ".git")))
        {
            if (Directory.Exists(InstallerPaths.RepoDir))
            {
                _log.Info("Removing incomplete repo directory…");
                Directory.Delete(InstallerPaths.RepoDir, true);
            }

            Directory.CreateDirectory(InstallerPaths.Root);
            var args = $"clone \"{InstallerPaths.RepoRemoteUrl}\" \"{InstallerPaths.RepoDir}\"";
            var r = await ProcessRunner.RunAsync(git, args, InstallerPaths.Root, env, ct).ConfigureAwait(false);
            if (r.ExitCode != 0)
                throw new InvalidOperationException($"git clone failed ({r.ExitCode}): {r.StdErr}");
            return;
        }

        var fetch = await ProcessRunner.RunAsync(git, "fetch origin", InstallerPaths.RepoDir, env, ct)
            .ConfigureAwait(false);
        if (fetch.ExitCode != 0)
        {
            _log.Error($"git fetch failed: {fetch.StdErr}");
            await ResetRepoAndCloneAsync(branch, ct).ConfigureAwait(false);
            return;
        }

        var reset = await ProcessRunner.RunAsync(
                git,
                $"reset --hard origin/{branch}",
                InstallerPaths.RepoDir,
                env,
                ct)
            .ConfigureAwait(false);
        if (reset.ExitCode != 0)
        {
            _log.Error($"git reset failed: {reset.StdErr}");
            await ResetRepoAndCloneAsync(branch, ct).ConfigureAwait(false);
            return;
        }

        var clean = await ProcessRunner.RunAsync(git, "clean -fd", InstallerPaths.RepoDir, env, ct)
            .ConfigureAwait(false);
        if (clean.ExitCode != 0)
            throw new InvalidOperationException($"git clean failed ({clean.ExitCode}): {clean.StdErr}");
    }

    private async Task ResetRepoAndCloneAsync(string branch, CancellationToken ct)
    {
        _log.Info("Deleting repo and cloning fresh…");
        if (Directory.Exists(InstallerPaths.RepoDir))
            Directory.Delete(InstallerPaths.RepoDir, true);
        await SyncAsync(branch, ct).ConfigureAwait(false);
    }
}
