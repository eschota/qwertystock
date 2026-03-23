namespace QwertyStock.Bootstrapper;

public sealed class InstallerState
{
    /// <summary>Override self-update manifest URL (default from code).</summary>
    public string? UpdateManifestUrl { get; set; }

    public string GitBranch { get; set; } = "main";

    public string LaunchArgs { get; set; } = "main.py";

    public string? PythonEmbedVersion { get; set; }
    public string? PythonEmbedSha256 { get; set; }

    public string? MinGitVersion { get; set; }

    /// <summary>SHA256 (hex) of last successful pip requirements.txt.</summary>
    public string? RequirementsTxtSha256 { get; set; }
}
