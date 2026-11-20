namespace ChargeHub.Sources;

internal enum AsusResponseKind { Unrelated, Battery, Asleep }

internal readonly record struct AsusResponse(AsusResponseKind Kind, int Percent = 0, bool Charging = false);

/// <summary>
/// The ASUS ROG Gladius III Wireless AimPoint battery query. Only the read-only battery
/// request (<c>00 12 07</c>) is ever sent; no DPI, lighting, firmware or profile data is written.
/// </summary>
internal static class AsusProtocol
{
    public const int ReportLength = 65;

    public const string WiredPidFragment = "vid_0b05&pid_1a70&mi_00";
    public const string DonglePidFragment = "vid_0b05&pid_1a72&mi_00";

    public static byte[] BatteryRequest()
    {
        var request = new byte[ReportLength];
        request[1] = 0x12;
        request[2] = 0x07;
        return request;
    }

    public static AsusResponse Parse(ReadOnlySpan<byte> packet)
    {
        if (packet.Length < 11) return new(AsusResponseKind.Unrelated);
        if (packet[1] == 0xff && packet[2] == 0xaa) return new(AsusResponseKind.Asleep);
        if (packet[0] != 0 || packet[1] != 0x12 || packet[2] != 0x07) return new(AsusResponseKind.Unrelated);
        if (packet[5] > 100) return new(AsusResponseKind.Unrelated);
        // The receiver answers with an all-zero payload when the mouse is not talking.
        if (packet[5] == 0 && packet[10] == 0) return new(AsusResponseKind.Asleep);
        return new(AsusResponseKind.Battery, packet[5], packet[10] > 0);
    }

    public static bool IsSupportedInterface(string devicePath) =>
        devicePath.Contains(WiredPidFragment, StringComparison.OrdinalIgnoreCase)
        || devicePath.Contains(DonglePidFragment, StringComparison.OrdinalIgnoreCase);

    public static bool IsWired(string devicePath) =>
        devicePath.Contains(WiredPidFragment, StringComparison.OrdinalIgnoreCase);
}
