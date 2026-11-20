using ChargeHub.Core;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Enumeration;
using Windows.Storage.Streams;

namespace ChargeHub.Sources;

/// <summary>
/// Paired Bluetooth Classic and Bluetooth LE devices. Each poll enumerates the paired
/// devices and reads batteries of the connected ones: the standard GATT Battery Service
/// for LE, the Windows driver battery property otherwise. DeviceWatchers are used only to
/// notice connect/disconnect events early; they carry no state the poll depends on.
/// </summary>
internal sealed class BluetoothSource : IBatterySource, IDisposable
{
    const string ConnectedKey = "System.Devices.Aep.IsConnected";
    const string AddressKey = "System.Devices.Aep.DeviceAddress";
    static readonly string[] RequestedProperties = [ConnectedKey, AddressKey];
    static readonly TimeSpan GattTimeout = TimeSpan.FromSeconds(5);

    sealed record Endpoint(string Id, string Name, string? Address, bool Connected, Transport Transport)
    {
        public string Key => Address is { Length: > 0 } ? "bt:" + Address : "bt-id:" + Id;
    }

    sealed class WatcherTracker
    {
        public volatile bool Ready;
        public readonly Dictionary<string, bool> Connected = [];
    }

    readonly List<DeviceWatcher> watchers = [];
    readonly HashSet<string> withoutBatteryService = [];
    bool disposed;

    public BluetoothSource()
    {
        try
        {
            StartWatcher(BluetoothDevice.GetDeviceSelectorFromPairingState(true));
            StartWatcher(BluetoothLEDevice.GetDeviceSelectorFromPairingState(true));
        }
        catch (Exception e) { Log.Error("Bluetooth watcher could not start", e); }
    }

    public string Name => "Bluetooth";

    public event Action? Changed;

    void StartWatcher(string selector)
    {
        var watcher = DeviceInformation.CreateWatcher(selector, RequestedProperties, DeviceInformationKind.AssociationEndpoint);
        var tracker = new WatcherTracker();

        void Notify() { if (tracker.Ready && !disposed) Changed?.Invoke(); }

        watcher.Added += (_, info) =>
        {
            lock (tracker.Connected) tracker.Connected[info.Id] = IsConnected(info.Properties) ?? false;
            Notify();
        };
        watcher.Updated += (_, update) =>
        {
            // Updates carry only changed properties; react to connection changes alone,
            // otherwise our own GATT reads would keep re-triggering polls.
            if (IsConnected(update.Properties) is not bool connected) return;
            bool changed;
            lock (tracker.Connected)
            {
                changed = !tracker.Connected.TryGetValue(update.Id, out var previous) || previous != connected;
                tracker.Connected[update.Id] = connected;
            }
            if (changed) Notify();
        };
        watcher.Removed += (_, update) =>
        {
            lock (tracker.Connected) tracker.Connected.Remove(update.Id);
            Notify();
        };
        watcher.EnumerationCompleted += (_, _) => tracker.Ready = true;
        watcher.Stopped += (_, _) =>
        {
            if (!disposed && watcher.Status == DeviceWatcherStatus.Aborted)
                Log.Error("Bluetooth device watcher was aborted by Windows");
        };
        watchers.Add(watcher);
        watcher.Start();
    }

    static bool? IsConnected(IReadOnlyDictionary<string, object> properties) =>
        properties.TryGetValue(ConnectedKey, out var value) && value is bool connected ? connected : null;

    public async Task<IReadOnlyList<DeviceReading>> ReadAsync(CancellationToken cancellation)
    {
        var endpoints = new List<Endpoint>();
        endpoints.AddRange(await FindAsync(BluetoothDevice.GetDeviceSelectorFromPairingState(true), Transport.Bluetooth, cancellation));
        endpoints.AddRange(await FindAsync(BluetoothLEDevice.GetDeviceSelectorFromPairingState(true), Transport.BluetoothLE, cancellation));

        var driverBatteries = endpoints.Any(e => e.Connected)
            ? await Task.Run(BluetoothPnpBattery.ReadAll, cancellation)
            : [];

        // Classic and LE records of one physical device share an address.
        var devices = endpoints.GroupBy(e => e.Key).Select(group => ReadDeviceAsync(group.ToArray(), driverBatteries, cancellation));
        return await Task.WhenAll(devices);
    }

