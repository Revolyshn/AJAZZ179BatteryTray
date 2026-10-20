using System.Drawing.Drawing2D;

namespace AJ179BatteryTray;

internal static class GlassBackdrop
{
    public static Bitmap Create(Image source)
    {
        // Precompute a neutral, blurred cover image once. No work per repaint.
        using var small = new Bitmap(100, 150);
        using (var g = Graphics.FromImage(small))
        {
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            var scale = Math.Max(100f / source.Width, 150f / source.Height);
            g.DrawImage(source, (100-source.Width*scale)/2, (150-source.Height*scale)/2,
                source.Width*scale, source.Height*scale);
        }
        var values = new float[100,150];
        for(var y=0;y<150;y++) for(var x=0;x<100;x++)
        {
            var c=small.GetPixel(x,y);
            values[x,y]=0.299f*c.R+0.587f*c.G+0.114f*c.B;
        }
        for(var pass=0;pass<1;pass++)
        {
            var next=new float[100,150];
            for(var y=0;y<150;y++) for(var x=0;x<100;x++)
            {
                float sum=0;
                for(var offset=-1;offset<=1;offset++) sum+=values[Math.Clamp(x+offset,0,99),y];
                next[x,y]=sum/3;
            }
            for(var y=0;y<150;y++) for(var x=0;x<100;x++)
            {
                float sum=0;
                for(var offset=-1;offset<=1;offset++) sum+=next[x,Math.Clamp(y+offset,0,149)];
                values[x,y]=sum/3;
            }
        }
        var result=new Bitmap(100,150);
        for(var y=0;y<150;y++) for(var x=0;x<100;x++)
        {
            var shade=(int)(values[x,y]*(0.36f-0.19f*y/149)+10);
            result.SetPixel(x,y,Color.FromArgb(shade,shade,shade));
        }
        return result;
    }
}
