using ChargeHub.Sources;
using Xunit;

namespace ChargeHub.Tests;

public class AsusProtocolTests
{
    static byte[] Packet(byte percent = 76, byte charging = 0, byte b1 = 0x12, byte b2 = 0x07)
    {
        var packet = new byte[65];
        packet[1] = b1; packet[2] = b2; packet[5] = percent; packet[10] = charging;
        return packet;
    }

    [Fact]
    public void Request_is_the_read_only_battery_query()
    {
        var request = AsusProtocol.BatteryRequest();
        Assert.Equal(65, request.Length);
        Assert.Equal(new byte[] { 0x00, 0x12, 0x07 }, request[..3]);
        Assert.All(request[3..], b => Assert.Equal(0, b));
    }

    [Fact]
    public void Battery_response_is_parsed()
    {
        var response = AsusProtocol.Parse(Packet(76));
        Assert.Equal(new AsusResponse(AsusResponseKind.Battery, 76, false), response);
    }

    [Fact]
    public void Charging_flag_is_parsed()
    {
        Assert.True(AsusProtocol.Parse(Packet(76, charging: 1)).Charging);
    }

    [Fact]
    public void Zero_percent_while_charging_is_a_real_reading()
    {
        Assert.Equal(new AsusResponse(AsusResponseKind.Battery, 0, true), AsusProtocol.Parse(Packet(0, 1)));
    }

    [Fact]
    public void Empty_payload_means_asleep_not_zero_percent()
    {
        Assert.Equal(AsusResponseKind.Asleep, AsusProtocol.Parse(Packet(0, 0)).Kind);
    }

    [Fact]
    public void Receiver_disconnected_status_means_asleep()
    {
        Assert.Equal(AsusResponseKind.Asleep, AsusProtocol.Parse(Packet(b1: 0xff, b2: 0xaa)).Kind);
    }

    [Fact]
    public void Truncated_out_of_range_and_unrelated_packets_are_rejected()
    {
        Assert.Equal(AsusResponseKind.Unrelated, AsusProtocol.Parse(new byte[2]).Kind);
        Assert.Equal(AsusResponseKind.Unrelated, AsusProtocol.Parse(Packet(255)).Kind);
        Assert.Equal(AsusResponseKind.Unrelated, AsusProtocol.Parse(Packet(80, b2: 0x05)).Kind);
    }

    [Theory]
    [InlineData(@"\\?\hid#vid_0b05&pid_1a72&mi_00#7&abc&0&0000#{4d1e55b2-f16f-11cf-88cb-001111000030}", true, false)]
    [InlineData(@"\\?\HID#VID_0B05&PID_1A70&MI_00#7&abc&0&0000#{4d1e55b2}", true, true)]
    [InlineData(@"\\?\hid#vid_0b05&pid_1a72&mi_01#7&abc", false, false)]
    [InlineData(@"\\?\hid#vid_046d&pid_c539&mi_00#7&abc", false, false)]
    public void Interface_selection(string path, bool supported, bool wired)
    {
        Assert.Equal(supported, AsusProtocol.IsSupportedInterface(path));
        Assert.Equal(wired, AsusProtocol.IsWired(path));
    }
}
