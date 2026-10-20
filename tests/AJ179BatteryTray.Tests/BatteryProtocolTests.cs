using AJ179BatteryTray;

namespace AJ179BatteryTray.Tests;

public class BatteryProtocolTests
{
    // Layout after the report ID: [canRead, kbBattery, mouseBattery, kbState, mouseState, canSend, deviceMask, ...]
    private static byte[] Frame(byte percent = 73, byte mouseState = 0, byte deviceMask = 2)
    {
        var report = new byte[BatteryProtocol.ReportLength];
        report[1] = 1;
        report[3] = percent;
        report[5] = mouseState;
        report[7] = deviceMask;
        return report;
    }

    [Fact]
    public void ParsesOnlineMouse()
    {
        Assert.True(BatteryProtocol.TryParse(Frame(), out var percent, out var online));
        Assert.Equal(73, percent);
        Assert.True(online);
    }

    [Fact]
    public void ParsesSleepingMouse()
    {
        Assert.True(BatteryProtocol.TryParse(Frame(mouseState: 1), out var percent, out var online));
        Assert.Equal(73, percent);
        Assert.False(online);
    }

    [Fact]
    public void ParsesFrameWithoutReportId()
    {
        var withoutId = Frame(percent: 41)[1..];
        Assert.True(BatteryProtocol.TryParse(withoutId, out var percent, out _));
        Assert.Equal(41, percent);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(100)]
    public void AcceptsPercentBounds(byte value)
    {
        Assert.True(BatteryProtocol.TryParse(Frame(percent: value), out var percent, out _));
        Assert.Equal(value, percent);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    [InlineData(255)]
    public void RejectsPercentOutOfRange(byte value) =>
        Assert.False(BatteryProtocol.TryParse(Frame(percent: value), out _, out _));

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(4)]
    public void RejectsFramesWithoutMouseTelemetry(byte deviceMask) =>
        Assert.False(BatteryProtocol.TryParse(Frame(deviceMask: deviceMask), out _, out _));

    [Fact]
    public void AcceptsKeyboardAndMouseMask() =>
        Assert.True(BatteryProtocol.TryParse(Frame(deviceMask: 3), out _, out _));

    [Fact]
    public void RejectsRequestEcho() =>
        Assert.False(BatteryProtocol.TryParse(BatteryProtocol.CreatePoll(), out _, out _));

    [Fact]
    public void RejectsEmptyFrame() =>
        Assert.False(BatteryProtocol.TryParse(new byte[BatteryProtocol.ReportLength], out _, out _));

    [Fact]
    public void RejectsShortFrame() =>
        Assert.False(BatteryProtocol.TryParse(Frame()[..8], out _, out _));

    [Fact]
    public void PollCarriesStatusOpcode()
    {
        var poll = BatteryProtocol.CreatePoll();
        Assert.Equal(BatteryProtocol.ReportLength, poll.Length);
        Assert.Equal(0, poll[0]);
        Assert.Equal(BatteryProtocol.StatusOpcode, poll[1]);
        Assert.All(poll[2..], b => Assert.Equal(0, b));
    }
}
