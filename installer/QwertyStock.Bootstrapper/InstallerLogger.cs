using System.Text;

namespace QwertyStock.Bootstrapper;

public sealed class InstallerLogger
{
    private static readonly object SessionLock = new();
    private static bool _sessionPrepared;

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

    /// <summary>
    /// Один раз за процесс: текущий <c>installer.log</c> → <c>installer.log.prev</c>, новый сеанс пишется с нуля.
    /// Большой .prev обрезается с хвоста, чтобы не раздувать диск.
    /// </summary>
    public static void PrepareNewSession()
    {
        lock (SessionLock)
        {
            if (_sessionPrepared)
                return;
            _sessionPrepared = true;
            try
            {
                Directory.CreateDirectory(InstallerPaths.LogsDir);
                var cur = InstallerPaths.InstallerLog;
                var prev = InstallerPaths.InstallerLogPrev;
                if (File.Exists(cur))
                    File.Move(cur, prev, overwrite: true);
                TrimTailIfTooLarge(prev, maxBytes: 5_000_000);
            }
            catch
            {
                // best effort
            }
        }
    }

    /// <summary>
    /// Перед запуском нового процесса Python: текущий <c>server.log</c> → <c>server.log.prev</c>, новый сеанс пишется с нуля.
    /// </summary>
    public static void PrepareServerLogSession()
    {
        try
        {
            Directory.CreateDirectory(InstallerPaths.LogsDir);
            var cur = InstallerPaths.ServerLog;
            var prev = InstallerPaths.ServerLogPrev;
            if (File.Exists(cur))
                File.Move(cur, prev, overwrite: true);
            TrimTailIfTooLarge(prev, maxBytes: 5_000_000);
        }
        catch
        {
            // best effort
        }
    }

    private static void TrimTailIfTooLarge(string path, long maxBytes)
    {
        if (!File.Exists(path))
            return;
        var len = new FileInfo(path).Length;
        if (len <= maxBytes)
            return;
        var keep = maxBytes - 400;
        if (keep < 1024)
            return;
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        fs.Seek(len - keep, SeekOrigin.Begin);
        var buf = new byte[keep];
        var read = fs.Read(buf, 0, (int)keep);
        var tmp = path + ".tmp";
        var prefix = Encoding.UTF8.GetBytes("... [truncated; older lines removed]\r\n");
        using (var outFs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            outFs.Write(prefix);
            outFs.Write(buf, 0, read);
        }

        File.Move(tmp, path, overwrite: true);
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
