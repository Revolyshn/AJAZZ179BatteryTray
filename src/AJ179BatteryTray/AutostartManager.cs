using Microsoft.Win32;

namespace AJ179BatteryTray;

internal enum AutostartState
{
    Disabled,
    Enabled,
    /// <summary>The Run entry points to an executable that no longer exists.</summary>
    Stale,
    /// <summary>The Run entry points to another existing copy of the app.</summary>
    OtherCopy
}

internal static class AutostartManager
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "AJ179BatteryTray";

    private static string CurrentExecutable => Environment.ProcessPath ?? Application.ExecutablePath;

    public static AutostartState GetState()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, false);
        return Classify(key?.GetValue(ValueName) as string, CurrentExecutable, File.Exists);
    }

    public static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, true)
            ?? Registry.CurrentUser.CreateSubKey(RunKey, true);

        if (enabled)
        {
            key.SetValue(ValueName, $"\"{CurrentExecutable}\"");
        }
        else
        {
            key.DeleteValue(ValueName, false);
        }
    }

    /// <summary>
    /// Points a Run entry whose executable was moved or deleted at this copy.
    /// An entry for another existing copy is left alone.
    /// </summary>
    public static bool RepairIfStale()
    {
        if (GetState() != AutostartState.Stale) return false;
        SetEnabled(true);
        return true;
    }

    internal static AutostartState Classify(string? command, string currentExecutable, Func<string, bool> fileExists)
    {
        var target = ExtractExecutable(command);
        if (target is null) return AutostartState.Disabled;
        if (PathsEqual(target, currentExecutable)) return AutostartState.Enabled;
        return fileExists(target) ? AutostartState.OtherCopy : AutostartState.Stale;
    }

    internal static string? ExtractExecutable(string? command)
    {
        if (string.IsNullOrWhiteSpace(command)) return null;
        var text = command.Trim();
        if (text[0] == '"')
        {
            var end = text.IndexOf('"', 1);
            return end > 1 ? text[1..end] : null;
        }

        var exe = text.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
        return exe >= 0 ? text[..(exe + 4)] : text;
    }

    private static bool PathsEqual(string a, string b)
    {
        try
        {
            return string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }
}
