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
    public static string RuntimeDir => Path.Combine(Root, "runtime");
    public static string PythonRuntimeDir => Path.Combine(RuntimeDir, "python");
    public static string PythonExe => Path.Combine(PythonRuntimeDir, "python.exe");
    public static string GitDir => Path.Combine(RuntimeDir, "git");
    public static string GitExe => Path.Combine(GitDir, "cmd", "git.exe");
    public static string RepoDir => Path.Combine(Root, "repo");
    public static string WebServerDir => Path.Combine(RepoDir, "qwertystock_web_server");

    public const string DefaultManifestUrl = "https://way.qwertystock.com/installer/version.json";
    public const string RepoRemoteUrl = "https://github.com/eschota/qwertystock.git";
    public const int ServerPort = 3000;
    public const string LocalServerUrl = "http://localhost:3000/";

    /// <summary>Embeddable Python 3.11.x Windows x86-64 (adjust when bumping runtime).</summary>
    public const string PythonEmbedZipUrl = "https://www.python.org/ftp/python/3.11.9/python-3.11.9-embed-amd64.zip";

    public const string PythonEmbedVersion = "3.11.9";

    /// <summary>MinGit portable (cmd/git.exe layout).</summary>
    public const string MinGitZipUrl =
        "https://github.com/git-for-windows/git/releases/download/v2.44.0.windows.1/MinGit-2.44.0-64-bit.zip";

    public const string MinGitVersion = "2.44.0";
}
