using ChargeHub.Core;

namespace ChargeHub.Ui;

internal sealed partial class MainWindow
{
    /// <summary>Adds the Devices page content (coordinates are relative to the viewport top) and returns its height.</summary>
    int BuildDevices(int x, int width)
    {
        var list = devices();
        int y = 0, gap = S(10);

        if (list.Count == 0)
        {
            int height = S(150);
            Visuals.Add(new Visual(new Rectangle(x, y, width, height), true, (g, r, _) =>
            {
                FillRound(g, Theme.Card, r, S(8));
                DrawText(g, "\uE772", HugeIconFont, new Rectangle(r.X, r.Y + S(18), r.Width, S(44)), Theme.Muted, TextFormatFlags.HorizontalCenter);
                DrawText(g, Strings.NoDevicesTitle, NameFont, new Rectangle(r.X, r.Y + S(66), r.Width, S(24)), Theme.Text, TextFormatFlags.HorizontalCenter);
                DrawWrapped(g, Strings.NoDevicesHint, BodyFont, new Rectangle(r.X + S(40), r.Y + S(94), r.Width - S(80), S(44)), Theme.Muted);
            }));
            return y + height;
        }

        var live = list.Where(d => d.Link != LinkStatus.Disconnected).ToArray();
        var off = list.Where(d => d.Link == LinkStatus.Disconnected).ToArray();

        if (live.Length > 0)
        {
            AddSectionLabel(Strings.ConnectedGroup, x, y, width);
            y += S(32);
            foreach (var device in live) y = AddDeviceCard(x, y, width, S(96), device, compact: false) + gap;
        }
        if (off.Length > 0)
        {
            if (live.Length > 0) y += S(6);
            AddSectionLabel(Strings.NotConnectedGroup, x, y, width);
            y += S(32);
            foreach (var device in off) y = AddDeviceCard(x, y, width, S(64), device, compact: true) + gap;
        }

        y += S(4);
        Visuals.Add(new Visual(new Rectangle(x, y, width, S(24)), true,
            (g, r, _) => DrawText(g, Strings.PinHint, SmallFont, r, Theme.Muted)));
        return y + S(24);
    }

    int AddDeviceCard(int x, int y, int width, int height, Device device, bool compact)
    {
        Visuals.Add(new Visual(new Rectangle(x, y, width, height), true,
            (g, r, hot) => PaintDeviceCard(g, r, device, settings.Pinned.Contains(device.Id), hot, compact, large: true, settings.LowBatteryPercent),
            () =>
            {
                if (!settings.Pinned.Remove(device.Id)) settings.Pinned.Add(device.Id);
                Changed();
            }));
        return y + height;
    }
}
