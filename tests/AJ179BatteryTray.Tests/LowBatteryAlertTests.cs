using AJ179BatteryTray;

namespace AJ179BatteryTray.Tests;

public class LowBatteryAlertTests
{
    [Fact]
    public void WarnsOncePerDischarge()
    {
        var alert = new LowBatteryAlert(20);
        Assert.False(alert.ShouldNotify(21, true));
        Assert.True(alert.ShouldNotify(20, true));
        Assert.False(alert.ShouldNotify(19, true));
        Assert.False(alert.ShouldNotify(5, true));
    }

    [Fact]
    public void RearmsOnlyAboveHysteresis()
    {
        var alert = new LowBatteryAlert(20);
        Assert.True(alert.ShouldNotify(18, true));
        Assert.False(alert.ShouldNotify(25, true)); // threshold + 5: still armed off
        Assert.False(alert.ShouldNotify(19, true));
        Assert.False(alert.ShouldNotify(26, true)); // above threshold + 5: re-armed
        Assert.True(alert.ShouldNotify(20, true));
    }

    [Fact]
    public void SleepingMouseDoesNotWarnButKeepsTheWarningPending()
    {
        var alert = new LowBatteryAlert(20);
        Assert.False(alert.ShouldNotify(15, false));
        Assert.True(alert.ShouldNotify(15, true));
    }

    [Fact]
    public void UnknownChargeNeverWarns() => Assert.False(new LowBatteryAlert(20).ShouldNotify(null, true));

    [Fact]
    public void ZeroThresholdDisablesWarnings() => Assert.False(new LowBatteryAlert(0).ShouldNotify(1, true));

    [Fact]
    public void ChangingThresholdRearms()
    {
        var alert = new LowBatteryAlert(20);
        Assert.True(alert.ShouldNotify(15, true));
        alert.ChangeThreshold(30);
        Assert.True(alert.ShouldNotify(15, true));
    }
}
