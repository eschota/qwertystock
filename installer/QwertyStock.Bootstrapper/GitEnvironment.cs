namespace QwertyStock.Bootstrapper;

public static class GitEnvironment
{
    public static IReadOnlyDictionary<string, string> PathWithGit()
    {
        var path = Environment.GetEnvironmentVariable("PATH") ?? "";
        var prepend = new[]
        {
            Path.Combine(InstallerPaths.GitDir, "cmd"),
            Path.Combine(InstallerPaths.GitDir, "mingw64", "bin"),
            Path.Combine(InstallerPaths.GitDir, "usr", "bin"),
        };
        foreach (var p in prepend.Where(Directory.Exists))
            path = p + Path.PathSeparator + path;
        return new Dictionary<string, string> { ["PATH"] = path };
    }
}
