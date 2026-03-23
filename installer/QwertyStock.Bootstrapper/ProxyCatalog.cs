using System.Reflection;
using System.Text.Json;

namespace QwertyStock.Bootstrapper;

public sealed class ProxyCatalog
{
    public IReadOnlyList<string> Http { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> Socks5 { get; init; } = Array.Empty<string>();

    public static ProxyCatalog Load()
    {
        var asm = Assembly.GetExecutingAssembly();
        const string name = "QwertyStock.Bootstrapper.Assets.NetworkProxies.json";
        using var stream = asm.GetManifestResourceStream(name);
        if (stream == null)
        {
            foreach (var n in asm.GetManifestResourceNames())
            {
                if (n.EndsWith("NetworkProxies.json", StringComparison.OrdinalIgnoreCase))
                {
                    using var s2 = asm.GetManifestResourceStream(n);
                    if (s2 != null)
                        return Parse(s2);
                }
            }

            throw new InvalidOperationException("NetworkProxies.json embedded resource not found.");
        }

        return Parse(stream);
    }

    private static ProxyCatalog Parse(Stream stream)
    {
        var doc = JsonDocument.Parse(stream);
        var root = doc.RootElement;
        var http = root.GetProperty("http").EnumerateArray().Select(e => e.GetString()!).ToList();
        var socks = root.GetProperty("socks5").EnumerateArray().Select(e => e.GetString()!).ToList();
        return new ProxyCatalog { Http = http, Socks5 = socks };
    }
}
