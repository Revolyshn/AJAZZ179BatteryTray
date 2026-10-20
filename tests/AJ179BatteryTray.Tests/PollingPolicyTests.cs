using AJ179BatteryTray;

namespace AJ179BatteryTray.Tests;

public class PollingPolicyTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void OpenPanelAlwaysPollsFast(bool receiverConnected, bool hasPercent) =>
        Assert.Equal(PollingPolicy.PanelOpen, PollingPolicy.NextDelay(true, receiverConnected, hasPercent));

    [Fact]
    public void KnownChargeInBackgroundPollsSlowly() =>
        Assert.Equal(PollingPolicy.Background, PollingPolicy.NextDelay(false, true, true));

    [Fact]
    public void MissingTelemetryPollsSoonerThanBackground()
    {
        var delay = PollingPolicy.NextDelay(false, true, false);
        Assert.Equal(PollingPolicy.WaitingForTelemetry, delay);
        Assert.True(delay < PollingPolicy.Background);
    }

    [Fact]
    public void MissingReceiverReliesOnDeviceEventsAndPollsRarely() =>
        Assert.Equal(PollingPolicy.ReceiverMissing, PollingPolicy.NextDelay(false, false, true));
}
