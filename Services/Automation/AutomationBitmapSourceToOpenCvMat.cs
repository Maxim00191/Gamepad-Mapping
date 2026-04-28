#nullable enable

using System.Windows.Media;
using System.Windows.Media.Imaging;
using OpenCvSharp;

namespace GamepadMapperGUI.Services.Automation;

internal static class AutomationBitmapSourceToOpenCvMat
{
    public static Mat ToBgrMat(BitmapSource source)
    {
        var converted = EnsureBgra32(source);
        var w = converted.PixelWidth;
        var h = converted.PixelHeight;
        var stride = w * 4;
        var pixels = new byte[stride * h];
        converted.CopyPixels(pixels, stride, 0);
        using var bgra = Mat.FromPixelData(h, w, MatType.CV_8UC4, pixels, stride);
        var bgr = new Mat();
        Cv2.CvtColor(bgra, bgr, ColorConversionCodes.BGRA2BGR);
        return bgr;
    }

    private static BitmapSource EnsureBgra32(BitmapSource source)
    {
        if (source.Format == PixelFormats.Bgra32)
            return source;

        var fb = new FormatConvertedBitmap();
        fb.BeginInit();
        fb.Source = source;
        fb.DestinationFormat = PixelFormats.Bgra32;
        fb.EndInit();
        fb.Freeze();
        return fb;
    }
}
