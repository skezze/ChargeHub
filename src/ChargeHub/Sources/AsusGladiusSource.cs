using System.ComponentModel;
using ChargeHub.Core;
using Windows.Devices.Enumeration;

namespace ChargeHub.Sources;

/// <summary>
/// ROG Gladius III Wireless AimPoint over its 2.4 GHz receiver (0B05:1A72) or the USB cable
/// (0B05:1A70). A receiver alone is not proof that the mouse is awake: a silent mouse is
/// reported as asleep, and the last known percentage is kept by <see cref="DeviceBook"/>.
/// Bluetooth mode is covered by <see cref="BluetoothSource"/> as a separate device entry.
/// </summary>
internal sealed class AsusGladiusSource : IBatterySource
{
    const string HidSelector =
        "System.Devices.InterfaceClassGuid:=\"{4D1E55B2-F16F-11CF-88CB-001111000030}\" AND System.Devices.InterfaceEnabled:=System.StructuredQueryType.Boolean#True";
    const string Id = KnownDevices.GladiusIiiAimpointId;
    const string DeviceName = KnownDevices.GladiusIiiAimpointName;
    static readonly TimeSpan ResponseTimeout = TimeSpan.FromMilliseconds(1500);
    const int MaxReadsPerQuery = 4;

    readonly HashSet<Transport> seenTransports = [];
    string? lastError;

    public string Name => "ASUS";

    // Never raised: USB plug/unplug reaches the monitor through WM_DEVICECHANGE (see MessageWindow).
    public event Action? Changed { add { } remove { } }

    public async Task<IReadOnlyList<DeviceReading>> ReadAsync(CancellationToken cancellation)
    {
        var interfaces = await DeviceInformation.FindAllAsync(HidSelector, null, DeviceInformationKind.DeviceInterface).AsTask(cancellation);
        var paths = interfaces
            .Where(i => i.IsEnabled && AsusProtocol.IsSupportedInterface(i.Id))
            .Select(i => i.Id)
            .ToLookup(path => AsusProtocol.IsWired(path) ? Transport.Usb : Transport.Dongle);

        var readings = new List<DeviceReading>();
        foreach (var transport in new[] { Transport.Usb, Transport.Dongle })
        {
            DeviceReading? best = null;
            foreach (var path in paths[transport])
            {
                var reading = await QueryAsync(path, transport, cancellation);
                if (best is null || reading.Link > best.Link) best = reading;
                if (reading.Link == LinkStatus.Connected) break;
            }
            if (best is not null)
            {
                seenTransports.Add(transport);
                readings.Add(best);
            }
            else if (seenTransports.Contains(transport))
                readings.Add(DeviceReading.Disconnected(DeviceId(transport), DeviceName, transport));
        }
        return readings;
    }

    static string DeviceId(Transport transport) => $"{Id}:{transport}";

    async Task<DeviceReading> QueryAsync(string path, Transport transport, CancellationToken cancellation)
    {
        try
        {
            using var hid = HidInterface.Open(path);
            if (hid.InputReportLength != AsusProtocol.ReportLength || hid.OutputReportLength != AsusProtocol.ReportLength)
                return Fail($"unexpected report sizes {hid.InputReportLength}/{hid.OutputReportLength}", transport);

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
            timeout.CancelAfter(ResponseTimeout);
            await hid.WriteAsync(AsusProtocol.BatteryRequest(), timeout.Token);

            var buffer = new byte[AsusProtocol.ReportLength];
            for (int attempt = 0; attempt < MaxReadsPerQuery; attempt++)
            {
                int count = await hid.ReadAsync(buffer, timeout.Token);
                var response = AsusProtocol.Parse(buffer.AsSpan(0, count));
                switch (response.Kind)
                {
                    case AsusResponseKind.Battery:
                        lastError = null;
                        return DeviceReading.Connected(DeviceId(transport), DeviceName, transport, response.Percent, response.Charging);
                    case AsusResponseKind.Asleep:
                        lastError = null;
                        return DeviceReading.Asleep(DeviceId(transport), DeviceName, transport);
                }
            }
            return DeviceReading.Asleep(DeviceId(transport), DeviceName, transport);
        }
        catch (OperationCanceledException) when (!cancellation.IsCancellationRequested)
        {
            // The receiver is there but the mouse did not answer in time: it is asleep or out of range.
            return DeviceReading.Asleep(DeviceId(transport), DeviceName, transport);
        }
        catch (Win32Exception e)
        {
            return Fail($"Win32 error {e.NativeErrorCode}", transport, e);
        }
        catch (IOException e)
        {
            return Fail("I/O error", transport, e);
        }
    }

    DeviceReading Fail(string reason, Transport transport, Exception? error = null)
    {
        // Persistent failures would otherwise log once per poll.
        if (lastError != reason)
        {
            lastError = reason;
            Log.Error($"ASUS HID query failed: {reason}", error);
        }
        return DeviceReading.Unavailable(DeviceId(transport), DeviceName, transport);
    }
}
