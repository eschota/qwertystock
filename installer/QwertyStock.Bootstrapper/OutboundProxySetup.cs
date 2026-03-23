using System.Net;
using System.Net.Http;

namespace QwertyStock.Bootstrapper;

/// <summary>
/// Один раз за запуск: проверяет HTTPS до <see cref="InstallerPaths.DefaultManifestUrl"/> (обновления/манифест), не к python.org —
/// встроенный Python уже в каталоге приложения.
/// </summary>
public static class OutboundProxySetup
{
    private static readonly Uri ProbeUri = new(InstallerPaths.DefaultManifestUrl);

    public static async Task EnsureAsync(InstallerLogger log, CancellationToken ct)
    {
        // Один раз за жизнь процесса: иначе каждый тик фона (~60 с) снова гонял бы пробу и шумел в лог.
        if (InstallerHttp.IsInitialized)
            return;

        var catalog = ProxyCatalog.Load();

        if (await ProbeAsync(null, log, "прямое подключение", ct).ConfigureAwait(false))
        {
            ProxySession.SetDirect();
            InstallerHttp.Initialize(null);
            log.Info($"Сеть: прямой доступ к {InstallerPaths.DefaultManifestUrl} OK.");
            return;
        }

        log.Info("Прямой доступ недоступен, перебор HTTP-прокси…");
        if (await TryProxyListAsync(catalog.Http, isHttp: true, log, ct).ConfigureAwait(false))
            return;

        log.Info("HTTP-прокси не подошли, перебор SOCKS5…");
        if (await TryProxyListAsync(catalog.Socks5, isHttp: false, log, ct).ConfigureAwait(false))
            return;

        throw new InvalidOperationException(
            $"Не удалось выйти в интернет: прямой доступ и все прокси из списка не прошли проверку ({InstallerPaths.DefaultManifestUrl}).");
    }

    private static async Task<bool> TryProxyListAsync(
        IReadOnlyList<string> uris,
        bool isHttp,
        InstallerLogger log,
        CancellationToken ct)
    {
        foreach (var uri in uris)
        {
            if (!Uri.TryCreate(uri, UriKind.Absolute, out var u))
                continue;
            var proxy = new WebProxy(u);
            if (!await ProbeAsync(proxy, log, uri, ct).ConfigureAwait(false))
                continue;

            ProxySession.SetProxy(uri, isHttp);
            InstallerHttp.Initialize(proxy);
            log.Info(isHttp ? $"Сеть: выбран HTTP-прокси {uri}" : $"Сеть: выбран SOCKS5 {uri}");
            return true;
        }

        return false;
    }

    private static async Task<bool> ProbeAsync(IWebProxy? proxy, InstallerLogger log, string label, CancellationToken ct)
    {
        try
        {
            var handler = new SocketsHttpHandler
            {
                UseProxy = proxy != null,
                Proxy = proxy,
            };
            WebProxyHelper.ApplyLocalBypass(proxy);
            using var client = new HttpClient(handler, disposeHandler: true) { Timeout = TimeSpan.FromSeconds(15) };
            using var req = new HttpRequestMessage(HttpMethod.Get, ProbeUri);
            using var resp = await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
            return resp.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            log.Info($"Проверка ({label}): {ex.Message}");
            return false;
        }
    }
}
