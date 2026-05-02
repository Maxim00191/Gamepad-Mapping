#nullable enable

using System.Runtime.InteropServices.WindowsRuntime;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Windows.Graphics.Imaging;

namespace GamepadMapperGUI.Services.Automation;

internal static class AutomationOcrBitmapEncoding
{
    private const int DefaultMaxLongEdgePx = 1280;

    public static SoftwareBitmap ToSoftwareBitmap(BitmapSource source, int maxLongEdgePx)
    {
        var bgra = source.Format == PixelFormats.Bgra32
            ? source
            : new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        bgra.Freeze();
        var scaled = ScaleDownIfNeeded(bgra, maxLongEdgePx);
        scaled.Freeze();
        var w = scaled.PixelWidth;
        var h = scaled.PixelHeight;
        var stride = w * 4;
        var pixels = new byte[stride * h];
        scaled.CopyPixels(pixels, stride, 0);
        return SoftwareBitmap.CreateCopyFromBuffer(
            pixels.AsBuffer(),
            BitmapPixelFormat.Bgra8,
            w,
            h,
            BitmapAlphaMode.Premultiplied);
    }

    public static int ResolveMaxLongEdge(int configuredMaxLongEdgePx) =>
        configuredMaxLongEdgePx <= 0 ? DefaultMaxLongEdgePx : Math.Clamp(configuredMaxLongEdgePx, 320, 4096);

    private static BitmapSource ScaleDownIfNeeded(BitmapSource source, int maxLongEdgePx)
    {
        var cap = ResolveMaxLongEdge(maxLongEdgePx);
        var w = source.PixelWidth;
        var h = source.PixelHeight;
        var maxDim = Math.Max(w, h);
        if (maxDim <= cap)
            return source;

        var scale = cap / (double)maxDim;
        var scaled = new TransformedBitmap(source, new ScaleTransform(scale, scale, 0, 0));
        return scaled;
    }
}
