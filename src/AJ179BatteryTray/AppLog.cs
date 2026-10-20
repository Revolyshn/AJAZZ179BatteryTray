using System.Text;

namespace AJ179BatteryTray;

/// <summary>
/// Small diagnostic log in %LOCALAPPDATA%\AJ179BatteryTray. Only state changes and
/// errors are written, and the file rolls over to log.1.txt at <see cref="MaxBytes"/>.
/// Logging never throws: a failed write is dropped.
/// </summary>
internal static class AppLog
{
    public const long MaxBytes = 256 * 1024;

    private static readonly object Gate = new();

    public static string Directory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AJ179BatteryTray");

    public static string FilePath { get; } = Path.Combine(Directory, "log.txt");

    public static void Info(string message) => Write("INFO", message);

    public static void Error(string message, Exception? ex = null) =>
        Write("ERROR", ex is null ? message : $"{message}: {ex.GetType().Name}: {ex.Message}");

    private static void Write(string level, string message)
    {
        var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {level} {message}{Environment.NewLine}";
        lock (Gate)
        {
            try
            {
                System.IO.Directory.CreateDirectory(Directory);
                RollIfNeeded(FilePath, MaxBytes);
                File.AppendAllText(FilePath, line, Encoding.UTF8);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
            }
        }
    }

    internal static void RollIfNeeded(string path, long maxBytes)
    {
        var file = new FileInfo(path);
        if (!file.Exists || file.Length < maxBytes) return;
        var previous = Path.ChangeExtension(path, ".1" + Path.GetExtension(path));
        File.Move(path, previous, overwrite: true);
    }
}
