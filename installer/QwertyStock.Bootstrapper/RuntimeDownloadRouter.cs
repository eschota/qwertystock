namespace QwertyStock.Bootstrapper;

/// <summary>Maps parallel Python zip + MinGit zip progress into one overall percent + speed/ETA.</summary>
internal sealed class RuntimeDownloadRouter
{
    private readonly IProgress<InstallProgress>? _ui;
    private readonly object _lock = new();
    private readonly bool _needPython;
    private readonly bool _needGit;
    private double _pyFrac;
    private double _gitFrac;
    private TransferProgress _lastPy;
    private TransferProgress _lastGit;

    public RuntimeDownloadRouter(IProgress<InstallProgress>? ui, bool needPython, bool needGit)
    {
        _ui = ui;
        _needPython = needPython;
        _needGit = needGit;
        _pyFrac = needPython ? 0 : 1;
        _gitFrac = needGit ? 0 : 1;
    }

    public IProgress<TransferProgress> Python => new Progress<TransferProgress>(OnPython);
    public IProgress<TransferProgress> Git => new Progress<TransferProgress>(OnGit);

    private void OnPython(TransferProgress tp)
    {
        lock (_lock)
        {
            _lastPy = tp;
            _pyFrac = tp.TotalBytes is > 0 ? tp.BytesReceived / (double)tp.TotalBytes.Value : _pyFrac;
            Push(InstallerStrings.ProgressPythonGit);
        }
    }

    private void OnGit(TransferProgress tp)
    {
        lock (_lock)
        {
            _lastGit = tp;
            _gitFrac = tp.TotalBytes is > 0 ? tp.BytesReceived / (double)tp.TotalBytes.Value : _gitFrac;
            Push(InstallerStrings.ProgressPythonGit);
        }
    }

    private void Push(string message)
    {
        var frac = 0.5 * _pyFrac + 0.5 * _gitFrac;
        var pct = ProgressSegments.RuntimeStart +
                  (ProgressSegments.RuntimeEnd - ProgressSegments.RuntimeStart) * frac;
        var speed = Math.Max(_lastPy.BytesPerSecond, _lastGit.BytesPerSecond);
        var etaA = _lastPy.EtaSeconds;
        var etaB = _lastGit.EtaSeconds;
        int? eta = null;
        if (etaA is >= 0 && etaB is >= 0)
            eta = Math.Max(etaA ?? 0, etaB ?? 0);
        else if (etaA is >= 0)
            eta = etaA;
        else if (etaB is >= 0)
            eta = etaB;

        var rx = _lastPy.BytesReceived + _lastGit.BytesReceived;
        long? tx = null;
        if (_lastPy.TotalBytes is > 0 && _lastGit.TotalBytes is > 0)
            tx = _lastPy.TotalBytes!.Value + _lastGit.TotalBytes!.Value;

        _ui?.Report(new InstallProgress(
            pct,
            message,
            Indeterminate: false,
            Detail: null,
            SpeedText: TransferProgressFormatter.FormatSpeed(speed),
            EtaText: TransferProgressFormatter.FormatEta(eta),
            BytesText: TransferProgressFormatter.FormatBytesOfTotal(rx, tx)));
    }
}
