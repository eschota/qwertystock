using System.Diagnostics;

namespace QwertyStock.Bootstrapper;

public static class LogFolderOpener
{
    public static void OpenLogsFolder()
    {
        Directory.CreateDirectory(InstallerPaths.LogsDir);
        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = "\"" + InstallerPaths.LogsDir + "\"",
            UseShellExecute = true,
        });
    }
}
