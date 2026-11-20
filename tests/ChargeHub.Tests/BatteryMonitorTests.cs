using ChargeHub.Core;
using Xunit;

namespace ChargeHub.Tests;

public class BatteryMonitorTests
{
    sealed class FakeSource : IBatterySource
    {
        public Func<IReadOnlyList<DeviceReading>> Next { get; set; } = () => [];
        public string Name => "fake";
        public event Action? Changed;
        public void RaiseChanged() => Changed?.Invoke();
        public Task<IReadOnlyList<DeviceReading>> ReadAsync(CancellationToken cancellation) => Task.FromResult(Next());
    }

    static BatteryMonitor Create(FakeSource source) =>
        new([source], interval: TimeSpan.FromMinutes(10), debounce: TimeSpan.FromMilliseconds(20));

    [Fact]
    public async Task Publishes_only_when_the_device_list_changes()
    {
        var source = new FakeSource { Next = () => [DeviceReading.Connected("a", "A", Transport.Bluetooth, 50)] };
        using var monitor = Create(source);
        int updates = 0;
        monitor.Updated += _ => updates++;

        await monitor.PollAsync(CancellationToken.None);
        await monitor.PollAsync(CancellationToken.None);
        Assert.Equal(1, updates);

        source.Next = () => [DeviceReading.Connected("a", "A", Transport.Bluetooth, 49)];
        await monitor.PollAsync(CancellationToken.None);
        Assert.Equal(2, updates);
        Assert.Equal(49, Assert.Single(monitor.Current).Percent);
    }

    [Fact]
    public async Task Failing_source_degrades_its_devices_to_unavailable_without_throwing()
    {
        var source = new FakeSource { Next = () => [DeviceReading.Connected("a", "A", Transport.Bluetooth, 50)] };
        using var monitor = Create(source);
        await monitor.PollAsync(CancellationToken.None);

        source.Next = () => throw new InvalidOperationException("boom");
        await monitor.PollAsync(CancellationToken.None);

        var device = Assert.Single(monitor.Current);
        Assert.Equal(LinkStatus.Unavailable, device.Link);
        Assert.Null(device.Percent);
        Assert.Equal(50, device.LastPercent); // remembered, but flagged stale
    }

    [Fact]
    public async Task Source_change_notification_triggers_a_refresh()
    {
        var source = new FakeSource { Next = () => [DeviceReading.Connected("a", "A", Transport.Bluetooth, 50)] };
        using var monitor = Create(source);
        var first = new TaskCompletionSource();
        var second = new TaskCompletionSource();
        int count = 0;
        monitor.Updated += _ => { if (++count == 1) first.TrySetResult(); else second.TrySetResult(); };
        monitor.Start();
        await first.Task.WaitAsync(TimeSpan.FromSeconds(5));

        source.Next = () => [DeviceReading.Connected("a", "A", Transport.Bluetooth, 40)];
        for (int i = 0; i < 5; i++) source.RaiseChanged(); // a burst collapses into one poll
        await second.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(40, Assert.Single(monitor.Current).Percent);
    }
}
