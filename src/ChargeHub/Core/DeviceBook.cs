namespace ChargeHub.Core;

/// <summary>
/// Merges readings from all sources into one list of devices and remembers the
/// last percentage of every device so it can be shown (marked stale) while the
/// device sleeps or is temporarily unreachable. Not thread-safe; owned by the monitor loop.
/// </summary>
internal sealed class DeviceBook
{
    readonly Dictionary<string, (int Percent, DateTimeOffset At)> lastSeen = [];

    public IReadOnlyList<Device> Update(IEnumerable<DeviceReading> readings, DateTimeOffset now)
    {
        var devices = new List<Device>();
        foreach (var group in readings.GroupBy(r => r.Id))
        {
            var reading = group.MaxBy(Rank)!;
            if (reading.Percent is int percent)
            {
                lastSeen[reading.Id] = (percent, now);
                devices.Add(new Device(reading.Id, reading.Name, reading.Transport, reading.Link, percent, reading.Charging));
            }
            else
            {
                lastSeen.TryGetValue(reading.Id, out var last);
                devices.Add(new Device(reading.Id, reading.Name, reading.Transport, reading.Link, null, reading.Charging,
                    last == default ? null : last.Percent, last == default ? null : last.At));
            }
        }
        return devices
            .OrderByDescending(d => d.Link)
            .ThenBy(d => d.Name, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(d => d.Id, StringComparer.Ordinal)
            .ToArray();
    }

    // A connected reading with a number beats a connected one without, which beats asleep, and so on.
    static int Rank(DeviceReading r) => (int)r.Link * 2 + (r.Percent.HasValue ? 1 : 0);
}
