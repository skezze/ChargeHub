using ChargeHub.Core;
using ChargeHub.Settings;

namespace ChargeHub.Tray;

internal enum IconTone { Normal, Low, Charging, Muted, Hub }

/// <summary>What an icon looks like: a short label (percentage) and a colour tone. Empty label + Hub tone = app icon.</summary>
internal readonly record struct IconGlyph(string Label, IconTone Tone);

/// <summary>A tray icon that should exist. Equal specs mean "nothing to update".</summary>
internal sealed record TrayIconSpec(string Key, IconGlyph Glyph, string Tooltip);

/// <summary>
/// Turns the device list and the settings into the exact set of tray icons that should be
/// visible. Pure function: the host only has to make reality match this plan.
/// </summary>
internal static class TrayPlanner
{
    public const string HubKey = "hub";

    /// <summary>Label of an icon that has no battery information at all.</summary>
    public const string NoValue = "–";

    public static IReadOnlyList<TrayIconSpec> Plan(IReadOnlyList<Device> devices, AppSettings settings)
    {
        var specs = devices
            .Where(d => settings.Pinned.Contains(d.Id) && (d.Link != LinkStatus.Disconnected || !settings.HideDisconnected))
            .Select(d => new TrayIconSpec(d.Id, Glyph(d, settings.LowBatteryPercent), Strings.Tooltip(d)))
            .ToList();

        // The tray always keeps one icon so the menu stays reachable.
        if (specs.Count == 0)
            specs.Add(new TrayIconSpec(HubKey, new IconGlyph("", IconTone.Hub), Strings.HubTooltip));
        return specs;
    }

    internal static IconGlyph Glyph(Device device, int lowThreshold)
    {
        if (device.Percent is int percent)
        {
            var tone = device.Charging ? IconTone.Charging : percent <= lowThreshold ? IconTone.Low : IconTone.Normal;
            return new IconGlyph(percent.ToString(), tone);
        }
        // No fresh reading: show what we last knew, dimmed, or a dash.
        return new IconGlyph(device.LastPercent?.ToString() ?? NoValue, IconTone.Muted);
    }
}
