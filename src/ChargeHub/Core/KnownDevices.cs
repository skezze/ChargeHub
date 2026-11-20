namespace ChargeHub.Core;

/// <summary>
/// Devices that can be reached through more than one source (e.g. over Bluetooth
/// and over a 2.4 GHz dongle) share one identity so they appear as a single device.
/// </summary>
internal static class KnownDevices
{
    public const string GladiusIiiAimpointId = "asus:gladius-iii-aimpoint";
    public const string GladiusIiiAimpointName = "ROG Gladius III Wireless AimPoint";

    public static bool IsGladiusIiiAimpoint(string name) =>
        (name.Contains("GIII", StringComparison.OrdinalIgnoreCase) || name.Contains("GLADIUS III", StringComparison.OrdinalIgnoreCase))
        && name.Contains("AIMPOINT", StringComparison.OrdinalIgnoreCase);
}
