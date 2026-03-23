using System.Globalization;

namespace QwertyStock.Bootstrapper;

public enum InstallerLanguage
{
    English,
    Russian,
}

/// <summary>
/// UI language: from <c>--lang=</c>, then executable name (<c>qwertystock-en.exe</c> / <c>qwertystock-ru.exe</c>),
/// then Windows UI culture (ru → Russian).
/// </summary>
public static class InstallerLocale
{
    public static InstallerLanguage Current { get; private set; } = InstallerLanguage.English;

    public static void Initialize(string[]? commandLineArgs = null)
    {
        commandLineArgs ??= Environment.GetCommandLineArgs();

        foreach (var arg in commandLineArgs)
        {
            if (arg.StartsWith("--lang=", StringComparison.OrdinalIgnoreCase))
            {
                var v = arg.AsSpan(7).Trim();
                if (v.Equals("ru", StringComparison.OrdinalIgnoreCase))
                {
                    Current = InstallerLanguage.Russian;
                    return;
                }

                if (v.Equals("en", StringComparison.OrdinalIgnoreCase))
                {
                    Current = InstallerLanguage.English;
                    return;
                }
            }
        }

        var path = Environment.ProcessPath;
        if (!string.IsNullOrEmpty(path))
        {
            var baseName = Path.GetFileNameWithoutExtension(path);
            if (baseName.Contains("-ru", StringComparison.OrdinalIgnoreCase))
            {
                Current = InstallerLanguage.Russian;
                return;
            }

            if (baseName.Contains("-en", StringComparison.OrdinalIgnoreCase))
            {
                Current = InstallerLanguage.English;
                return;
            }
        }

        Current = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("ru", StringComparison.OrdinalIgnoreCase)
            ? InstallerLanguage.Russian
            : InstallerLanguage.English;
    }
}
