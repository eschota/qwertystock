namespace QwertyStock.Bootstrapper;

internal static class GitRepositoryHelper
{
    public static async Task<string?> ReadHeadAsync(CancellationToken ct)
    {
        var gitDir = Path.Combine(InstallerPaths.RepoDir, ".git");
        if (!Directory.Exists(gitDir))
            return null;

        var env = GitEnvironment.PathWithGit();
        // Без таймаута rev-parse мог зависнуть (блокировка .git на Windows) и держать _syncGate вечно → минутные «skipped».
        var (code, stdout, _) = await ProcessRunner.RunAsync(
                InstallerPaths.GitExe,
                "rev-parse HEAD",
                InstallerPaths.RepoDir,
                env,
                ct,
                TimeSpan.FromSeconds(60))
            .ConfigureAwait(false);
        if (code != 0)
            return null;
        var t = stdout.Trim();
        return t.Length > 0 ? t : null;
    }
}
