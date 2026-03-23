namespace QwertyStock.Bootstrapper;

/// <param name="Percent">0–100 when <see cref="Indeterminate"/> is false.</param>
/// <param name="Indeterminate">True while work runs without a known fraction (pip, git clone, large downloads).</param>
public readonly record struct InstallProgress(int Percent, string Message, bool Indeterminate);
