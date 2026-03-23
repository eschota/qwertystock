using System.Diagnostics;
using System.IO;
using System.Windows;

namespace QwertyStock.Bootstrapper;

public static class UninstallService
{
    private static readonly string PidPath = Path.Combine(InstallerPaths.Root, "server.pid");

    public static void RunInteractive()
    {
        var ok = System.Windows.MessageBox.Show(
            InstallerStrings.UninstallConfirm,
            InstallerStrings.AppTitle,
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (ok != MessageBoxResult.Yes)
            return;

        WindowsStartup.Unregister();
        StartMenuShortcut.TryRemove();
        TryKillServerFromPidFile();
        TryDeleteDataRoot();

        System.Windows.MessageBox.Show(
            InstallerStrings.UninstallDone,
            InstallerStrings.AppTitle,
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private static void TryKillServerFromPidFile()
    {
        if (!File.Exists(PidPath))
            return;
        try
        {
            var text = File.ReadAllText(PidPath).Trim();
            if (!int.TryParse(text, out var pid))
                return;
            using var p = Process.GetProcessById(pid);
            var path = p.MainModule?.FileName;
            if (path != null
                && path.Equals(InstallerPaths.PythonExe, StringComparison.OrdinalIgnoreCase))
            {
                p.Kill(entireProcessTree: true);
                p.WaitForExit(5000);
            }
        }
        catch
        {
            // ignore
        }

        try
        {
            File.Delete(PidPath);
        }
        catch
        {
            // ignore
        }
    }

    private static void TryDeleteDataRoot()
    {
        try
        {
            if (Directory.Exists(InstallerPaths.Root))
                Directory.Delete(InstallerPaths.Root, recursive: true);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                string.Format(InstallerStrings.UninstallDeleteFailed, ex.Message),
                InstallerStrings.AppTitle,
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }
}
