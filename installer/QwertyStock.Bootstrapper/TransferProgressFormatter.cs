namespace QwertyStock.Bootstrapper;

internal static class TransferProgressFormatter
{
    public static string FormatSpeed(double bytesPerSecond)
    {
        if (bytesPerSecond <= 0 || double.IsNaN(bytesPerSecond))
            return "—";
        if (bytesPerSecond >= 0x100000)
            return $"{bytesPerSecond / 0x100000:F1} MB/s";
        if (bytesPerSecond >= 1024)
            return $"{bytesPerSecond / 1024:F1} KB/s";
        return $"{bytesPerSecond:F0} B/s";
    }

    public static string FormatEta(int? seconds)
    {
        if (seconds is null or < 0)
            return "—";
        if (seconds == 0)
            return "0:00";
        var t = TimeSpan.FromSeconds(Math.Min(seconds.Value, 99 * 3600));
        return t.TotalHours >= 1
            ? $"{(int)t.TotalHours}:{t.Minutes:D2}:{t.Seconds:D2}"
            : $"{t.Minutes}:{t.Seconds:D2}";
    }

    public static string FormatBytes(long bytes)
    {
        if (bytes < 1024)
            return $"{bytes} B";
        if (bytes < 0x100000)
            return $"{bytes / 1024.0:F1} KB";
        if (bytes < 0x40000000)
            return $"{bytes / (double)0x100000:F1} MB";
        return $"{bytes / (double)0x40000000:F2} GB";
    }

    public static string FormatBytesOfTotal(long received, long? total)
    {
        if (total is > 0)
            return $"{FormatBytes(received)} / {FormatBytes(total.Value)}";
        return FormatBytes(received);
    }
}
