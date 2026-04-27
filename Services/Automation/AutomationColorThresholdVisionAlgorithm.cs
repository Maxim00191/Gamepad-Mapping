#nullable enable

using System.Buffers;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GamepadMapperGUI.Interfaces.Services.Automation;
using GamepadMapperGUI.Models.Automation;

namespace GamepadMapperGUI.Services.Automation;

public sealed class AutomationColorThresholdVisionAlgorithm : IAutomationVisionAlgorithm
{
    public AutomationVisionAlgorithmKind Kind => AutomationVisionAlgorithmKind.ColorThreshold;

    public ValueTask<AutomationVisionResult> ProcessAsync(AutomationVisionFrame frame, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var image = frame.Image.Format == PixelFormats.Bgra32
            ? frame.Image
            : new FormatConvertedBitmap(frame.Image, PixelFormats.Bgra32, null, 0);
        var width = image.PixelWidth;
        var height = image.PixelHeight;
        if (width <= 0 || height <= 0)
            return ValueTask.FromResult(new AutomationVisionResult(false, 0, 0));

        var stride = width * 4;
        var rented = ArrayPool<byte>.Shared.Rent(stride * height);
        try
        {
            image.CopyPixels(rented, stride, 0);
            var found = false;
            var count = 0;
            var minX = width;
            var minY = height;
            var maxX = 0;
            var maxY = 0;
            long sumX = 0;
            long sumY = 0;

            for (var y = 0; y < height; y++)
            {
                var rowOffset = y * stride;
                for (var x = 0; x < width; x++)
                {
                    var i = rowOffset + (x * 4);
                    var b = rented[i];
                    var g = rented[i + 1];
                    var r = rented[i + 2];
                    RgbToHsv(r, g, b, out var h, out var s, out var v);
                    if (h < frame.HsvHueMin || h > frame.HsvHueMax ||
                        s < frame.HsvSaturationMin || s > frame.HsvSaturationMax ||
                        v < frame.HsvValueMin || v > frame.HsvValueMax)
                    {
                        continue;
                    }

                    found = true;
                    count++;
                    sumX += x;
                    sumY += y;
                    minX = Math.Min(minX, x);
                    minY = Math.Min(minY, y);
                    maxX = Math.Max(maxX, x);
                    maxY = Math.Max(maxY, y);
                }
            }

            if (!found || count <= 0)
                return ValueTask.FromResult(new AutomationVisionResult(false, 0, 0));

            var centroidX = (int)(sumX / count);
            var centroidY = (int)(sumY / count);
            return ValueTask.FromResult(new AutomationVisionResult(
                true,
                centroidX,
                centroidY,
                count,
                1d,
                minX,
                minY,
                Math.Max(1, (maxX - minX) + 1),
                Math.Max(1, (maxY - minY) + 1)));
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }

    private static void RgbToHsv(byte rByte, byte gByte, byte bByte, out int h, out int s, out int v)
    {
        var r = rByte / 255d;
        var g = gByte / 255d;
        var b = bByte / 255d;
        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var delta = max - min;

        v = (int)Math.Round(max * 255d);
        s = max <= double.Epsilon ? 0 : (int)Math.Round((delta / max) * 255d);

        if (delta <= double.Epsilon)
        {
            h = 0;
            return;
        }

        double hue;
        if (Math.Abs(max - r) < double.Epsilon)
            hue = 60d * (((g - b) / delta) % 6d);
        else if (Math.Abs(max - g) < double.Epsilon)
            hue = 60d * (((b - r) / delta) + 2d);
        else
            hue = 60d * (((r - g) / delta) + 4d);

        if (hue < 0d)
            hue += 360d;

        h = (int)Math.Round(hue / 2d);
    }
}
