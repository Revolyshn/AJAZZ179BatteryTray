namespace AJ179BatteryTray;

/// <param name="Charging">
/// The mouse itself is connected over USB (it enumerates as "AJAZZ AJ179 PRO"). The HID
/// protocol has no charging flag, so a cable connection is the only charging signal.
/// </param>
internal sealed record BatteryReading(
    bool ReceiverConnected,
    bool MouseOnline,
    int? Percent,
    string FirmwareRevision,
    string Detail,
    bool Charging = false)
{
    public static BatteryReading Disconnected(string detail = "Приёмник не найден") =>
        new(false, false, null, "—", detail);
}
