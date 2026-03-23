using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

namespace QwertyStock.Bootstrapper;

internal static class InstallerManifestFetcher
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static async Task<InstallerManifest> FetchAsync(string manifestUrl, CancellationToken ct)
    {
        var http = InstallerHttp.Client;
        // Заголовки no-cache + уникальный query: иначе HTTP-прокси отдаёт старый version.json, self-update «молчит».
        var requestUri = AppendCacheBustQuery(manifestUrl);
        using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        request.Headers.CacheControl = new CacheControlHeaderValue { NoCache = true, NoStore = true };
        request.Headers.TryAddWithoutValidation("Pragma", "no-cache");
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        var manifest = await JsonSerializer.DeserializeAsync<InstallerManifest>(stream, JsonOptions, ct)
            .ConfigureAwait(false);
        if (manifest == null || string.IsNullOrWhiteSpace(manifest.Version) || string.IsNullOrWhiteSpace(manifest.Url))
            throw new InvalidOperationException("Update manifest is missing version or url.");
        return manifest;
    }

    private static string AppendCacheBustQuery(string manifestUrl)
    {
        if (!Uri.TryCreate(manifestUrl, UriKind.Absolute, out var u))
            return manifestUrl;
        var b = new UriBuilder(u);
        var q = b.Query.TrimStart('?');
        var pair = "_cb=" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        b.Query = string.IsNullOrEmpty(q) ? pair : q + "&" + pair;
        return b.Uri.AbsoluteUri;
    }
}
