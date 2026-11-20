using Xunit;
using ChargeHub.Core;

namespace ChargeHub.Tests;

public class DiagnosticReportTests
{
    static Device[] Sample() =>
    [
        new("bt-AA:BB", "Headset", Transport.Bluetooth, LinkStatus.Connected, 80, true),
        new("usb-1", "Mouse", Transport.Dongle, LinkStatus.Asleep, null, false, 42, DateTimeOffset.Now),
    ];

    [Fact]
    public void Report_lists_devices_and_hides_ids_by_default()
    {
        var report = DiagnosticReport.Build(Sample(), DateTimeOffset.Now, includeIds: false);

        Assert.Contains("Devices: 2", report);
        Assert.Contains("Headset | Bluetooth | Connected | 80% charging", report);
        Assert.Contains("Mouse | Dongle | Asleep | no reading | last 42%", report);
        Assert.DoesNotContain("AA:BB", report);
    }

    [Fact]
    public void Report_can_include_ids_for_the_probe()
    {
        Assert.Contains("bt-AA:BB", DiagnosticReport.Build(Sample(), null, includeIds: true));
    }

    [Fact]
    public void Report_without_a_poll_says_never()
    {
        Assert.Contains("Last poll: never", DiagnosticReport.Build([], null, includeIds: false));
    }
}
