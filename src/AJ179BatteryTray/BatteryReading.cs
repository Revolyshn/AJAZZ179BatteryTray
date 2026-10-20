namespace AJ179BatteryTray;

/// <param name="OnCable">
/// The mouse itself is connected over USB (it enumerates as "AJAZZ AJ179 PRO"). The HID
/// protocol has no charging flag, and the mouse may also charge from a dock, so this only
/// states the cable connection and makes no claim about charging.
/// </param>
internal sealed record BatteryReading(
    bool ReceiverConnected,
    bool MouseOnline,
    int? Percent,
    string FirmwareRevision,
    string Detail,
    bool OnCable = false)
{
    public static BatteryReading Disconnected(string detail = "Приёмник не найден") =>
        new(false, false, null, "—", detail);
}
