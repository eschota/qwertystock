using System.Text.Json;
namespace QwertyStock.Bootstrapper;

public sealed class DaemonSettings
{
    public const int DefaultRepoPollIntervalSeconds = 60;
    public const int MinRepoPollIntervalSeconds = 30;
    public const int MaxRepoPollIntervalSeconds = 86400;

    public int RepoPollIntervalSeconds { get; set; } = DefaultRepoPollIntervalSeconds;

    public void Normalize()
    {
        if (RepoPollIntervalSeconds < MinRepoPollIntervalSeconds)
            RepoPollIntervalSeconds = MinRepoPollIntervalSeconds;
        if (RepoPollIntervalSeconds > MaxRepoPollIntervalSeconds)
            RepoPollIntervalSeconds = MaxRepoPollIntervalSeconds;
    }
}

public sealed class DaemonSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public DaemonSettings LoadOrCreate()
    {
        if (!File.Exists(InstallerPaths.DaemonSettingsPath))
        {
            var d = new DaemonSettings();
            d.Normalize();
            return d;
        }

        var json = File.ReadAllText(InstallerPaths.DaemonSettingsPath);
        var s = JsonSerializer.Deserialize<DaemonSettings>(json, JsonOptions) ?? new DaemonSettings();
        s.Normalize();
        return s;
    }

    public void Save(DaemonSettings settings)
    {
        settings.Normalize();
        Directory.CreateDirectory(InstallerPaths.Root);
        File.WriteAllText(InstallerPaths.DaemonSettingsPath, JsonSerializer.Serialize(settings, JsonOptions));
    }
}
