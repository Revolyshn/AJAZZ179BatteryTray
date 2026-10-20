using AJ179BatteryTray;

namespace AJ179BatteryTray.Tests;

public sealed class AppSettingsTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"aj179-settings-{Guid.NewGuid():N}");
    private string SettingsPath => Path.Combine(_dir, "settings.json");

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public void MissingFileGivesDefaults() =>
        Assert.Equal(AppSettings.DefaultLowBatteryThreshold, AppSettings.Load(SettingsPath).LowBatteryThreshold);

    [Fact]
    public void SaveAndLoadRoundTrip()
    {
        new AppSettings { LowBatteryThreshold = 0 }.Save(SettingsPath);
        Assert.Equal(0, AppSettings.Load(SettingsPath).LowBatteryThreshold);
        Assert.False(File.Exists(SettingsPath + ".tmp"));
    }

    [Fact]
    public void CorruptFileFallsBackToDefaults()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(SettingsPath, "{ not json");
        Assert.Equal(AppSettings.DefaultLowBatteryThreshold, AppSettings.Load(SettingsPath).LowBatteryThreshold);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(51)]
    public void OutOfRangeValueIsReset(int value)
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(SettingsPath, $$"""{ "LowBatteryThreshold": {{value}} }""");
        Assert.Equal(AppSettings.DefaultLowBatteryThreshold, AppSettings.Load(SettingsPath).LowBatteryThreshold);
    }

    [Fact]
    public void UnknownPropertiesAreIgnored()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(SettingsPath, """{ "LowBatteryThreshold": 15, "Future": true }""");
        Assert.Equal(15, AppSettings.Load(SettingsPath).LowBatteryThreshold);
    }
}
