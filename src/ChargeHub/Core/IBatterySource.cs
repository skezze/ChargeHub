namespace ChargeHub.Core;

/// <summary>
/// A provider of device battery readings (Bluetooth, a vendor dongle protocol, ...).
/// Sources are stateless from the caller's point of view: every call returns the
/// complete set of devices the source currently knows about.
/// </summary>
internal interface IBatterySource
{
    string Name { get; }

    /// <summary>Raised (on any thread) when the source notices a hardware change worth re-polling for.</summary>
    event Action? Changed;

    Task<IReadOnlyList<DeviceReading>> ReadAsync(CancellationToken cancellation);
}
