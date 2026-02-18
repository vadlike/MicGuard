using System.Text.Json;

public sealed class GuardConfig
{
    public string? PreferredMicDeviceId { get; set; }
    public string[] BlockedMicNameContains { get; set; } = ["OnePlus Buds Pro 3"];
    public string[] BlockedMicDeviceIds { get; set; } = [];
    public string PreferredMicNameContains { get; set; } = "Realtek";
    public bool GuardEnabled { get; set; } = true;
    public int EventDebounceMs { get; set; } = 700;

    public static GuardConfig LoadOrCreate(string path)
    {
        if (!File.Exists(path))
        {
            var defaultConfig = new GuardConfig();
            defaultConfig.Save(path);
            return defaultConfig;
        }

        try
        {
            var json = File.ReadAllText(path);
            var config = JsonSerializer.Deserialize<GuardConfig>(json, SerializerOptions) ?? new GuardConfig();
            config.Normalize();
            return config;
        }
        catch
        {
            var fallback = new GuardConfig();
            fallback.Save(path);
            return fallback;
        }
    }

    private static JsonSerializerOptions SerializerOptions => new()
    {
        WriteIndented = true
    };

    public void Save(string path)
    {
        Normalize();
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(this, SerializerOptions);
        File.WriteAllText(path, json);
    }

    private void Normalize()
    {
        BlockedMicNameContains = (BlockedMicNameContains ?? Array.Empty<string>())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (BlockedMicNameContains.Length == 0)
        {
            BlockedMicNameContains = ["OnePlus Buds Pro 3"];
        }

        BlockedMicDeviceIds = (BlockedMicDeviceIds ?? Array.Empty<string>())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        PreferredMicDeviceId = string.IsNullOrWhiteSpace(PreferredMicDeviceId)
            ? null
            : PreferredMicDeviceId.Trim();

        PreferredMicNameContains = (PreferredMicNameContains ?? string.Empty).Trim();
        EventDebounceMs = Math.Clamp(EventDebounceMs, 100, 10_000);
    }
}
