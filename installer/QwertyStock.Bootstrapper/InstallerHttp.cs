using System.Net;
using System.Net.Http;

namespace QwertyStock.Bootstrapper;

/// <summary>Shared <see cref="HttpClient"/> after <see cref="OutboundProxySetup"/>.</summary>
public static class InstallerHttp
{
    private static HttpClient? _client;
    private static IWebProxy? _configuredProxy;

    /// <summary>True after первого успешного <see cref="Initialize"/> — повторный выбор прокси в том же процессе не нужен.</summary>
    public static bool IsInitialized => _client != null;

    public static HttpClient Client => _client ?? throw new InvalidOperationException("InstallerHttp.Initialize not called.");

    public static void Initialize(IWebProxy? proxy)
    {
        if (_client != null)
            return;

        _configuredProxy = proxy;
        var handler = new SocketsHttpHandler
        {
            UseProxy = proxy != null,
            Proxy = proxy,
            AutomaticDecompression = DecompressionMethods.All,
            MaxConnectionsPerServer = 16,
        };
        WebProxyHelper.ApplyLocalBypass(proxy);
        _client = new HttpClient(handler, disposeHandler: true) { Timeout = TimeSpan.FromMinutes(20) };
    }

    /// <summary>
    /// Отдельный клиент для скачивания крупного бинарника (qwertystock.exe): тот же прокси, без авто-декомпрессии,
    /// <see cref="HttpClient.Timeout"/> = 0 (без лимита) — иначе ~160 MB на медленном канале обрывается через 20 мин.
    /// </summary>
    public static HttpClient CreateLargeBinaryDownloadClient()
    {
        if (_client == null)
            throw new InvalidOperationException("InstallerHttp.Initialize not called.");

        var handler = new SocketsHttpHandler
        {
            UseProxy = _configuredProxy != null,
            Proxy = _configuredProxy,
            AutomaticDecompression = DecompressionMethods.None,
            MaxConnectionsPerServer = 4,
        };
        WebProxyHelper.ApplyLocalBypass(_configuredProxy as WebProxy);
        return new HttpClient(handler, disposeHandler: true) { Timeout = TimeSpan.Zero };
    }
}
