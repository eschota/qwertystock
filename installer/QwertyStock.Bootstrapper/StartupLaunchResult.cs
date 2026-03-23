namespace QwertyStock.Bootstrapper;

public enum StartupLaunchResult
{
    /// <summary>Show full installer window (first run or repair).</summary>
    NeedFullInstallerUi,

    /// <summary>Cabinet ready; host should show tray daemon and keep running.</summary>
    RunTrayDaemon,
}
