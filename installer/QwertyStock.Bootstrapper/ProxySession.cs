namespace QwertyStock.Bootstrapper;

/// <summary>Holds selected outbound proxy for HttpClient, git, and pip for this run.</summary>
public static class ProxySession
{
    public static bool IsDirect { get; private set; } = true;

    /// <summary>Active proxy URI, or null if direct.</summary>
    public static string? ActiveProxyUri { get; private set; }

    public static bool IsHttpProxy { get; private set; }

    public static void SetDirect()
    {
        IsDirect = true;
        ActiveProxyUri = null;
        IsHttpProxy = false;
    }

    public static void SetProxy(string uri, bool isHttp)
    {
        IsDirect = false;
        ActiveProxyUri = uri;
        IsHttpProxy = isHttp;
    }

    public static IReadOnlyDictionary<string, string> GetProcessEnvironment()
    {
        if (IsDirect || string.IsNullOrEmpty(ActiveProxyUri))
            return new Dictionary<string, string>();

        var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (IsHttpProxy)
        {
            d["HTTP_PROXY"] = ActiveProxyUri;
            d["HTTPS_PROXY"] = ActiveProxyUri;
            d["http_proxy"] = ActiveProxyUri;
            d["https_proxy"] = ActiveProxyUri;
        }
        else
        {
            d["ALL_PROXY"] = ActiveProxyUri;
            d["all_proxy"] = ActiveProxyUri;
        }

        return d;
    }

    /// <summary>Git CLI prefix: <c>-c http.proxy=... -c https.proxy=...</c> (works for HTTP and socks5://).</summary>
    public static string GitProxyPrefix()
    {
        if (IsDirect || string.IsNullOrEmpty(ActiveProxyUri))
            return "";

        var p = ActiveProxyUri;
        return $"-c http.proxy={p} -c https.proxy={p} ";
    }
}
