namespace QwertyStock.Bootstrapper;

public static class InstallerPaths
{
    public static string Root { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "QwertyStock");

    public static string LogsDir => Path.Combine(Root, "logs");
    public static string InstallerLog => Path.Combine(LogsDir, "installer.log");
    public static string ServerLog => Path.Combine(LogsDir, "server.log");
    public static string StatePath => Path.Combine(Root, "installer_state.json");

    /// <summary>Shared with FastAPI (env QS_DAEMON_SETTINGS_PATH): repo poll interval, etc.</summary>
    public static string DaemonSettingsPath => Path.Combine(Root, "daemon_settings.json");
    public static string RuntimeDir => Path.Combine(Root, "runtime");
    public static string PythonRuntimeDir => Path.Combine(RuntimeDir, "python");
    public static string PythonExe => Path.Combine(PythonRuntimeDir, "python.exe");
    public static string GitDir => Path.Combine(RuntimeDir, "git");
    public static string GitExe => Path.Combine(GitDir, "cmd", "git.exe");
    public static string RepoDir => Path.Combine(Root, "repo");
    public static string WebServerDir => Path.Combine(RepoDir, "qwertystock_web_server");

    public const string DefaultManifestUrl = "https://way.qwertystock.com/installer/version.json";
    public const string RepoRemoteUrl = "https://github.com/eschota/qwertystock.git";
    public const int ServerPort = 7332;

    public static string LocalServerUrl => $"http://localhost:{ServerPort}/";

    /// <summary>Embeddable Python 3.11.x Windows x86-64 (adjust when bumping runtime).</summary>
    public const string PythonEmbedZipUrl = "https://www.python.org/ftp/python/3.11.9/python-3.11.9-embed-amd64.zip";

    public const string PythonEmbedVersion = "3.11.9";

    /// <summary>SHA256 of official <see cref="PythonEmbedZipUrl"/> (verify downloads).</summary>
    public const string PythonEmbedZipSha256Official =
        "009d6bf7e3b2ddca3d784fa09f90fe54336d5b60f0e0f305c37f400bf83cfd3b";

    /// <summary>MinGit portable (cmd/git.exe layout).</summary>
    public const string MinGitZipUrl =
        "https://github.com/git-for-windows/git/releases/download/v2.44.0.windows.1/MinGit-2.44.0-64-bit.zip";

    public const string MinGitVersion = "2.44.0";

    public const string MinGitZipSha256Official =
        "ed4e74e171c59c9c9d418743c7109aa595e0cc0d1c80cac574d69ed5e571ae59";

    public const string GetPipDefaultUrl = "https://bootstrap.pypa.io/get-pip.py";

    public const string GetPipSha256Official =
        "feba1c697df45be1b539b40d93c102c9ee9dde1d966303323b830b06f3fbca3c";
}
