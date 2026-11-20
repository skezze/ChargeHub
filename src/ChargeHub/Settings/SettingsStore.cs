using System.Text.Json;

namespace ChargeHub.Settings;

/// <summary>Loads and saves <see cref="AppSettings"/> as JSON; saving is atomic (temp file + replace).</summary>
internal sealed class SettingsStore(string path)
{
    static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public static SettingsStore Default { get; } = new(Path.Combine(Log.Directory, "settings.json"));

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(path)) return new AppSettings();
            var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path)) ?? new AppSettings();
            settings.Pinned = new HashSet<string>(settings.Pinned ?? [], StringComparer.Ordinal);
            settings.LowBatteryPercent = Math.Clamp(settings.LowBatteryPercent, 1, 99);
            if (!AppSettings.RefreshChoices.Contains(settings.RefreshIntervalSeconds))
                settings.RefreshIntervalSeconds = AppSettings.DefaultRefreshSeconds;
            if (!AppSettings.Languages.Contains(settings.Language ?? "")) settings.Language = "auto";
            return settings;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            Log.Error("Could not read settings, using defaults", e);
            return new AppSettings();
        }
    }

    /// <returns><c>false</c> if the file could not be written (the error is logged).</returns>
    public bool Save(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var temp = path + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(settings, Options));
            File.Move(temp, path, overwrite: true);
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Log.Error("Could not save settings", e);
            return false;
        }
    }
}
