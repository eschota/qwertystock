namespace QwertyStock.Bootstrapper;

/// <summary>0–100 scale segments for the installer pipeline.</summary>
internal static class ProgressSegments
{
    public const double PrepareEnd = 3;
    public const double NetworkEnd = 5;
    /// <summary>Self-update (скачивание ~160 MB exe): широкий сегмент, иначе полоска «застывает» на ~5%.</summary>
    public const double SelfUpdateStart = 5;
    public const double SelfUpdateEnd = 45;
    public const double RuntimeStart = 45;
    public const double RuntimeEnd = 62;
    public const double RepoStart = 62;
    public const double RepoEnd = 74;
    public const double PipStart = 74;
    public const double PipEnd = 97;
    public const double PortEnd = 98;
    public const double ServerEnd = 99;
    public const double HttpWaitEnd = 99.5;
}
