using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace QwertyStock.Bootstrapper;

/// <summary>Windows 11 DWM: round native window corners (reduces light “square” fringing on borderless WPF).</summary>
internal static class DwmWindowInterop
{
    private const int DwmwaWindowCornerPreference = 33;

    private enum DwmWindowCornerPreference : uint
    {
        Default = 0,
        DoNotRound = 1,
        Round = 2,
        RoundSmall = 3,
    }

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int dwAttribute, ref uint pvAttribute, int cbAttribute);

    internal static void TryApplyRoundedCorners(Window window)
    {
        if (!OperatingSystem.IsWindows())
            return;

        var h = new WindowInteropHelper(window).Handle;
        if (h == IntPtr.Zero)
            return;

        uint pref = (uint)DwmWindowCornerPreference.Round;
        DwmSetWindowAttribute(h, DwmwaWindowCornerPreference, ref pref, sizeof(uint));
    }
}
