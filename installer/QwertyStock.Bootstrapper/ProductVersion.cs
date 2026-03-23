namespace QwertyStock.Bootstrapper;

/// <summary>
/// Человекочитаемая версия продукта: один номер для инсталлера и кабинета
/// (корень репозитория <c>VERSION</c>, совпадает с &lt;Version&gt; в .csproj и полем <c>version</c> в опубликованном <c>version.json</c>).
/// </summary>
public static class ProductVersion
{
    /// <summary>Читает <see cref="InstallerPaths.RepoDir"/>/<c>VERSION</c> после git clone; иначе версия сборки exe.</summary>
    public static string ReadFromRepoOrAssembly()
    {
        var path = Path.Combine(InstallerPaths.RepoDir, "VERSION");
        try
        {
            if (File.Exists(path))
            {
                foreach (var raw in File.ReadLines(path))
                {
                    var line = raw.Trim();
                    if (line.Length == 0 || line.StartsWith('#'))
                        continue;
                    return line;
                }
            }
        }
        catch
        {
            // ignore — падаем на версию сборки
        }

        return AppVersion.Semantic;
    }
}
