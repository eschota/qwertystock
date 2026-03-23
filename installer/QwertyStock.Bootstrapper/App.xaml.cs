using System.Windows;

namespace QwertyStock.Bootstrapper;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        InstallerLocale.Initialize(e.Args);
        base.OnStartup(e);
    }
}
