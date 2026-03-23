namespace QwertyStock.Bootstrapper;

/// <param name="Percent">Overall 0–100 (smooth).</param>
/// <param name="Indeterminate">True only when no numeric progress exists for this phase.</param>
public readonly record struct InstallProgress(
    double Percent,
    string Message,
    bool Indeterminate,
    string? Detail = null,
    string? SpeedText = null,
    string? EtaText = null,
    string? BytesText = null);
