using System.Net;
using System.Net.Http;

namespace QwertyStock.Bootstrapper;

/// <summary>Probes direct connection, then HTTP proxies, then SOCKS5; configures <see cref="InstallerHttp"/> and <see cref="ProxySession"/>.</summary>
public static class OutboundProxySetup
{
    private static readonly Uri ProbeUri = new("https://www.python.org/");

    public static async Task EnsureAsync(InstallerLogger log, CancellationToken ct)
    {
        var catalog = ProxyCatalog.Load();

        if (await ProbeAsync(null, log, "прямое подключение", ct).ConfigureAwait(false))
        {
            ProxySession.SetDirect();
            InstallerHttp.Initialize(null);
            log.Info("Сеть: прямой доступ к https://www.python.org/ OK.");
            return;
        }

        log.Info("Прямой доступ недоступен, перебор HTTP-прокси…");
        foreach (var uri in catalog.Http)
        {
            if (!Uri.TryCreate(uri, UriKind.Absolute, out var u))
                continue;
            var proxy = new WebProxy(u);
            if (await ProbeAsync(proxy, log, uri, ct).ConfigureAwait(false))
            {
                ProxySession.SetProxy(uri, isHttp: true);
                InstallerHttp.Initialize(proxy);
                log.Info($"Сеть: выбран HTTP-прокси {uri}");
                return;
            }
        }

        log.Info("HTTP-прокси не подошли, перебор SOCKS5…");
        foreach (var uri in catalog.Socks5)
        {
            if (!Uri.TryCreate(uri, UriKind.Absolute, out var u))
                continue;
            var proxy = new WebProxy(u);
            if (await ProbeAsync(proxy, log, uri, ct).ConfigureAwait(false))
            {
                ProxySession.SetProxy(uri, isHttp: false);
                InstallerHttp.Initialize(proxy);
                log.Info($"Сеть: выбран SOCKS5 {uri}");
                return;
            }
        }

        throw new InvalidOperationException(
            "Не удалось выйти в интернет: прямой доступ и все прокси из списка не прошли проверку (https://www.python.org/).");
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
            if (proxy is WebProxy wpProbe)
                wpProbe.BypassProxyOnLocal = true;
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
