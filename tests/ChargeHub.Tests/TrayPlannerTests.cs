using ChargeHub.Core;
using ChargeHub.Settings;
using ChargeHub.Tray;
using Xunit;

namespace ChargeHub.Tests;

public class TrayPlannerTests
{
    static Device Connected(string id, int percent, bool charging = false) =>
        new(id, id, Transport.Bluetooth, LinkStatus.Connected, percent, charging);

    static Device Of(string id, LinkStatus link, int? last = null) =>
        new(id, id, Transport.Dongle, link, null, false, last, last is null ? null : DateTimeOffset.Now);

    [Fact]
    public void Nothing_pinned_shows_only_the_hub_icon()
    {
        var specs = TrayPlanner.Plan([Connected("a", 50)], new AppSettings());
        var hub = Assert.Single(specs);
        Assert.Equal(TrayPlanner.HubKey, hub.Key);
        Assert.Equal(IconTone.Hub, hub.Glyph.Tone);
    }

    [Fact]
    public void Only_pinned_devices_get_icons_and_the_hub_disappears()
    {
        var settings = new AppSettings { Pinned = ["a"] };
        var specs = TrayPlanner.Plan([Connected("a", 50), Connected("b", 60)], settings);
        var spec = Assert.Single(specs);
        Assert.Equal("a", spec.Key);
        Assert.Equal(new IconGlyph("50", IconTone.Normal), spec.Glyph);
    }

    [Fact]
    public void Unpinning_everything_brings_back_exactly_the_hub()
    {
        var settings = new AppSettings { Pinned = ["a"] };
        var devices = new[] { Connected("a", 50) };
        Assert.Equal("a", Assert.Single(TrayPlanner.Plan(devices, settings)).Key);
        settings.Pinned.Clear();
        Assert.Equal(TrayPlanner.HubKey, Assert.Single(TrayPlanner.Plan(devices, settings)).Key);
    }

    [Fact]
    public void Disconnected_pinned_device_is_hidden_or_shown_by_setting()
    {
        var devices = new[] { Of("a", LinkStatus.Disconnected, 40) };
        var hidden = TrayPlanner.Plan(devices, new AppSettings { Pinned = ["a"], HideDisconnected = true });
        Assert.Equal(TrayPlanner.HubKey, Assert.Single(hidden).Key);

        var shown = TrayPlanner.Plan(devices, new AppSettings { Pinned = ["a"], HideDisconnected = false });
        Assert.Equal(new IconGlyph("40", IconTone.Muted), Assert.Single(shown).Glyph);
    }

    [Fact]
    public void Sleeping_device_stays_visible_with_a_dimmed_last_value()
    {
        var specs = TrayPlanner.Plan([Of("a", LinkStatus.Asleep, 7)], new AppSettings { Pinned = ["a"] });
        Assert.Equal(new IconGlyph("7", IconTone.Muted), Assert.Single(specs).Glyph);
    }

    [Fact]
    public void Device_without_any_reading_shows_a_dash()
    {
        var specs = TrayPlanner.Plan([Of("a", LinkStatus.Unavailable)], new AppSettings { Pinned = ["a"] });
        Assert.Equal(new IconGlyph(TrayPlanner.NoValue, IconTone.Muted), Assert.Single(specs).Glyph);
    }

    [Theory]
    [InlineData(80, false, "Normal")]
    [InlineData(20, false, "Low")]
    [InlineData(5, true, "Charging")]
    public void Tone_follows_level_and_charging(int percent, bool charging, string expected)
    {
        Assert.Equal(expected, TrayPlanner.Glyph(Connected("a", percent, charging), 20).Tone.ToString());
    }

    [Fact]
    public void Identical_input_produces_equal_specs_so_the_host_can_skip_updates()
    {
        var settings = new AppSettings { Pinned = ["a"] };
        var first = TrayPlanner.Plan([Connected("a", 50)], settings);
        var second = TrayPlanner.Plan([Connected("a", 50)], settings);
        Assert.Equal(first, second);
    }
}
