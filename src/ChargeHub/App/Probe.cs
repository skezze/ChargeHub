using System.Runtime.InteropServices;
using ChargeHub.Core;
using ChargeHub.Sources;

namespace ChargeHub;

/// <summary>
/// <c>ChargeHub.exe --probe</c>: reads every source once and prints the result to the
/// console. Useful for diagnosing hardware without the tray, and for bug reports.
/// </summary>
internal static class Probe
{
    public static async Task<int> RunAsync()
    {
        AttachConsole(-1); // parent process console; harmless if there is none
        var output = new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true };

        using var monitor = new BatteryMonitor([new BluetoothSource(), new AsusGladiusSource()]);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await monitor.PollAsync(timeout.Token);

        output.Write(DiagnosticReport.Build(monitor.Current, monitor.LastPollAt, includeIds: true));
        return 0;
    }

    [DllImport("kernel32.dll")]
    static extern bool AttachConsole(int processId);
}
