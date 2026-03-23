using System.Reflection;

namespace QwertyStock.Bootstrapper;

/// <summary>Версия сборки exe; при релизе совпадает с <c>VERSION</c> в корне репозитория и с полем <c>version</c> в <c>installer/version.json</c>.</summary>
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
