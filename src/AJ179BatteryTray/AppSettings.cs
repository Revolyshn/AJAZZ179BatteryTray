using System.Text.Json;

namespace AJ179BatteryTray;

/// <summary>User settings stored as JSON next to the log in %LOCALAPPDATA%\AJ179BatteryTray.</summary>
internal sealed record AppSettings
{
    public const int DefaultLowBatteryThreshold = 20;
    public static readonly int[] LowBatteryChoices = [10, 15, 20, 25, 30];

    /// <summary>The warning is re-armed only after the charge rises this far above the threshold.</summary>
    public const int LowBatteryHysteresis = 5;

    /// <summary>Percent at or below which a low-battery notification is shown; 0 disables it.</summary>
    public int LowBatteryThreshold { get; init; } = DefaultLowBatteryThreshold;

    public static string FilePath { get; } = Path.Combine(AppLog.Directory, "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static AppSettings Load() => Load(FilePath);

    public void Save() => Save(FilePath);

    internal static AppSettings Load(string path)
    {
        try
        {
            if (!File.Exists(path)) return new AppSettings();
            var loaded = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path), JsonOptions);
            return (loaded ?? new AppSettings()).Normalized();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            AppLog.Error($"Settings ignored, defaults used ({path})", ex);
            return new AppSettings();
        }
    }

    internal void Save(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        // Write a sibling file first so a crash never leaves a half-written settings file.
        var temporary = path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(this, JsonOptions));
        File.Move(temporary, path, overwrite: true);
    }

    /// <summary>Clamps hand-edited values to the supported range.</summary>
    internal AppSettings Normalized() =>
        LowBatteryThreshold is < 0 or > 50 ? this with { LowBatteryThreshold = DefaultLowBatteryThreshold } : this;
}
