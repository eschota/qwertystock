namespace QwertyStock.Bootstrapper;

public enum StartupLaunchResult
{
    /// <summary>Show full installer window (first run or repair).</summary>
    NeedFullInstallerUi,

    /// <summary>Opened cabinet in browser; host should exit.</summary>
    OpenedCabinetAndExit,
}
