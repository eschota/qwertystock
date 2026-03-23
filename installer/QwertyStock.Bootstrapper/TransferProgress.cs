namespace QwertyStock.Bootstrapper;

/// <summary>Byte transfer snapshot for HTTP downloads (speed + ETA from sliding window).</summary>
public readonly record struct TransferProgress(
    long BytesReceived,
    long? TotalBytes,
    double BytesPerSecond,
    int? EtaSeconds);