    static async Task<IEnumerable<Endpoint>> FindAsync(string selector, Transport transport, CancellationToken cancellation)
    {
        var infos = await DeviceInformation.FindAllAsync(selector, RequestedProperties, DeviceInformationKind.AssociationEndpoint).AsTask(cancellation);
        return infos.Select(info => new Endpoint(
            info.Id,
            string.IsNullOrWhiteSpace(info.Name) ? "Bluetooth device" : info.Name,
            info.Properties.TryGetValue(AddressKey, out var address) && address is string text && text.Length > 0
                ? text.Replace(":", "").ToUpperInvariant() : null,
            IsConnected(info.Properties) ?? false,
            transport));
    }

    async Task<DeviceReading> ReadDeviceAsync(Endpoint[] group, Dictionary<string, int> driverBatteries, CancellationToken cancellation)
    {
        var connectedEndpoints = group.Where(e => e.Connected).ToArray();
        var primary = connectedEndpoints.FirstOrDefault() ?? group[0];
        string name = group.Select(e => e.Name).FirstOrDefault(n => n != "Bluetooth device") ?? primary.Name;
        string id = KnownDevices.IsGladiusIiiAimpoint(name) ? KnownDevices.GladiusIiiAimpointId : primary.Key;

        if (connectedEndpoints.Length == 0)
            return DeviceReading.Disconnected(id, name, primary.Transport);

        int? percent = null;
        var lowEnergy = connectedEndpoints.FirstOrDefault(e => e.Transport == Transport.BluetoothLE);
        if (lowEnergy is not null) percent = await ReadGattBatteryAsync(lowEnergy.Id, cancellation);
        if (percent is null && primary.Address is not null && driverBatteries.TryGetValue(primary.Address, out var level)) percent = level;

        return DeviceReading.Connected(id, name, primary.Transport, percent);
    }

    async Task<int?> ReadGattBatteryAsync(string endpointId, CancellationToken cancellation)
    {
        lock (withoutBatteryService)
            if (withoutBatteryService.Contains(endpointId)) return null;

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
            timeout.CancelAfter(GattTimeout);
            using var device = await BluetoothLEDevice.FromIdAsync(endpointId).AsTask(timeout.Token);
            if (device is null || device.ConnectionStatus != BluetoothConnectionStatus.Connected) return null;

            var services = await device.GetGattServicesForUuidAsync(GattServiceUuids.Battery, BluetoothCacheMode.Cached).AsTask(timeout.Token);
            if (services.Status != GattCommunicationStatus.Success || services.Services.Count == 0)
            {
                services = await device.GetGattServicesForUuidAsync(GattServiceUuids.Battery, BluetoothCacheMode.Uncached).AsTask(timeout.Token);
                if (services.Status == GattCommunicationStatus.Success && services.Services.Count == 0)
                {
                    // The device answered and has no Battery Service: don't re-probe it every minute.
                    lock (withoutBatteryService) withoutBatteryService.Add(endpointId);
                    return null;
                }
            }
            if (services.Status != GattCommunicationStatus.Success) return null;

            try
            {
                foreach (var service in services.Services)
                {
                    var characteristics = await service.GetCharacteristicsForUuidAsync(GattCharacteristicUuids.BatteryLevel).AsTask(timeout.Token);
                    if (characteristics.Status != GattCommunicationStatus.Success) continue;
                    foreach (var characteristic in characteristics.Characteristics)
                    {
                        var read = await characteristic.ReadValueAsync(BluetoothCacheMode.Uncached).AsTask(timeout.Token);
                        if (read.Status != GattCommunicationStatus.Success || read.Value.Length < 1) continue;
                        using var reader = DataReader.FromBuffer(read.Value);
                        int value = reader.ReadByte();
                        if (value <= 100) return value;
                    }
                }
                return null;
            }
            finally { foreach (var service in services.Services) service.Dispose(); }
        }
        catch (OperationCanceledException) when (!cancellation.IsCancellationRequested) { return null; }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            Log.Error("GATT battery read failed", e);
            return null;
        }
    }

    public void Dispose()
    {
        disposed = true;
        foreach (var watcher in watchers)
        {
            try
            {
                if (watcher.Status is DeviceWatcherStatus.Started or DeviceWatcherStatus.EnumerationCompleted) watcher.Stop();
            }
            catch (Exception e) { Log.Error("Stopping Bluetooth watcher failed", e); }
        }
    }
}
