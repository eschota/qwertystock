using System.IO;
using System.Net.Http;
using System.Net.Sockets;

namespace QwertyStock.Bootstrapper;

public static class NetworkErrors
{
    /// <summary>Whether the failure is likely transient (retry may help).</summary>
    public static bool IsLikelyNetworkRelated(Exception ex)
    {
        for (var e = ex; e != null; e = e.InnerException)
        {
            switch (e)
            {
                case HttpRequestException:
                case SocketException:
                case TaskCanceledException:
                case TimeoutException:
                    return true;
                case IOException io:
                    if (ContainsNetworkHint(io.Message))
                        return true;
                    break;
            }
        }

        return ContainsNetworkHint(ex.Message);
    }

    private static bool ContainsNetworkHint(string? message)
    {
        if (string.IsNullOrEmpty(message))
            return false;
        return message.Contains("network", StringComparison.OrdinalIgnoreCase)
               || message.Contains("connection", StringComparison.OrdinalIgnoreCase)
               || message.Contains("TLS", StringComparison.OrdinalIgnoreCase)
               || message.Contains("SSL", StringComparison.OrdinalIgnoreCase)
               || message.Contains("host", StringComparison.OrdinalIgnoreCase)
               && message.Contains("unreachable", StringComparison.OrdinalIgnoreCase);
    }
}
