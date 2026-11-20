using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace ChargeHub.Sources;

/// <summary>A raw Windows HID interface opened for asynchronous report I/O.</summary>
internal sealed class HidInterface : IDisposable
{
    const uint GenericRead = 0x80000000, GenericWrite = 0x40000000;
    const uint FileShareReadWrite = 3, OpenExisting = 3, FileFlagOverlapped = 0x40000000;

    readonly FileStream stream;

    HidInterface(FileStream stream, int inputReportLength, int outputReportLength)
    {
        this.stream = stream;
        InputReportLength = inputReportLength;
        OutputReportLength = outputReportLength;
    }

    public int InputReportLength { get; }

    public int OutputReportLength { get; }

    /// <exception cref="Win32Exception">The interface could not be opened (e.g. access denied, error 5).</exception>
    public static HidInterface Open(string devicePath)
    {
        var handle = CreateFileW(devicePath, GenericRead | GenericWrite, FileShareReadWrite, IntPtr.Zero, OpenExisting, FileFlagOverlapped, IntPtr.Zero);
        try
        {
            if (handle.IsInvalid) throw new Win32Exception(Marshal.GetLastWin32Error());
            if (!HidD_GetPreparsedData(handle, out var preparsed)) throw new Win32Exception(Marshal.GetLastWin32Error());

            int input, output;
            try
            {
                var caps = new byte[64]; // HIDP_CAPS
                if (HidP_GetCaps(preparsed, caps) < 0) throw new Win32Exception("HidP_GetCaps failed");
                input = BitConverter.ToUInt16(caps, 4);
                output = BitConverter.ToUInt16(caps, 6);
            }
            finally { HidD_FreePreparsedData(preparsed); }

            return new HidInterface(new FileStream(handle, FileAccess.ReadWrite, 0, isAsync: true), input, output);
        }
        catch
        {
            handle.Dispose();
            throw;
        }
    }

    public ValueTask WriteAsync(byte[] report, CancellationToken cancellation) => stream.WriteAsync(report, cancellation);

    public ValueTask<int> ReadAsync(byte[] buffer, CancellationToken cancellation) => stream.ReadAsync(buffer, cancellation);

    public void Dispose() => stream.Dispose();

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern SafeFileHandle CreateFileW(string path, uint access, uint share, IntPtr security, uint creation, uint flags, IntPtr template);

    [DllImport("hid.dll", SetLastError = true)]
    static extern bool HidD_GetPreparsedData(SafeFileHandle handle, out IntPtr preparsed);

    [DllImport("hid.dll")]
    static extern bool HidD_FreePreparsedData(IntPtr preparsed);

    [DllImport("hid.dll")]
    static extern int HidP_GetCaps(IntPtr preparsed, [Out] byte[] caps);
}
