using Xunit;
using ChargeHub.Settings;

namespace ChargeHub.Tests;

public sealed class SettingsStoreTests : IDisposable
{
    readonly string folder = Path.Combine(Path.GetTempPath(), "ChargeHubTests-" + Guid.NewGuid().ToString("N"));

    string FilePath => Path.Combine(folder, "settings.json");

    public void Dispose()
    {
        if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
    }

    [Fact]
    public void Missing_file_gives_defaults()
    {
        var settings = new SettingsStore(FilePath).Load();

        Assert.Equal(60, settings.RefreshIntervalSeconds);
        Assert.Equal("auto", settings.Language);
        Assert.Empty(settings.Pinned);
    }

    [Fact]
    public void Settings_survive_a_round_trip()
    {
        var store = new SettingsStore(FilePath);
        Assert.True(store.Save(new AppSettings
        {
            Pinned = ["a", "b"], HideDisconnected = false, LowBatteryPercent = 35, RefreshIntervalSeconds = 300, Language = "ru",
        }));

        var loaded = store.Load();

        Assert.Equal(["a", "b"], loaded.Pinned.Order());
        Assert.False(loaded.HideDisconnected);
        Assert.Equal(35, loaded.LowBatteryPercent);
        Assert.Equal(300, loaded.RefreshIntervalSeconds);
        Assert.Equal("ru", loaded.Language);
    }

    [Fact]
    public void Out_of_range_values_are_replaced_on_load()
    {
        Directory.CreateDirectory(folder);
        File.WriteAllText(FilePath, """{ "LowBatteryPercent": 500, "RefreshIntervalSeconds": 1, "Language": "xx" }""");

        var loaded = new SettingsStore(FilePath).Load();

        Assert.Equal(99, loaded.LowBatteryPercent);
        Assert.Equal(60, loaded.RefreshIntervalSeconds);
        Assert.Equal("auto", loaded.Language);
    }

    [Fact]
    public void Corrupt_file_gives_defaults()
    {
        Directory.CreateDirectory(folder);
        File.WriteAllText(FilePath, "{ not json");

        Assert.Equal(60, new SettingsStore(FilePath).Load().RefreshIntervalSeconds);
    }
}
