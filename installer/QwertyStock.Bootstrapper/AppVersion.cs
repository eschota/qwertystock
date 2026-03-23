using System.Reflection;

namespace QwertyStock.Bootstrapper;

/// <summary>Единая точка для версии сборки (совпадает с &lt;Version&gt; в .csproj и self-update).</summary>
public static class AppVersion
{
    public static string Semantic
    {
        get
        {
            var v = Assembly.GetExecutingAssembly().GetName().Version;
            return v is { Build: >= 0 }
                ? $"{v.Major}.{v.Minor}.{v.Build}"
                : "?";
        }
    }
}
