using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace GamepadMapperGUI.Services.Automation;

internal static class AutomationBitmapDpiNormalizer
{
    private const double DefaultDpi = 96.0;

    public static BitmapSource NormalizeToDefaultDpi(BitmapSource source)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (NearlyEqual(source.DpiX, DefaultDpi) && NearlyEqual(source.DpiY, DefaultDpi))
            return source;

        var converted = new FormatConvertedBitmap(source, PixelFormats.Pbgra32, null, 0);

        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
        {
            dc.DrawImage(converted, new Rect(0, 0, converted.PixelWidth, converted.PixelHeight));
        }

        var rtb = new RenderTargetBitmap(converted.PixelWidth, converted.PixelHeight, DefaultDpi, DefaultDpi,
            PixelFormats.Pbgra32);
        rtb.Render(dv);
        rtb.Freeze();
        return rtb;
    }

    private static bool NearlyEqual(double a, double b) => Math.Abs(a - b) < 0.01;
}
