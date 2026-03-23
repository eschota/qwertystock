using System.Net;
using System.Net.Http.Headers;

namespace QwertyStock.Bootstrapper;

/// <summary>HTTP downloads with progress (bytes, speed, ETA) and optional parallel ranges.</summary>
internal static class HttpDownload
{
    public const int CopyBufferSize = 1024 * 1024;

    private const long ParallelThresholdBytes = 4L * 1024 * 1024;
    private const long TargetPartBytes = 8L * 1024 * 1024;
    private const int MaxParallelParts = 8;

    public static Task DownloadToFileAsync(HttpClient http, string url, string path, CancellationToken ct) =>
        DownloadToFileAsync(http, url, path, null, ct);

    public static async Task DownloadToFileAsync(
        HttpClient http,
        string url,
        string path,
        IProgress<TransferProgress>? progress,
        CancellationToken ct)
    {
        if (await TryHeadForParallelAsync(http, url, ct).ConfigureAwait(false) is { } probe
            && probe.Length >= ParallelThresholdBytes
            && probe.AcceptRangesBytes)
        {
            await DownloadParallelToFileAsync(http, url, path, probe.Length, progress, ct).ConfigureAwait(false);
            return;
        }

        await DownloadSingleToFileAsync(http, url, path, progress, ct).ConfigureAwait(false);
    }

    public static Task DownloadToFileAsync(HttpClient http, Uri url, string path, IProgress<TransferProgress>? progress, CancellationToken ct) =>
        DownloadToFileAsync(http, url.ToString(), path, progress, ct);

    private static async Task<(long Length, bool AcceptRangesBytes)?> TryHeadForParallelAsync(
        HttpClient http,
        string url,
        CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Head, url);
        using var resp = await http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        if (!resp.IsSuccessStatusCode)
            return null;
        var len = resp.Content.Headers.ContentLength;
        if (!len.HasValue || len.Value <= 0)
            return null;
        var rangesOk = resp.Headers.TryGetValues("Accept-Ranges", out var vals)
            && vals.Any(v => v.Equals("bytes", StringComparison.OrdinalIgnoreCase));
        return (len.Value, rangesOk);
    }

    private static async Task DownloadSingleToFileAsync(
        HttpClient http,
        string url,
        string path,
        IProgress<TransferProgress>? progress,
        CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        using var resp = await http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        var total = resp.Content.Headers.ContentLength;
        await using var stream = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        await using var fs = new FileStream(
            path,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: CopyBufferSize,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        var tracker = new TransferSpeedTracker();
        var buffer = new byte[CopyBufferSize];
        long received = 0;
        while (true)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), ct).ConfigureAwait(false);
            if (read == 0)
                break;
            await fs.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false);
            received += read;
            progress?.Report(tracker.BuildSnapshot(received, total));
        }
    }

    private static async Task DownloadParallelToFileAsync(
        HttpClient http,
        string url,
        string path,
        long length,
        IProgress<TransferProgress>? progress,
        CancellationToken ct)
    {
        var partCount = (int)Math.Max(2, Math.Min(MaxParallelParts, (int)Math.Ceiling((double)length / TargetPartBytes)));
        var ranges = BuildRanges(length, partCount);
        var partPaths = new string[ranges.Count];
        for (var i = 0; i < partPaths.Length; i++)
            partPaths[i] = path + ".part" + i.ToString("D2");

        var tracker = new TransferSpeedTracker();
        var receivedLock = new object();
        long receivedTotal = 0;

        void OnMoreBytes(int n)
        {
            if (progress == null || n == 0)
                return;
            long snap;
            lock (receivedLock)
            {
                receivedTotal += n;
                snap = receivedTotal;
            }

            TransferProgress tp;
            lock (tracker)
            {
                tp = tracker.BuildSnapshot(snap, length);
            }

            progress.Report(tp);
        }

        try
        {
            await Task.WhenAll(
                Enumerable.Range(0, ranges.Count).Select(
                    async i =>
                    {
                        var (start, end) = ranges[i];
                        await DownloadRangeToTempAsync(http, url, partPaths[i], start, end, OnMoreBytes, ct)
                            .ConfigureAwait(false);
                    })).ConfigureAwait(false);

            await using (var outFs = new FileStream(
                path,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                bufferSize: CopyBufferSize,
                FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                foreach (var p in partPaths)
                {
                    await using var pfs = new FileStream(
                        p,
                        FileMode.Open,
                        FileAccess.Read,
                        FileShare.Read,
                        bufferSize: CopyBufferSize,
                        FileOptions.Asynchronous | FileOptions.SequentialScan);
                    await pfs.CopyToAsync(outFs, CopyBufferSize, ct).ConfigureAwait(false);
                }
            }

            if (new FileInfo(path).Length != length)
                throw new InvalidOperationException("Parallel download assembled file size mismatch.");

            foreach (var p in partPaths)
                TryDelete(p);
        }
        catch
        {
            foreach (var p in partPaths)
                TryDelete(p);
            TryDelete(path);
            throw;
        }
    }

    private static async Task DownloadRangeToTempAsync(
        HttpClient http,
        string url,
        string tempPath,
        long start,
        long end,
        Action<int> onBytesRead,
        CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Range = new RangeHeaderValue(start, end);
        using var resp = await http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        if (resp.StatusCode != HttpStatusCode.PartialContent)
        {
            throw new InvalidOperationException(
                $"Parallel download: expected 206 Partial Content, got {(int)resp.StatusCode}.");
        }

        await using var stream = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        await using var fs = new FileStream(
            tempPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: CopyBufferSize,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        var buffer = new byte[CopyBufferSize];
        while (true)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), ct).ConfigureAwait(false);
            if (read == 0)
                break;
            await fs.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false);
            onBytesRead(read);
        }
    }

    private static List<(long Start, long End)> BuildRanges(long total, int parts)
    {
        var list = new List<(long Start, long End)>(parts);
        var baseSize = total / parts;
        var remainder = total % parts;
        var start = 0L;
        for (var i = 0; i < parts; i++)
        {
            var chunk = baseSize + (i < remainder ? 1 : 0);
            if (chunk <= 0)
                break;
            var end = start + chunk - 1;
            list.Add((start, end));
            start = end + 1;
        }

        return list;
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // best effort
        }
    }
}
