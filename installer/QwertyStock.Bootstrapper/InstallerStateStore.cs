using System.Text.Json;

namespace QwertyStock.Bootstrapper;

public sealed class InstallerStateStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public InstallerState LoadOrCreate()
    {
        if (!File.Exists(InstallerPaths.StatePath))
            return new InstallerState();
        var json = File.ReadAllText(InstallerPaths.StatePath);
        var state = JsonSerializer.Deserialize<InstallerState>(json, JsonOptions);
        if (state == null)
            throw new InvalidOperationException("installer_state.json could not be read. Delete it and try again.");
        return state;
    }

    public void Save(InstallerState state)
    {
        Directory.CreateDirectory(InstallerPaths.Root);
        var json = JsonSerializer.Serialize(state, JsonOptions);
        File.WriteAllText(InstallerPaths.StatePath, json);
    }
}
