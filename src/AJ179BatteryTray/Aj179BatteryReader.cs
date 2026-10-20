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
        HidDevice? device;
        try
        {
            device = DeviceList.Local.GetHidDevices(VendorId)
                .Where(d => SupportedProductIds.Contains(d.ProductID))
                .Where(d => d.GetMaxFeatureReportLength() >= BatteryProtocol.ReportLength)
                .OrderByDescending(IsExactAj179Pro)
                .FirstOrDefault();
        }
        catch (Exception ex)
        {
            return BatteryReading.Disconnected($"Ошибка поиска HID: {ex.Message}");
        }

        if (device is null)
        {
            return BatteryReading.Disconnected();
        }

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

    private static bool IsExactAj179Pro(HidDevice device)
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
