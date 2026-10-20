using Microsoft.Win32;

namespace AJ179BatteryTray;

internal static class TaskbarTheme
{
    private const string PersonalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    /// <summary>
    /// True when the taskbar uses the light theme (Settings, Personalization, Colors,
    /// "Choose your default Windows mode"). A missing value is treated as dark, matching
    /// the original silver icon.
    /// </summary>
    public static bool IsLight()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKey, false);
            return key?.GetValue("SystemUsesLightTheme") is int value && value != 0;
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
