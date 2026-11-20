using ChargeHub.Core;
using Xunit;

namespace ChargeHub.Tests;

public class LowBatteryNotifierTests
{
    static readonly IReadOnlySet<string> Watched = new HashSet<string> { "a" };

    static Device Connected(int percent, bool charging = false) =>
        new("a", "A", Transport.Bluetooth, LinkStatus.Connected, percent, charging);

    static Device Asleep() => new("a", "A", Transport.Dongle, LinkStatus.Asleep, null, false, 15, DateTimeOffset.Now);

    [Fact]
    public void Alerts_once_per_discharge_cycle()
    {
        var notifier = new LowBatteryNotifier();
        Assert.Single(notifier.Evaluate([Connected(19)], Watched, 20));
        Assert.Empty(notifier.Evaluate([Connected(18)], Watched, 20));
        Assert.Empty(notifier.Evaluate([Connected(22)], Watched, 20)); // inside the hysteresis band
        Assert.Empty(notifier.Evaluate([Connected(19)], Watched, 20));
        notifier.Evaluate([Connected(60)], Watched, 20);               // recovered
        Assert.Single(notifier.Evaluate([Connected(20)], Watched, 20));
    }

    [Fact]
    public void Charging_device_is_not_alerted_and_resets_the_cycle()
    {
        var notifier = new LowBatteryNotifier();
        Assert.Empty(notifier.Evaluate([Connected(10, charging: true)], Watched, 20));
        Assert.Single(notifier.Evaluate([Connected(10)], Watched, 20));
        notifier.Evaluate([Connected(11, charging: true)], Watched, 20);
        Assert.Single(notifier.Evaluate([Connected(10)], Watched, 20));
    }

    [Fact]
    public void Sleeping_device_does_not_re_alert_on_every_wake_up()
    {
        var notifier = new LowBatteryNotifier();
        Assert.Single(notifier.Evaluate([Connected(15)], Watched, 20));
        Assert.Empty(notifier.Evaluate([Asleep()], Watched, 20));
        Assert.Empty(notifier.Evaluate([Connected(15)], Watched, 20));
    }

    [Fact]
    public void Unwatched_devices_are_ignored()
    {
        var notifier = new LowBatteryNotifier();
        Assert.Empty(notifier.Evaluate([Connected(5)], new HashSet<string>(), 20));
    }
}
