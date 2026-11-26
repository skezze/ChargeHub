using ChargeHub.Core;
using ChargeHub.Settings;
using ChargeHub.Sources;
using ChargeHub.Tray;
using ChargeHub.Ui;
using ChargeHub.Updates;
using Microsoft.Win32;

namespace ChargeHub;

/// <summary>
/// Wires the layers together: sources → monitor → (UI thread) → planner → tray host.
/// The tray icon is the app's home; the quick panel and the main window are opened from it.
/// </summary>
internal sealed class TrayApp : ApplicationContext
{
    readonly SettingsStore store = SettingsStore.Default;
    readonly AppSettings settings;
    readonly MessageWindow window = new();
    readonly BatteryMonitor monitor;
    readonly TrayMenu menu;
    readonly TrayIconHost tray;
    readonly FlyoutWindow flyout;
    readonly MainWindow main;
    readonly LowBatteryNotifier notifier = new();
    readonly UpdateService updates;
    IReadOnlyList<Device> devices = [];
    bool disposed;

    /// <param name="showWindow">Open the main window right away (a manual start); autostart stays in the tray.</param>
    public TrayApp(bool showWindow)
    {
        settings = store.Load();
        Strings.Apply(settings.Language);
        StartupRegistration.UpgradeLegacyCommand();

        monitor = new BatteryMonitor([new BluetoothSource(), new AsusGladiusSource()], TimeSpan.FromSeconds(settings.RefreshIntervalSeconds));
        updates = new UpdateService(settings, new UpdateChecker(UpdateChecker.CreateClient(), AppInfo.Repository), AppInfo.CurrentVersion,
            persist: () => window.Post(() => store.Save(settings)));
        menu = new TrayMenu(settings, () => devices, () => updates.Available);
        tray = new TrayIconHost(menu.Strip);
        flyout = new FlyoutWindow(settings, () => devices);
        main = new MainWindow(settings, () => devices, () => monitor.LastPollAt, updates);

        tray.LeftClicked += ToggleFlyout;
        tray.DoubleClicked += ShowMainWindow;
        flyout.SettingsChanged += OnSettingsChanged;
        flyout.RefreshRequested += () => monitor.RequestRefresh();
        flyout.OpenRequested += ShowMainWindow;
        flyout.ExitRequested += ExitThread;
        main.SettingsChanged += OnSettingsChanged;
        main.RefreshRequested += () => monitor.RequestRefresh();
        main.ExitRequested += ExitThread;

        menu.SettingsChanged += OnSettingsChanged;
        menu.OpenRequested += ShowMainWindow;
        menu.UpdateRequested += () => { flyout.Hide(); main.ShowAbout(); };
        menu.RefreshRequested += () => monitor.RequestRefresh();
        menu.ExitRequested += ExitThread;
        window.HardwareChanged += () => monitor.RequestRefresh();
        window.ResumedFromSleep += OnResumed;
        monitor.Updated += list => window.Post(() => OnDevicesUpdated(list));
        monitor.Polled += () => window.Post(main.RefreshContent);
        updates.Changed += () => window.Post(main.RefreshContent);
        updates.NewReleaseFound += release => window.Post(() => tray.ShowBalloon(Strings.UpdateAvailableTitle, Strings.UpdateAvailableText(release.Version), ToolTipIcon.Info));
        SystemEvents.UserPreferenceChanged += OnAppearanceChanged;
        SystemEvents.DisplaySettingsChanged += OnAppearanceChanged;

        tray.Sync(TrayPlanner.Plan(devices, settings)); // hub icon until the first poll completes
        monitor.Start();
        updates.Start();
        if (showWindow) window.Post(ShowMainWindow);
    }

    /// <summary>Opens the main window; safe to call from any thread (used when a second instance is started).</summary>
    public void RequestShowWindow() => window.Post(ShowMainWindow);

    void ShowMainWindow()
    {
        if (disposed) return;
        flyout.Hide();
        main.ShowWindow();
    }

    void ToggleFlyout()
    {
        if (flyout.Visible) { flyout.Hide(); return; }
        // The click on the tray icon first deactivates (and hides) the panel; don't reopen it for that same click.
        if (DateTime.UtcNow - flyout.LastHiddenUtc < TimeSpan.FromMilliseconds(300)) return;
        flyout.ShowNearTray();
    }

    void OnDevicesUpdated(IReadOnlyList<Device> list)
    {
        if (disposed) return;
        devices = list;
        tray.Sync(TrayPlanner.Plan(devices, settings));
        flyout.RefreshContent();
        main.RefreshContent();

        if (!settings.LowBatteryNotifications) return;
        foreach (var device in notifier.Evaluate(devices, settings.Pinned, settings.LowBatteryPercent))
            tray.ShowBalloon(Strings.LowBatteryTitle, $"{device.Name}: {device.Percent}%");
    }

    void OnSettingsChanged()
    {
        Strings.Apply(settings.Language);
        var interval = TimeSpan.FromSeconds(settings.RefreshIntervalSeconds);
        if (monitor.Interval != interval)
        {
            monitor.Interval = interval;
            monitor.RequestRefresh(); // wakes the loop so the new interval applies at once
        }
        store.Save(settings);
        _ = updates.CheckIfDueAsync(); // e.g. the user just turned automatic checks on
        tray.Sync(TrayPlanner.Plan(devices, settings));
        flyout.RefreshContent();
        main.RefreshContent();
    }

    void OnResumed()
    {
        // Bluetooth and USB devices need a few seconds to come back after sleep.
        monitor.RequestRefresh(TimeSpan.FromSeconds(5));
        monitor.RequestRefresh(TimeSpan.FromSeconds(20));
    }

    // Raised on the SystemEvents thread.
    void OnAppearanceChanged(object? sender, EventArgs e) => window.Post(() =>
    {
        tray.RefreshAppearance();
        main.ReloadTheme();
    });

    protected override void Dispose(bool disposing)
    {
        if (disposing && !disposed)
        {
            disposed = true;
            SystemEvents.UserPreferenceChanged -= OnAppearanceChanged;
            SystemEvents.DisplaySettingsChanged -= OnAppearanceChanged;
            updates.Dispose();
            monitor.Dispose();
            tray.Dispose();
            menu.Dispose();
            flyout.Dispose();
            main.Dispose();
            window.Dispose();
        }
        base.Dispose(disposing);
    }
}
