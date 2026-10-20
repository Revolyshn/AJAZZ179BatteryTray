using AJ179BatteryTray;

namespace AJ179BatteryTray.Tests;

public sealed class AppLogTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"aj179-log-{Guid.NewGuid():N}");
    private string LogPath => Path.Combine(_dir, "log.txt");
    private string RolledPath => Path.Combine(_dir, "log.1.txt");

    public AppLogTests() => Directory.CreateDirectory(_dir);

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void SmallFileIsKept()
    {
        File.WriteAllText(LogPath, "short");
        AppLog.RollIfNeeded(LogPath, 100);
        Assert.True(File.Exists(LogPath));
        Assert.False(File.Exists(RolledPath));
    }

    [Fact]
    public void FullFileRollsOverAndReplacesPreviousRoll()
    {
        File.WriteAllText(RolledPath, "old roll");
        File.WriteAllText(LogPath, new string('x', 100));
        AppLog.RollIfNeeded(LogPath, 100);
        Assert.False(File.Exists(LogPath));
        Assert.Equal(new string('x', 100), File.ReadAllText(RolledPath));
    }

    [Fact]
    public void MissingFileIsIgnored() => AppLog.RollIfNeeded(LogPath, 100);
}
