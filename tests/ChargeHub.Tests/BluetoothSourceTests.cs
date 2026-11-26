using ChargeHub.Core;
using ChargeHub.Sources;
using Xunit;

namespace ChargeHub.Tests;

public class BluetoothSourceTests
{
    [Theory]
    [InlineData("01-23-45-67-89-ab", true)]
    [InlineData("01:23:45:67:89:AC", false)]
    [InlineData(null, false)]
    public void Classic_and_Le_share_an_identity_only_with_the_same_address(string? leAddress, bool same)
    {
        var classicAddress = leAddress is null ? null : BluetoothSource.NormalizeAddress("01:23:45:67:89:AB");
        var classic = new BluetoothSource.Endpoint("endpoint", "Headphones", classicAddress, true, Transport.Bluetooth);
        var le = new BluetoothSource.Endpoint("endpoint", "Headphones",
            leAddress is null ? null : BluetoothSource.NormalizeAddress(leAddress), true, Transport.BluetoothLE);

        Assert.Equal(same, classic.Key == le.Key);
    }
}
