using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace QwertyStock.Bootstrapper;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        InstallerLocale.Initialize(e.Args);
        var args = new HashSet<string>(
            e.Args.Select(a => a.Trim()),
            StringComparer.OrdinalIgnoreCase);

        if (args.Contains("--uninstall"))
        {
            UninstallService.RunInteractive();
            Shutdown(0);
            return;
        }

        if (args.Contains("--silent"))
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            if (args.Contains("--startup"))
                SilentStartupAsync();
            else
                SilentInstallAsync();
            return;
        }

        if (args.Contains("--startup"))
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            StartupLaunchAsync();
            return;
        }

        base.OnStartup(e);
        var w = new MainWindow();
        MainWindow = w;
        w.Show();
    }

    private async void SilentInstallAsync()
    {
        var log = new InstallerLogger();
        try
        {
            var orch = new InstallerOrchestrator(log);
            await orch.RunAsync(null, CancellationToken.None).ConfigureAwait(true);
            Environment.Exit(0);
        }
        catch (Exception ex)
        {
            log.Error("Silent install failed", ex);
            Environment.Exit(1);
        }
    }

    private async void SilentStartupAsync()
    {
        var log = new InstallerLogger();
        try
        {
            var orch = new InstallerOrchestrator(log);
            var result = await orch.RunStartupLaunchAsync(CancellationToken.None).ConfigureAwait(true);
            Environment.Exit(result == StartupLaunchResult.NeedFullInstallerUi ? 1 : 0);
        }
        catch (Exception ex)
        {
            log.Error("Silent startup failed", ex);
            Environment.Exit(1);
        }
    }

    private async void StartupLaunchAsync()
    {
        try
        {
            var log = new InstallerLogger();
            var orch = new InstallerOrchestrator(log);
            var result = await orch.RunStartupLaunchAsync(CancellationToken.None).ConfigureAwait(true);
            if (result == StartupLaunchResult.NeedFullInstallerUi)
            {
                ShutdownMode = ShutdownMode.OnMainWindowClose;
                var w = new MainWindow();
                MainWindow = w;
                w.Show();
            }
            else
            {
                Shutdown(0);
            }
        }
        catch (Exception ex)
        {
            try
            {
                MessageBox.Show(ex.Message, InstallerStrings.AppTitle, MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch
            {
                // ignore
            }

            ShutdownMode = ShutdownMode.OnMainWindowClose;
            var w = new MainWindow();
            MainWindow = w;
            w.Show();
        }
    }
}
