namespace AJ179BatteryTray;

internal sealed record BatteryReading(
    bool ReceiverConnected,
    bool MouseOnline,
    int? Percent,
    string FirmwareRevision,
    string Detail)
{
    public static BatteryReading Disconnected(string detail = "Приёмник не найден") =>
        new(false, false, null, "—", detail);
}
