namespace AJ179BatteryTray;

/// <summary>Status line for the tray menu and the icon tooltip (limited to 63 characters by Windows).</summary>
internal static class TrayText
{
    public const int TooltipLimit = 63;

    public static string Menu(BatteryReading reading, int? shownPercent) =>
        Compose(reading, shownPercent, reading.ReceiverConnected ? "нет данных" : "не подключена");

    public static string Tooltip(BatteryReading reading, int? shownPercent)
    {
        var text = Compose(reading, shownPercent, reading.ReceiverConnected ? "пошевелите мышь" : "приёмник не найден");
        return text.Length <= TooltipLimit ? text : text[..TooltipLimit];
    }

    private static string Compose(BatteryReading reading, int? shownPercent, string noData)
    {
        if (shownPercent is null)
        {
            return reading.Charging ? "AJ179 PRO: заряжается" : $"AJ179 PRO: {noData}";
        }

        var stale = reading.Percent is null ? " (последнее)" : string.Empty;
        var charging = reading.Charging ? " · заряжается" : string.Empty;
        return $"AJ179 PRO: {shownPercent}%{stale}{charging}";
    }
}
