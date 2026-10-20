namespace AJ179BatteryTray;

/// <summary>
/// Chooses the delay before the next poll. Battery level changes slowly, so the
/// receiver is polled often only while the user looks at the panel or while the
/// app still waits for its first telemetry. Plug/unplug and resume trigger
/// immediate polls separately.
/// </summary>
internal static class PollingPolicy
{
    public static readonly TimeSpan PanelOpen = TimeSpan.FromSeconds(5);
    public static readonly TimeSpan WaitingForTelemetry = TimeSpan.FromSeconds(10);
    public static readonly TimeSpan Background = TimeSpan.FromSeconds(30);
    public static readonly TimeSpan ReceiverMissing = TimeSpan.FromSeconds(60);

    /// <summary>Delay that lets Windows finish enumerating a receiver after a device change.</summary>
    public static readonly TimeSpan DeviceChangeSettle = TimeSpan.FromMilliseconds(1500);

    /// <summary>Delay after resume from sleep before the receiver is expected to answer.</summary>
    public static readonly TimeSpan ResumeSettle = TimeSpan.FromSeconds(3);

    public static TimeSpan NextDelay(bool panelVisible, bool receiverConnected, bool hasPercent)
    {
        if (panelVisible) return PanelOpen;
        if (!receiverConnected) return ReceiverMissing;
        return hasPercent ? Background : WaitingForTelemetry;
    }
}
