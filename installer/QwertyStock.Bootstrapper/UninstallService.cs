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
        PortProcessTerminator.TryKillPythonServerFromPidFile();
        TryDeleteServerPidFile();
        TryDeleteDataRoot();

        System.Windows.MessageBox.Show(
            InstallerStrings.UninstallDone,
            InstallerStrings.AppTitle,
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private static void TryDeleteServerPidFile()
    {
        try
        {
            if (File.Exists(PidPath))
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
