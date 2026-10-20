using AJ179BatteryTray;

namespace AJ179BatteryTray.Tests;

public class TrayTextTests
{
    private static BatteryReading Reading(int? percent, bool receiver = true, bool online = true, bool onCable = false) =>
        new(receiver, online, percent, "3.0", "test", onCable);

    [Fact]
    public void ShowsLivePercent() =>
        Assert.Equal("AJ179 PRO: 87%", TrayText.Tooltip(Reading(87), 87));

    [Fact]
    public void MarksLastKnownPercent() =>
        Assert.Equal("AJ179 PRO: 87% (последнее)", TrayText.Tooltip(Reading(null), 87));

    [Fact]
    public void MarksCableWithLastKnownPercent() =>
        Assert.Equal("AJ179 PRO: 87% (последнее) · по кабелю",
            TrayText.Tooltip(Reading(null, online: false, onCable: true), 87));

    [Fact]
    public void CableWithoutAnyPercent() =>
        Assert.Equal("AJ179 PRO: по кабелю", TrayText.Menu(Reading(null, onCable: true), null));

    [Theory]
    [InlineData(true, "AJ179 PRO: пошевелите мышь")]
    [InlineData(false, "AJ179 PRO: приёмник не найден")]
    public void TooltipWithoutData(bool receiver, string expected) =>
        Assert.Equal(expected, TrayText.Tooltip(Reading(null, receiver), null));

    [Theory]
    [InlineData(true, "AJ179 PRO: нет данных")]
    [InlineData(false, "AJ179 PRO: не подключена")]
    public void MenuWithoutData(bool receiver, string expected) =>
        Assert.Equal(expected, TrayText.Menu(Reading(null, receiver), null));

    [Fact]
    public void LongestTooltipFitsTheWindowsLimit() =>
        Assert.True(TrayText.Tooltip(Reading(null, onCable: true), 100).Length <= TrayText.TooltipLimit);
}

public class ReaderOrderTests
{
    [Fact]
    public void PollsReceiversBeforeTheWiredMouse()
    {
        var devices = new[] { ("cable", true), ("dongle", false), ("dongle2", false) };
        var order = Aj179BatteryReader.OrderForPolling(devices, d => d.Item2).Select(d => d.Item1);
        Assert.Equal(["dongle", "dongle2", "cable"], order);
    }
}
