using System.Net;
using System.Net.Http;

namespace QwertyStock.Bootstrapper;

/// <summary>Shared <see cref="HttpClient"/> after <see cref="OutboundProxySetup"/>.</summary>
public static class InstallerHttp
{
    private static HttpClient? _client;

    public static HttpClient Client => _client ?? throw new InvalidOperationException("InstallerHttp.Initialize not called.");

    public static void Initialize(IWebProxy? proxy)
    {
        if (_client != null)
            return;

        var handler = new SocketsHttpHandler
        {
            UseProxy = proxy != null,
            Proxy = proxy,
            AutomaticDecompression = DecompressionMethods.All,
        };
        WebProxyHelper.ApplyLocalBypass(proxy);
        _client = new HttpClient(handler, disposeHandler: true) { Timeout = TimeSpan.FromMinutes(20) };
    }
}
