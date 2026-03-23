namespace QwertyStock.Bootstrapper;

public sealed class InstallerLogger
{
    private readonly object _lock = new();

    public InstallerLogger()
    {
        try
        {
            Directory.CreateDirectory(InstallerPaths.LogsDir);
        }
        catch
        {
            // logged to debug only if file fails
        }
    }

    public void Info(string message)
    {
        Write("INFO", message, null);
    }

    public void Error(string message, Exception? ex = null)
    {
        Write("ERROR", message, ex);
    }

    private void Write(string level, string message, Exception? ex)
    {
        var line = $"{DateTime.UtcNow:O} [{level}] {message}";
        if (ex != null)
            line += Environment.NewLine + ex;
        lock (_lock)
        {
            try
            {
                File.AppendAllText(InstallerPaths.InstallerLog, line + Environment.NewLine);
            }
            catch
            {
                System.Diagnostics.Debug.WriteLine(line);
            }
        }
    }
}
