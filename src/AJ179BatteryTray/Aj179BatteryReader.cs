using HidSharp;

namespace AJ179BatteryTray;

internal sealed class Aj179BatteryReader
{
    private const int VendorId = 0x3151;
    private static readonly HashSet<int> SupportedProductIds = [0x4026, 0x4027, 0x402D];

    public Task<BatteryReading> ReadAsync(CancellationToken cancellationToken = default) =>
        Task.Run(Read, cancellationToken);

    private static BatteryReading Read()
    {
        List<(HidDevice Device, bool Wired)> devices;
        try
        {
            devices = DeviceList.Local.GetHidDevices(VendorId)
                .Where(d => SupportedProductIds.Contains(d.ProductID))
                .Select(d => (d, IsWiredMouse(d)))
                .ToList();
        }
        catch (Exception ex)
        {
            return BatteryReading.Disconnected($"Ошибка поиска HID: {ex.Message}");
        }

        var onCable = devices.Any(d => d.Wired);
        var receivers = PollTargets(devices, d => d.Wired)
            .Select(d => d.Device)
            .Where(d => d.GetMaxFeatureReportLength() >= BatteryProtocol.ReportLength)
            .ToList();

        if (receivers.Count == 0)
        {
            return onCable
                ? new(false, false, null, "—", "Мышь подключена кабелем; по кабелю заряд не передаётся", OnCable: true)
                : BatteryReading.Disconnected();
        }

        BatteryReading? fallback = null;
        foreach (var receiver in receivers)
        {
            var reading = Poll(receiver) with { OnCable = onCable };
            if (reading.Percent is not null) return reading;
            fallback ??= reading;
        }
        return fallback!;
    }

    /// <summary>
    /// Only receivers are polled. The mouse connected by cable is never sent the 0xF7 status
    /// poll: its firmware stops registering button clicks while it receives it (movement still
    /// works), and it does not report the charge over the cable anyway.
    /// </summary>
    internal static IEnumerable<T> PollTargets<T>(IEnumerable<T> devices, Func<T, bool> isWired) =>
        devices.Where(d => !isWired(d));

    private static BatteryReading Poll(HidDevice device)
    {
        var revision = device.ReleaseNumber.ToString();
        if (!device.TryOpen(out var stream))
        {
            return new(true, false, null, revision, "HID занят другим приложением");
        }

        using (stream)
        {
            try
            {
                for (var attempt = 0; attempt < 3; attempt++)
                {
                    stream.SetFeature(BatteryProtocol.CreatePoll());
                    Thread.Sleep(attempt == 0 ? 35 : 90);

                    var response = new byte[BatteryProtocol.ReportLength];
                    response[0] = 0x00;
                    stream.GetFeature(response);

                    if (BatteryProtocol.TryParse(response, out var percent, out var online))
                    {
                        return new(true, online, percent, revision,
                            online ? "Телеметрия получена" : "Мышь спит; показано последнее значение приёмника");
                    }
                }

                return new(true, false, null, revision,
                    "Телеметрия пока недоступна — пошевелите мышь или включите режим 2.4G");
            }
            catch (Exception ex)
            {
                return new(true, false, null, revision, $"Ошибка HID: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// The receiver reports itself as "AJAZZ 2.4G 8K"; the mouse connected by cable reports
    /// "AJAZZ AJ179 PRO" (PID 0x4026, revision 3.3).
    /// </summary>
    private static bool IsWiredMouse(HidDevice device)
    {
        try
        {
            return device.GetProductName().Contains("AJ179 PRO", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }
}
