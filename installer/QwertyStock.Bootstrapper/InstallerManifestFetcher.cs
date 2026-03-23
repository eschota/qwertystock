using System.Net.Http;
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
        using var response = await http.GetAsync(manifestUrl, HttpCompletionOption.ResponseHeadersRead, ct)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        var manifest = await JsonSerializer.DeserializeAsync<InstallerManifest>(stream, JsonOptions, ct)
            .ConfigureAwait(false);
        if (manifest == null || string.IsNullOrWhiteSpace(manifest.Version) || string.IsNullOrWhiteSpace(manifest.Url))
            throw new InvalidOperationException("Update manifest is missing version or url.");
        return manifest;
    }
}
