using System.Drawing;
using System.Drawing.Drawing2D;
using AJ179BatteryTray;

namespace AJ179BatteryTray.Tests;

public class TrayIconRendererTests
{
    private static int ContourCount(GraphicsPath path)
    {
        using var iterator = new GraphicsPathIterator(path);
        return iterator.SubpathCount;
    }

    [Theory]
    [InlineData("0", 2)] // outer + counter; the inner dot is dropped
    [InlineData("8", 3)] // outer + two counters stay
    [InlineData("?", 2)] // hook + separate dot stay
    [InlineData("100", 5)]
    public void DropsOnlyTheZeroDot(string text, int expected)
    {
        using var path = TrayIconRenderer.BuildDigits(text);
        Assert.Equal(expected, ContourCount(path));
    }

    [Theory]
    [InlineData(100, 16, false)]
    [InlineData(100, 16, true)]
    [InlineData(7, 20, false)]
    [InlineData(null, 24, true)]
    [InlineData(55, 32, false)]
    public void DrawsInsideTheIconWithAMargin(int? percent, int size, bool light)
    {
        using var bitmap = TrayIconRenderer.Render(percent, size, light);
        Assert.Equal(size, bitmap.Width);
        Assert.Equal(size, bitmap.Height);

        var opaque = 0;
        for (var y = 0; y < size; y++)
        for (var x = 0; x < size; x++)
        {
            if (bitmap.GetPixel(x, y).A > 128) opaque++;
        }
        Assert.InRange(opaque, size * size / 10, size * size * 3 / 4);

        // The corner pixels stay clear so neighbouring tray icons do not touch.
        foreach (var (x, y) in new[] { (0, 0), (size - 1, 0), (0, size - 1), (size - 1, size - 1) })
        {
            Assert.True(bitmap.GetPixel(x, y).A < 40, $"corner {x},{y} is drawn");
        }
    }
}
