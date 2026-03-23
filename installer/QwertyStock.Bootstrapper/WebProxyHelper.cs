using System.Net;

namespace QwertyStock.Bootstrapper;

internal static class WebProxyHelper
{
    public static void ApplyLocalBypass(IWebProxy? proxy)
    {
        if (proxy is WebProxy wp)
            wp.BypassProxyOnLocal = true;
    }
}
