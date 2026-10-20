using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace AJ179BatteryTray;

internal static class TrayIconRenderer
{
    /// <summary>Tray icon size for the current system DPI (16 px at 100%, 20 at 125%, 24 at 150%, 32 at 200%).</summary>
    public static int CurrentSize => Math.Max(16, SystemInformation.SmallIconSize.Width);

    public static Icon Create(int? percent, int size, bool lightTaskbar)
    {
        using var bitmap = Render(percent, size, lightTaskbar);
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

    /// <summary>
    /// Draws the percentage directly at the target pixel size instead of letting Windows
    /// shrink a 32 px bitmap. Wide values are condensed horizontally so the digits keep
    /// their height; three digits get a slightly smaller height to limit the squeeze.
    /// </summary>
    internal static Bitmap Render(int? percent, int size, bool lightTaskbar)
    {
        var bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.CompositingQuality = CompositingQuality.HighQuality;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        graphics.Clear(Color.Transparent);

        var text = percent?.ToString() ?? "?";
        using var digits = BuildDigits(text);

        var unit = size / 32f;
        var bounds = digits.GetBounds();
        var targetHeight = size * (text.Length >= 3 ? 0.62f : 0.74f);
        var maxWidth = size - 2f * unit;
        var scaleY = targetHeight / bounds.Height;
        var scaleX = Math.Min(scaleY, maxWidth / bounds.Width);
        using (var fit = new Matrix())
        {
            fit.Scale(scaleX, scaleY);
            digits.Transform(fit);
            bounds = digits.GetBounds();
            fit.Reset();
            // Center, then snap the top edge to a whole pixel so small sizes stay crisp.
            var left = (size - bounds.Width) / 2f - bounds.Left;
            var top = MathF.Round((size - bounds.Height) / 2f) - bounds.Top;
            fit.Translate(left, top);
            digits.Transform(fit);
        }

        var palette = lightTaskbar ? Palette.Light : Palette.Dark;
        // Below 24 px the strokes are about one pixel wide; thicken them so the digits stay legible.
        // Dark digits on a light taskbar lack the dark outline that adds weight, so they are always thickened.
        var embolden = lightTaskbar ? Math.Max(0.9f, 1.1f * unit) : size < 24 ? 0.9f : 0f;
        using (var halo = new Pen(palette.Halo, 3.2f * unit + embolden) { LineJoin = LineJoin.Round })
        {
            graphics.DrawPath(halo, digits);
        }
        using (var outline = new Pen(palette.Outline, Math.Max(1f, 1.3f * unit) + embolden) { LineJoin = LineJoin.Round })
        {
            graphics.DrawPath(outline, digits);
        }
        using (var fill = new LinearGradientBrush(new RectangleF(0, 0, size, size + 1),
                   palette.FillTop, palette.FillBottom, LinearGradientMode.Vertical))
        {
            if (embolden > 0)
            {
                using var thicken = new Pen(fill, embolden) { LineJoin = LineJoin.Round };
                graphics.DrawPath(thicken, digits);
            }
            graphics.FillPath(fill, digits);
        }
        // The inner highlight only reads at 24 px and up; at 16-20 px it just blurs the digits.
        // On dark digits (light taskbar) a white edge would thin the strokes, so it is skipped.
        if (size >= 24 && palette.Highlight.A > 0)
        {
            using var highlight = new Pen(palette.Highlight, 0.65f * unit) { LineJoin = LineJoin.Round };
            graphics.DrawPath(highlight, digits);
        }

        return bitmap;
    }

    /// <summary>
    /// Builds the text outline without the dot JetBrains Mono draws inside zero: at tray
    /// sizes it turns "0" into "θ". The dot is the only contour nested two levels deep
    /// (glyph, counter, dot), so contours at that depth are dropped.
    /// </summary>
    internal static GraphicsPath BuildDigits(string text)
    {
        using var source = new GraphicsPath();
        source.AddString(text, AppFonts.Family, (int)FontStyle.Bold, 100f, Point.Empty, StringFormat.GenericTypographic);

        var figures = new List<(PointF[] Points, byte[] Types, RectangleF Bounds)>();
        using (var iterator = new GraphicsPathIterator(source))
        using (var figure = new GraphicsPath())
        {
            while (iterator.NextSubpath(figure, out _) > 0)
            {
                figures.Add((figure.PathPoints, figure.PathTypes, figure.GetBounds()));
                figure.Reset();
            }
        }

        var result = new GraphicsPath(FillMode.Alternate);
        foreach (var (points, types, bounds) in figures)
        {
            var depth = figures.Count(other => other.Bounds != bounds && Contains(other.Bounds, bounds));
            if (depth >= 2) continue;
            using var contour = new GraphicsPath(points, types);
            result.AddPath(contour, connect: false);
        }
        return result;
    }

    private static bool Contains(RectangleF outer, RectangleF inner) =>
        outer.Left <= inner.Left && outer.Top <= inner.Top && outer.Right >= inner.Right && outer.Bottom >= inner.Bottom;

    private sealed record Palette(Color Halo, Color Outline, Color FillTop, Color FillBottom, Color Highlight)
    {
        // Silver digits with a dark outline for a dark taskbar.
        public static readonly Palette Dark = new(
            Color.FromArgb(68, 255, 255, 255), Color.FromArgb(225, 24, 24, 24),
            Color.FromArgb(255, 255, 255, 255), Color.FromArgb(255, 166, 166, 166),
            Color.FromArgb(190, 255, 255, 255));

        // Graphite digits with a light halo for a light taskbar.
        public static readonly Palette Light = new(
            Color.FromArgb(150, 255, 255, 255), Color.FromArgb(235, 250, 250, 252),
            Color.FromArgb(255, 58, 62, 72), Color.FromArgb(255, 22, 24, 29),
            Color.Transparent);
    }

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(nint handle);
}
