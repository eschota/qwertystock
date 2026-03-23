using System.IO;
using System.Security.Cryptography;

namespace QwertyStock.Bootstrapper;

public static class HttpHash
{
    public static string Sha256Hex(Stream stream)
    {
        var hash = SHA256.HashData(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public static string Sha256Hex(byte[] data)
    {
        var hash = SHA256.HashData(data);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public static bool EqualsHex(string a, string b)
    {
        return string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Verifies file SHA256; throws and deletes the file on mismatch.</summary>
    public static void VerifyFileSha256Hex(string path, string expectedHex)
    {
        if (string.IsNullOrWhiteSpace(expectedHex))
            throw new ArgumentException("Expected SHA256 is required.", nameof(expectedHex));
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        var hex = Sha256Hex(fs);
        if (EqualsHex(hex, expectedHex))
            return;
        try
        {
            File.Delete(path);
        }
        catch
        {
            // ignore
        }

        throw new InvalidOperationException("Downloaded file failed SHA256 verification.");
    }
}
