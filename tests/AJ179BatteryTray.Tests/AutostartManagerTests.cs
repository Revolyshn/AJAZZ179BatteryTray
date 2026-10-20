using AJ179BatteryTray;

namespace AJ179BatteryTray.Tests;

public class AutostartManagerTests
{
    private const string Current = @"D:\soft\AJ179BatteryTray\AJ179BatteryTray.exe";
    private const string Old = @"C:\Users\me\Downloads\old\AJ179BatteryTray\AJ179BatteryTray.exe";

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void MissingEntryIsDisabled(string? command) =>
        Assert.Equal(AutostartState.Disabled, AutostartManager.Classify(command, Current, _ => true));

    [Theory]
    [InlineData("\"" + Current + "\"")]
    [InlineData(Current)]
    [InlineData("\"D:\\SOFT\\aj179batterytray\\AJ179BatteryTray.exe\" --panel")]
    public void EntryForThisCopyIsEnabled(string command) =>
        Assert.Equal(AutostartState.Enabled, AutostartManager.Classify(command, Current, _ => false));

    [Fact]
    public void EntryForDeletedFileIsStale() =>
        Assert.Equal(AutostartState.Stale, AutostartManager.Classify($"\"{Old}\"", Current, _ => false));

    [Fact]
    public void EntryForExistingOtherCopyIsLeftAlone() =>
        Assert.Equal(AutostartState.OtherCopy, AutostartManager.Classify($"\"{Old}\"", Current, _ => true));

    [Theory]
    [InlineData("\"C:\\Program Files\\App\\app.exe\" --x", "C:\\Program Files\\App\\app.exe")]
    [InlineData("C:\\Program Files\\App\\app.exe --x", "C:\\Program Files\\App\\app.exe")]
    [InlineData("C:\\tools\\app", "C:\\tools\\app")]
    [InlineData("\"unterminated", null)]
    public void ExtractsExecutablePath(string command, string? expected) =>
        Assert.Equal(expected, AutostartManager.ExtractExecutable(command));
}
