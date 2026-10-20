using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace AJ179BatteryTray;

internal static class TrayIconRenderer
{
    public static Icon Create(int? percent, bool connected, bool online)
    {
        using var bitmap = new Bitmap(32, 32, PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.CompositingQuality = CompositingQuality.HighQuality;
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
        graphics.Clear(Color.Transparent);

        var text = percent?.ToString() ?? "?";
        var fontSize = text.Length >= 3 ? 19f : text.Length == 2 ? 24f : 27f;
        var family = AppFonts.Family;
        using var digits = new GraphicsPath();
        digits.AddString(text, family, (int)FontStyle.Bold, fontSize, Point.Empty, StringFormat.GenericTypographic);

        var textBounds = digits.GetBounds();
        using (var center = new Matrix())
        {
            var scale = Math.Min(1f, 28f / textBounds.Width);
            center.Scale(scale, scale);
            digits.Transform(center);
            center.Reset();
            textBounds = digits.GetBounds();
            center.Translate(
                (32f - textBounds.Width) / 2f - textBounds.Left,
                (32f - textBounds.Height) / 2f - textBounds.Top - 0.4f);
            digits.Transform(center);
        }

        // Keep every state monochrome: the percentage already communicates the
        // level, while the neutral glass treatment remains legible on any taskbar.
        using (var glow = new Pen(Color.FromArgb(68, 255, 255, 255), 3.2f)
               { LineJoin = LineJoin.Round })
        {
            graphics.DrawPath(glow, digits);
        }
        using (var outline = new Pen(Color.FromArgb(225, 24, 24, 24), 1.3f)
               { LineJoin = LineJoin.Round })
        {
            graphics.DrawPath(outline, digits);
        }
        using (var fill = new LinearGradientBrush(
                   new RectangleF(0, 1, 32, 30),
                   Color.FromArgb(255, 255, 255, 255),
                   Color.FromArgb(255, 166, 166, 166),
                   LinearGradientMode.Vertical))
        {
            graphics.FillPath(fill, digits);
        }
        using (var highlight = new Pen(Color.FromArgb(190, 255, 255, 255), 0.65f)
               { LineJoin = LineJoin.Round })
        {
            graphics.DrawPath(highlight, digits);
        }

        var handle = bitmap.GetHicon();
        try
        {
            using var temporary = Icon.FromHandle(handle);
            return (Icon)temporary.Clone();
        }
        finally
        {
            DestroyIcon(handle);
        }
    }

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(nint handle);
}
