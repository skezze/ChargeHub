using ChargeHub.Core;
using ChargeHub.Settings;
using ChargeHub.Updates;

namespace ChargeHub.Tray;

/// <summary>The tray context menu. It is rebuilt every time it opens, so it always reflects the current devices.</summary>
internal sealed class TrayMenu : IDisposable
{
    readonly ContextMenuStrip strip = new() { ShowItemToolTips = false };
    readonly Font boldFont;
    readonly AppSettings settings;
    readonly Func<IReadOnlyList<Device>> devices;
    readonly Func<ReleaseInfo?> update;

    public TrayMenu(AppSettings settings, Func<IReadOnlyList<Device>> devices, Func<ReleaseInfo?> update)
    {
        this.settings = settings;
        boldFont = new Font(strip.Font, FontStyle.Bold);
        this.devices = devices;
        this.update = update;
        strip.Opening += (_, _) => Rebuild();
    }

    public ContextMenuStrip Strip => strip;

    /// <summary>A setting or pinned device changed; the owner should persist and re-plan the tray.</summary>
    public event Action? SettingsChanged;

    public event Action? OpenRequested;

    public event Action? UpdateRequested;

    public event Action? RefreshRequested;

    public event Action? ExitRequested;

    void Rebuild()
    {
        foreach (ToolStripItem item in strip.Items) item.Dispose();
        strip.Items.Clear();

        var all = devices();
        var connected = all.Where(d => d.Link != LinkStatus.Disconnected).ToArray();
        var disconnected = all.Where(d => d.Link == LinkStatus.Disconnected).ToArray();

        strip.Items.Add(new ToolStripMenuItem(Strings.OpenApp, null, (_, _) => OpenRequested?.Invoke()) { Font = boldFont });
        if (update() is { } release)
            strip.Items.Add(new ToolStripMenuItem(Strings.MenuUpdate(release.Version), null, (_, _) => UpdateRequested?.Invoke()));
        strip.Items.Add(new ToolStripSeparator());

        foreach (var device in connected) strip.Items.Add(DeviceItem(device));
        if (disconnected.Length > 0)
        {
            var group = new ToolStripMenuItem(Strings.NotConnectedGroup);
            foreach (var device in disconnected) group.DropDownItems.Add(DeviceItem(device));
            strip.Items.Add(group);
        }
        if (all.Count == 0) strip.Items.Add(new ToolStripMenuItem(Strings.NoDevices) { Enabled = false });

        strip.Items.Add(new ToolStripSeparator());
        strip.Items.Add(new ToolStripMenuItem(Strings.Refresh, null, (_, _) => RefreshRequested?.Invoke()));
        strip.Items.Add(Toggle(Strings.HideDisconnected, settings.HideDisconnected, v => settings.HideDisconnected = v));
        strip.Items.Add(Toggle(Strings.LowBatteryNotifications, settings.LowBatteryNotifications, v => settings.LowBatteryNotifications = v));
        strip.Items.Add(StartupItem());
        strip.Items.Add(new ToolStripSeparator());
        strip.Items.Add(new ToolStripMenuItem(Strings.Exit, null, (_, _) => ExitRequested?.Invoke()));
    }

    ToolStripMenuItem DeviceItem(Device device)
    {
        bool pinned = settings.Pinned.Contains(device.Id);
        return new ToolStripMenuItem(Strings.MenuLabel(device), null, (_, _) =>
        {
            if (pinned) settings.Pinned.Remove(device.Id);
            else settings.Pinned.Add(device.Id);
            SettingsChanged?.Invoke();
        })
        { Checked = pinned };
    }

    ToolStripMenuItem Toggle(string text, bool value, Action<bool> apply) =>
        new(text, null, (_, _) => { apply(!value); SettingsChanged?.Invoke(); }) { Checked = value };

    static ToolStripMenuItem StartupItem()
    {
        bool enabled = StartupRegistration.IsEnabled;
        return new ToolStripMenuItem(Strings.StartWithWindows, null, (_, _) =>
        {
            if (!StartupRegistration.TrySet(!enabled, out var error))
                MessageBox.Show(Strings.StartupFailed(error!), "ChargeHub", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        })
        { Checked = enabled };
    }

    public void Dispose()
    {
        strip.Dispose();
        boldFont.Dispose();
    }
}
