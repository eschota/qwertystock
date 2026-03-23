namespace QwertyStock.Bootstrapper;

/// <summary>Resolves CDN mirror URLs and required SHA256 for verification.</summary>
internal static class RuntimeAssetUrls
{
    public static string PythonZipUrl(InstallerManifest m) =>
        string.IsNullOrWhiteSpace(m.PythonEmbedZipUrl) ? InstallerPaths.PythonEmbedZipUrl : m.PythonEmbedZipUrl!.Trim();

    public static string MinGitZipUrl(InstallerManifest m) =>
        string.IsNullOrWhiteSpace(m.MinGitZipUrl) ? InstallerPaths.MinGitZipUrl : m.MinGitZipUrl!.Trim();

    public static string RepoRemoteUrl(InstallerManifest m) =>
        string.IsNullOrWhiteSpace(m.RepoRemoteUrl) ? InstallerPaths.RepoRemoteUrl : m.RepoRemoteUrl!.Trim();

    public static string GetPipUrl(InstallerManifest m) =>
        string.IsNullOrWhiteSpace(m.GetPipUrl) ? InstallerPaths.GetPipDefaultUrl : m.GetPipUrl!.Trim();

    public static string RequirePythonZipSha256(string resolvedUrl, InstallerManifest m)
    {
        if (!string.IsNullOrWhiteSpace(m.PythonEmbedZipSha256))
            return m.PythonEmbedZipSha256.Trim();
        if (UrlsEqual(resolvedUrl, InstallerPaths.PythonEmbedZipUrl))
            return InstallerPaths.PythonEmbedZipSha256Official;
        throw new InvalidOperationException(InstallerStrings.ErrorMirrorNeedsPythonSha256);
    }

    public static string RequireMinGitZipSha256(string resolvedUrl, InstallerManifest m)
    {
        if (!string.IsNullOrWhiteSpace(m.MinGitZipSha256))
            return m.MinGitZipSha256.Trim();
        if (UrlsEqual(resolvedUrl, InstallerPaths.MinGitZipUrl))
            return InstallerPaths.MinGitZipSha256Official;
        throw new InvalidOperationException(InstallerStrings.ErrorMirrorNeedsMinGitSha256);
    }

    public static string RequireGetPipSha256(string resolvedUrl, InstallerManifest m)
    {
        if (!string.IsNullOrWhiteSpace(m.GetPipSha256))
            return m.GetPipSha256.Trim();
        if (UrlsEqual(resolvedUrl, InstallerPaths.GetPipDefaultUrl))
            return InstallerPaths.GetPipSha256Official;
        throw new InvalidOperationException(InstallerStrings.ErrorMirrorNeedsGetPipSha256);
    }

    private static bool UrlsEqual(string a, string b) =>
        string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);
}
