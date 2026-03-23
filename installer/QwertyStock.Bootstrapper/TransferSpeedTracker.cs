namespace QwertyStock.Bootstrapper;

/// <summary>Sliding-window speed estimate (~1.5s) and ETA when total size is known. Thread-safe.</summary>
public sealed class TransferSpeedTracker
{
    private readonly object _lock = new();
    private readonly LinkedList<(DateTime Utc, long Bytes)> _samples = new();
    private const double WindowSeconds = 1.5;

    public TransferProgress BuildSnapshot(long received, long? total)
    {
        lock (_lock)
        {
            var now = DateTime.UtcNow;
            _samples.AddLast((now, received));
            while (_samples.Count > 0 && (now - _samples.First!.Value.Utc).TotalSeconds > WindowSeconds)
                _samples.RemoveFirst();

            var bps = ComputeBpsUnlocked();
            int? eta = null;
            if (total is > 0 && received < total && bps >= 1)
                eta = (int)Math.Ceiling((total.Value - received) / bps);

            return new TransferProgress(received, total, bps, eta);
        }
    }

    private double ComputeBpsUnlocked()
    {
        if (_samples.Count < 2)
            return 0;
        var a = _samples.First!.Value;
        var b = _samples.Last!.Value;
        var dt = (b.Utc - a.Utc).TotalSeconds;
        if (dt < 0.02)
            return 0;
        return Math.Max(0, (b.Bytes - a.Bytes) / dt);
    }
}
