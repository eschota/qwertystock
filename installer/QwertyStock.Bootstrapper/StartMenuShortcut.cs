namespace QwertyStock.Bootstrapper;

/// <summary>
/// Creates a Start Menu shortcut so Windows Search ("Пуск") can find the app.
/// Autorun via Registry alone does not add a searchable Start Menu entry.
/// </summary>
public static class StartMenuShortcut
{
    private const string ShortcutFileName = "QwertyStock.lnk";

    public static string ShortcutPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Microsoft",
        "Windows",
        "Start Menu",
        "Programs",
        ShortcutFileName);

    /// <summary>Creates or updates the Programs menu shortcut pointing at the current exe.</summary>
    public static void CreateOrUpdate()
    {
        var exe = Environment.ProcessPath;
        if (string.IsNullOrEmpty(exe) || !File.Exists(exe))
            return;

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ShortcutPath)!);
        }
        catch
        {
            return;
        }

        try
        {
            var t = Type.GetTypeFromProgID("WScript.Shell");
            if (t == null)
                return;
            dynamic shell = Activator.CreateInstance(t)!;
            dynamic shortcut = shell.CreateShortcut(ShortcutPath);
            shortcut.TargetPath = exe;
            shortcut.WorkingDirectory = Path.GetDirectoryName(exe) ?? "";
            shortcut.Description = InstallerStrings.AppTitle;
            shortcut.IconLocation = exe + ",0";
            shortcut.Save();
        }
        catch
        {
            // ignore COM / permission failures
        }
    }

    public static void TryRemove()
    {
        try
        {
            if (File.Exists(ShortcutPath))
                File.Delete(ShortcutPath);
        }
        catch
        {
            // ignore
        }
    }
}
