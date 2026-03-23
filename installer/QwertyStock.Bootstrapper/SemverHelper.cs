namespace QwertyStock.Bootstrapper;

public static class SemverHelper
{
    /// <summary>Returns true if <paramref name="a"/> is strictly newer than <paramref name="b"/>.</summary>
    public static bool IsNewer(string a, string b)
    {
        var pa = Parse(a);
        var pb = Parse(b);
        if (pa == null || pb == null)
            return false;
        return pa.Value.CompareTo(pb.Value) > 0;
    }

    private static (int Major, int Minor, int Patch)? Parse(string v)
    {
        v = v.Trim().TrimStart('v');
        var parts = v.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 1)
            return null;
        if (!int.TryParse(parts[0], out var major))
            return null;
        var minor = parts.Length > 1 && int.TryParse(parts[1], out var mi) ? mi : 0;
        var patch = parts.Length > 2 && int.TryParse(parts[2], out var pa) ? pa : 0;
        return (major, minor, patch);
    }
}
