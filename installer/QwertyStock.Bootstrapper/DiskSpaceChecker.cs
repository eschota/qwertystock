using System.IO;

namespace QwertyStock.Bootstrapper;

/// <summary>Ensures enough free space on the data drive before large downloads.</summary>
public static class DiskSpaceChecker
{
    /// <summary>Minimum free space required for runtime + repo + pip (conservative).</summary>
    public const long MinimumFreeBytes = 2L * 1024 * 1024 * 1024;

    public static void EnsureFreeSpaceForInstall()
    {
        string full;
        try
        {
            full = Path.GetFullPath(InstallerPaths.Root);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(InstallerStrings.ErrorDiskUnknown, ex);
        }

        var root = Path.GetPathRoot(full);
        if (string.IsNullOrEmpty(root))
            throw new InvalidOperationException(InstallerStrings.ErrorDiskUnknown);

        DriveInfo drive;
        try
        {
            drive = new DriveInfo(root);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(InstallerStrings.ErrorDiskUnknown, ex);
        }

        if (!drive.IsReady)
            throw new InvalidOperationException(InstallerStrings.ErrorDiskNotReady);

        if (drive.AvailableFreeSpace < MinimumFreeBytes)
        {
            throw new InvalidOperationException(
                string.Format(
                    System.Globalization.CultureInfo.CurrentCulture,
                    InstallerStrings.ErrorInsufficientDiskSpace,
                    TransferProgressFormatter.FormatBytes(Math.Max(0, drive.AvailableFreeSpace)),
                    TransferProgressFormatter.FormatBytes(MinimumFreeBytes)));
        }
    }
}
