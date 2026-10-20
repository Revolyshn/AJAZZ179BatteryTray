namespace AJ179BatteryTray;

/// <summary>
/// Decides when to warn about a low charge: once per discharge, while the mouse is online,
/// re-armed after the charge climbs <see cref="AppSettings.LowBatteryHysteresis"/> points
/// above the threshold.
/// </summary>
internal sealed class LowBatteryAlert
{
    private bool _notified;

    public int Threshold { get; private set; }

    public LowBatteryAlert(int threshold) => Threshold = threshold;

    public void ChangeThreshold(int threshold)
    {
        Threshold = threshold;
        // A new threshold gets a fresh chance to warn on the next reading.
        _notified = false;
    }

    /// <summary>Returns true when a notification should be shown for this reading.</summary>
    public bool ShouldNotify(int? percent, bool mouseOnline)
    {
        if (Threshold <= 0 || percent is null) return false;
        if (percent > Threshold + AppSettings.LowBatteryHysteresis)
        {
            _notified = false;
            return false;
        }
        if (_notified || !mouseOnline || percent > Threshold) return false;
        _notified = true;
        return true;
    }
}
