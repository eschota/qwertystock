namespace QwertyStock.Bootstrapper;

/// <summary>HTTP GET в один поток с прогрессом. Параллельные Range-GET убраны — за прокси/CDN часто зависали.</summary>
internal static class HttpDownload
{
    public const int CopyBufferSize = 1024 * 1024;

    public static Task DownloadToFileAsync(HttpClient http, string url, string path, CancellationToken ct) =>
        DownloadToFileAsync(http, url, path, null, ct);

    public static Task DownloadToFileAsync(
        HttpClient http,
        string url,
        string path,
        IProgress<TransferProgress>? progress,
        CancellationToken ct) =>
        DownloadSingleToFileAsync(http, url, path, progress, ct);

    public static Task DownloadToFileAsync(HttpClient http, Uri url, string path, IProgress<TransferProgress>? progress, CancellationToken ct) =>
        DownloadToFileAsync(http, url.ToString(), path, progress, ct);

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
}
