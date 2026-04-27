using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace GamepadMapperGUI.Services.Automation;

public static class AutomationThumbnailEncoder
{
    public static string ToPngBase64(BitmapSource source, int maxEdge)
    {
        var scaled = ScaleToMaxEdge(source, maxEdge);
        var enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(scaled));
        using var ms = new MemoryStream();
        enc.Save(ms);
        return Convert.ToBase64String(ms.ToArray());
    }

    private static BitmapSource ScaleToMaxEdge(BitmapSource source, int maxEdge)
    {
        var w = source.PixelWidth;
        var h = source.PixelHeight;
        if (w <= 0 || h <= 0)
            return source;

        var scale = Math.Min(1.0, maxEdge / (double)Math.Max(w, h));
        if (scale >= 1.0 - 1e-6)
            return source;

        var tw = Math.Max(1, (int)Math.Round(w * scale));
        var th = Math.Max(1, (int)Math.Round(h * scale));
        var rt = new RenderTargetBitmap(tw, th, 96, 96, PixelFormats.Pbgra32);
        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
        {
            dc.DrawImage(source, new System.Windows.Rect(0, 0, tw, th));
        }

        rt.Render(dv);
        rt.Freeze();
        return rt;
    }
}
