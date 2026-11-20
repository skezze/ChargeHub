using ChargeHub.Core;
using Xunit;

namespace ChargeHub.Tests;

public class DeviceBookTests
{
    static readonly DateTimeOffset T0 = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Connected_reading_keeps_percent_and_has_no_stale_value()
    {
        var book = new DeviceBook();
        var device = Assert.Single(book.Update([DeviceReading.Connected("a", "Mouse", Transport.Dongle, 70)], T0));
        Assert.Equal(70, device.Percent);
        Assert.Null(device.LastPercent);
        Assert.False(device.IsStale);
    }

    [Fact]
    public void Sleeping_device_exposes_last_known_percent_as_stale_not_as_live()
    {
        var book = new DeviceBook();
        book.Update([DeviceReading.Connected("a", "Mouse", Transport.Dongle, 70)], T0);
        var device = Assert.Single(book.Update([DeviceReading.Asleep("a", "Mouse", Transport.Dongle)], T0.AddMinutes(5)));

        Assert.Null(device.Percent);
        Assert.Equal(70, device.LastPercent);
        Assert.Equal(T0, device.LastPercentAt);
        Assert.True(device.IsStale);
        Assert.Equal(LinkStatus.Asleep, device.Link);
    }

    [Fact]
    public void Last_seen_time_tracks_the_latest_connected_reading()
    {
        var book = new DeviceBook();
        book.Update([DeviceReading.Connected("a", "Mouse", Transport.Dongle, 70)], T0);
        book.Update([DeviceReading.Connected("a", "Mouse", Transport.Dongle, 69)], T0.AddMinutes(1));
        var device = Assert.Single(book.Update([DeviceReading.Disconnected("a", "Mouse", Transport.Dongle)], T0.AddMinutes(2)));

        Assert.Equal(69, device.LastPercent);
        Assert.Equal(T0.AddMinutes(1), device.LastPercentAt);
    }

    [Fact]
    public void Same_device_from_two_sources_is_merged_preferring_live_data()
    {
        var book = new DeviceBook();
        var devices = book.Update(
        [
            DeviceReading.Connected("x", "GIII AIMPOINT", Transport.BluetoothLE),
            DeviceReading.Asleep("x", "ROG Gladius", Transport.Dongle),
            DeviceReading.Connected("x", "ROG Gladius", Transport.Dongle, 55),
        ], T0);

        var device = Assert.Single(devices);
        Assert.Equal(55, device.Percent);
        Assert.Equal(Transport.Dongle, device.Transport);
    }

    [Fact]
    public void Asleep_beats_disconnected_when_merging()
    {
        var book = new DeviceBook();
        var device = Assert.Single(book.Update(
        [
            DeviceReading.Disconnected("x", "Mouse", Transport.BluetoothLE),
            DeviceReading.Asleep("x", "Mouse", Transport.Dongle),
        ], T0));
        Assert.Equal(LinkStatus.Asleep, device.Link);
    }

    [Fact]
    public void Devices_are_sorted_connected_first_then_by_name()
    {
        var book = new DeviceBook();
        var names = book.Update(
        [
            DeviceReading.Disconnected("1", "Alpha", Transport.Bluetooth),
            DeviceReading.Connected("2", "Zulu", Transport.Bluetooth, 10),
            DeviceReading.Connected("3", "Bravo", Transport.Bluetooth, 20),
        ], T0).Select(d => d.Name);
        Assert.Equal(["Bravo", "Zulu", "Alpha"], names);
    }

    [Fact]
    public void Out_of_range_percent_is_dropped()
    {
        Assert.Null(DeviceReading.Connected("a", "A", Transport.Bluetooth, 101).Percent);
        Assert.Null(DeviceReading.Connected("a", "A", Transport.Bluetooth, -1).Percent);
        Assert.Equal(0, DeviceReading.Connected("a", "A", Transport.Bluetooth, 0).Percent);
    }
}
