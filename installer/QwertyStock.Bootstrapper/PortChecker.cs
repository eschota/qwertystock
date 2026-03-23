using System.Net.Sockets;

namespace QwertyStock.Bootstrapper;

public static class PortChecker
{
    /// <summary>Returns true if something accepts TCP connections on the port (localhost).</summary>
    public static async Task<bool> IsLocalPortInUseAsync(int port, CancellationToken ct)
    {
        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync("127.0.0.1", port, ct).ConfigureAwait(false);
            return client.Connected;
        }
        catch (SocketException)
        {
            return false;
        }
        catch (ObjectDisposedException)
        {
            return false;
        }
    }
}
