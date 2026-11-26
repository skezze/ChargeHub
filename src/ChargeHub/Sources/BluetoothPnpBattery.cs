using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using ChargeHub.Core;

namespace ChargeHub.Sources;

/// <summary>
/// Reads the battery level Windows' Bluetooth drivers publish on the device node
/// (DEVPKEY_Bluetooth_Battery). This is how classic Bluetooth headsets (HFP battery
/// indicator) and many HID-over-GATT devices report their charge.
/// </summary>
internal static partial class BluetoothPnpBattery
{
    const uint DigcfPresent = 0x2, DigcfAllClasses = 0x4;
    const uint DevPropTypeByte = 0x3, DevPropTypeUInt32 = 0x7;
    static readonly IntPtr InvalidHandle = new(-1);
    static readonly Guid BatteryKeyFormat = new("104ea319-6ee2-4701-bd47-8ddbf425bbe5");

    /// <summary>Battery percentages keyed by transport and upper-case address without separators.</summary>
    public static Dictionary<(Transport, string), int> ReadAll()
    {
        var result = new Dictionary<(Transport, string), int>();
        ReadEnumerator("BTHENUM", Transport.Bluetooth, result);
        ReadEnumerator("BTHLE", Transport.BluetoothLE, result);
        ReadEnumerator("BTHLEDEVICE", Transport.BluetoothLE, result);
        return result;
    }

    static void ReadEnumerator(string enumerator, Transport transport, Dictionary<(Transport, string), int> result)
    {
        var set = SetupDiGetClassDevsW(IntPtr.Zero, enumerator, IntPtr.Zero, DigcfAllClasses | DigcfPresent);
        if (set == InvalidHandle) return;
        try
        {
            var key = new PropertyKey { Format = BatteryKeyFormat, Id = 2 };
            var instanceId = new StringBuilder(512);
            var value = new byte[4];
            for (uint index = 0; ; index++)
            {
                var info = new DeviceInfoData { Size = (uint)Marshal.SizeOf<DeviceInfoData>() };
                if (!SetupDiEnumDeviceInfo(set, index, ref info)) break;
                if (!SetupDiGetDevicePropertyW(set, ref info, ref key, out var type, value, (uint)value.Length, out var required, 0)) continue;

                int level = type == DevPropTypeByte && required >= 1 ? value[0]
                    : type == DevPropTypeUInt32 && required >= 4 ? BitConverter.ToInt32(value)
                    : -1;
                if (level is < 0 or > 100) continue;

                instanceId.Clear();
                if (!SetupDiGetDeviceInstanceIdW(set, ref info, instanceId, (uint)instanceId.Capacity, out _)) continue;
                var match = AddressInInstanceId().Match(instanceId.ToString());
                if (!match.Success) continue;
                var device = (transport, match.Groups[1].Value.ToUpperInvariant());
                // Prefer the remote device node over a profile/service child when both report charge.
                if (instanceId.ToString().StartsWith(enumerator + @"\DEV_", StringComparison.OrdinalIgnoreCase))
                    result[device] = level;
                else
                    result.TryAdd(device, level);
            }
        }
        finally { SetupDiDestroyDeviceInfoList(set); }
    }

    // BTHLE\DEV_D4E4F2F3A5B6\...  and  BTHENUM\{uuid}_VID&..._PID&...\7&1A2B3C4D&0&D4E4F2F3A5B6_C00000000
    [GeneratedRegex(@"(?:DEV_|&)([0-9A-F]{12})(?:_|\\|$)", RegexOptions.IgnoreCase)]
    private static partial Regex AddressInInstanceId();

    [StructLayout(LayoutKind.Sequential)]
    struct DeviceInfoData { public uint Size; public Guid ClassGuid; public uint DevInst; public UIntPtr Reserved; }

    [StructLayout(LayoutKind.Sequential)]
    struct PropertyKey { public Guid Format; public uint Id; }

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern IntPtr SetupDiGetClassDevsW(IntPtr classGuid, string? enumerator, IntPtr parent, uint flags);

    [DllImport("setupapi.dll", SetLastError = true)]
    static extern bool SetupDiEnumDeviceInfo(IntPtr set, uint index, ref DeviceInfoData info);

    [DllImport("setupapi.dll", SetLastError = true)]
    static extern bool SetupDiGetDevicePropertyW(IntPtr set, ref DeviceInfoData info, ref PropertyKey key, out uint type, byte[] buffer, uint size, out uint required, uint flags);

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern bool SetupDiGetDeviceInstanceIdW(IntPtr set, ref DeviceInfoData info, StringBuilder id, uint size, out uint required);

    [DllImport("setupapi.dll")]
    static extern bool SetupDiDestroyDeviceInfoList(IntPtr set);
}
