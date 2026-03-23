namespace QwertyStock.Bootstrapper;

/// <summary>0–100 scale segments for the installer pipeline.</summary>
internal static class ProgressSegments
{
    public const double PrepareEnd = 3;
    public const double NetworkEnd = 5;
    /// <summary>Self-update download (manifest check + optional exe).</summary>
    public const double SelfUpdateStart = 5;
    public const double SelfUpdateEnd = 14;
    public const double RuntimeStart = 14;
    public const double RuntimeEnd = 38;
    public const double RepoStart = 38;
    public const double RepoEnd = 56;
    public const double PipStart = 56;
    public const double PipEnd = 92;
    public const double PortEnd = 93;
    public const double ServerEnd = 95;
    public const double HttpWaitEnd = 99;
}
