namespace ChargeHub.Core;

/// <summary>
/// Decides when to warn about a low battery: once per discharge cycle, only for
/// watched devices, and never because of a sleeping device's missing reading.
/// </summary>
internal sealed class LowBatteryNotifier
{
    const int ResetMargin = 5;
    readonly HashSet<string> alerted = [];

    public IReadOnlyList<Device> Evaluate(IReadOnlyList<Device> devices, IReadOnlySet<string> watched, int threshold)
    {
        var toAlert = new List<Device>();
        foreach (var device in devices)
        {
            if (!watched.Contains(device.Id)) { alerted.Remove(device.Id); continue; }
            if (device.Percent is not int percent) continue; // no fresh data: keep the current alert state

            if (percent <= threshold && !device.Charging)
            {
                if (alerted.Add(device.Id)) toAlert.Add(device);
            }
            else if (percent > threshold + ResetMargin || device.Charging)
            {
                alerted.Remove(device.Id);
            }
        }
        return toAlert;
    }
}
