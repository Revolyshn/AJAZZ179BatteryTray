namespace AJ179BatteryTray;

internal static class BatteryProtocol
{
    public const byte StatusOpcode = 0xF7;
    public const int ReportLength = 65; // report ID + 64-byte payload

    public static byte[] CreatePoll()
    {
        var report = new byte[ReportLength];
        report[0] = 0x00; // HID report ID
        report[1] = StatusOpcode;
        return report;
    }

    public static bool TryParse(ReadOnlySpan<byte> report, out int percent, out bool mouseOnline)
    {
        percent = 0;
        mouseOnline = false;

        if (report.Length < 9)
        {
            return false;
        }

        // HidSharp keeps the report ID at index 0. The GearHub payload is:
        // [canRead, keyboardBattery, mouseBattery, keyboardState,
        //  mouseState, canSend, deviceMask, reserved, rfBoot, ...]
        var payload = report[0] == 0 ? report[1..] : report;
        if (payload.Length < 9 || payload[0] == StatusOpcode)
        {
            return false; // request echo: wireless telemetry is not ready
        }

        var deviceMask = payload[6];
        var hasMouseTelemetry = deviceMask is 2 or 3;
        var value = payload[2];
        if (!hasMouseTelemetry || value is < 1 or > 100)
        {
            return false;
        }

        percent = value;
        mouseOnline = payload[4] == 0;
        return true;
    }
}
