using System.Drawing.Text;
using System.Reflection;
using System.Runtime.InteropServices;

namespace AJ179BatteryTray;

internal static class AppFonts
{
    private static readonly PrivateFontCollection Fonts = new();
    private static readonly List<nint> Buffers = [];
    public static FontFamily Family { get; }

    static AppFonts()
    {
        foreach (var weight in new[] { "Regular", "Bold" })
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream($"AJ179BatteryTray.Assets.JetBrainsMono-{weight}.ttf")!;
            using var memory = new MemoryStream();
            stream.CopyTo(memory);
            var bytes = memory.ToArray();
            var buffer = Marshal.AllocHGlobal(bytes.Length);
            Marshal.Copy(bytes, 0, buffer, bytes.Length);
            Buffers.Add(buffer);
            Fonts.AddMemoryFont(buffer, bytes.Length);
        }
        Family = Fonts.Families.Single();
        AppDomain.CurrentDomain.ProcessExit += (_, _) =>
        {
            Family.Dispose(); Fonts.Dispose();
            foreach (var buffer in Buffers) Marshal.FreeHGlobal(buffer);
        };
    }

    public static Font Create(float points, FontStyle style = FontStyle.Regular) => new(Family, points, style);
}
