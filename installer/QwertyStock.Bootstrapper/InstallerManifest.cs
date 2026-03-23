namespace QwertyStock.Bootstrapper;

/// <summary>Unified installer manifest: self-update + optional CDN mirrors for runtime assets.</summary>
public sealed class InstallerManifest
{
    /// <summary>Published bootstrapper semantic version.</summary>
    public string Version { get; set; } = "";

    /// <summary>HTTPS URL of qwertystock.exe (self-update).</summary>
    public string Url { get; set; } = "";

    /// <summary>Optional SHA256 (hex) of the published exe.</summary>
    public string? Sha256 { get; set; }

    /// <summary>Optional mirror for embeddable Python zip (same bytes as upstream).</summary>
    public string? PythonEmbedZipUrl { get; set; }

    /// <summary>SHA256 of Python zip (required when <see cref="PythonEmbedZipUrl"/> is a custom host).</summary>
    public string? PythonEmbedZipSha256 { get; set; }

    /// <summary>Optional mirror for MinGit zip.</summary>
    public string? MinGitZipUrl { get; set; }

    public string? MinGitZipSha256 { get; set; }

    /// <summary>Optional git remote (bare mirror). When set, replaces default GitHub URL.</summary>
    public string? RepoRemoteUrl { get; set; }

    /// <summary>Optional mirror for get-pip.py.</summary>
    public string? GetPipUrl { get; set; }

    public string? GetPipSha256 { get; set; }

    /// <summary>Optional pip index (e.g. https://pypi.org/simple or a private mirror).</summary>
    public string? PipIndexUrl { get; set; }
}
