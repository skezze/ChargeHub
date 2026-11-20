namespace ChargeHub.Core;

internal enum Transport { Bluetooth, BluetoothLE, Usb, Dongle }

/// <summary>Ordered from "worst" to "best"; used to pick the winner when two sources describe the same device.</summary>
internal enum LinkStatus { Disconnected, Unavailable, Asleep, Connected }

/// <summary>
/// What a single source observed during one poll. <see cref="Percent"/> is only
/// ever set for <see cref="LinkStatus.Connected"/> readings, so a stale value can
/// never be mistaken for a live one.
/// </summary>
internal sealed record DeviceReading(
    string Id, string Name, Transport Transport, LinkStatus Link, int? Percent = null, bool Charging = false)
{
    public static DeviceReading Connected(string id, string name, Transport transport, int? percent = null, bool charging = false) =>
        new(id, name, transport, LinkStatus.Connected, percent is >= 0 and <= 100 ? percent : null, charging);

    /// <summary>The device is reachable (receiver/radio present) but not talking: sleeping or switched off.</summary>
    public static DeviceReading Asleep(string id, string name, Transport transport) =>
        new(id, name, transport, LinkStatus.Asleep);

    public static DeviceReading Disconnected(string id, string name, Transport transport) =>
        new(id, name, transport, LinkStatus.Disconnected);

    /// <summary>The state could not be determined (access denied, driver error, source failure).</summary>
    public static DeviceReading Unavailable(string id, string name, Transport transport) =>
        new(id, name, transport, LinkStatus.Unavailable);
}

/// <summary>
/// A device as presented to the UI: the live reading plus the last percentage we
/// saw, which is only populated while there is no fresh <see cref="Percent"/>.
/// </summary>
internal sealed record Device(
    string Id, string Name, Transport Transport, LinkStatus Link, int? Percent, bool Charging,
    int? LastPercent = null, DateTimeOffset? LastPercentAt = null)
{
    public bool IsConnected => Link == LinkStatus.Connected;

    /// <summary>Fresh percentage if available, otherwise the last known one.</summary>
    public int? KnownPercent => Percent ?? LastPercent;

    public bool IsStale => Percent is null && LastPercent is not null;
}
