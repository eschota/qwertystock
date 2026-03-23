using Microsoft.Win32;

namespace QwertyStock.Bootstrapper;

/// <summary>Registers the bootstrapper in HKCU Run for Windows logon.</summary>
public static class WindowsStartup
{
    private const string RunSubKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "QwertyStock";

    public static void Register()
    {
        var exe = Environment.ProcessPath;
        if (string.IsNullOrEmpty(exe))
            return;
        var value = "\"" + exe + "\" --startup";
        using var key = Registry.CurrentUser.CreateSubKey(RunSubKey, true);
        key.SetValue(ValueName, value, RegistryValueKind.String);
    }

    public static void Unregister()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunSubKey, true);
            key?.DeleteValue(ValueName, throwOnMissingValue: false);
        }
        catch
        {
            // ignore
        }
    }
}
