#nullable enable

using System.Windows.Media;
using System.Windows.Media.Imaging;
using GamepadMapperGUI.Interfaces.Services.Automation;
using GamepadMapperGUI.Models.Automation;

namespace GamepadMapperGUI.Services.Automation;

public sealed class AutomationTemplateMatcherBruteForce : IAutomationTemplateMatcher
{
    public AutomationTemplateMatchResult Match(
        BitmapSource haystack,
        BitmapSource needle,
        AutomationImageProbeOptions options,
        CancellationToken cancellationToken = default)
    {
        var h = EnsureBgra32(haystack);
        var n = EnsureBgra32(needle);
        var searchW = h.PixelWidth - n.PixelWidth + 1;
        var searchH = h.PixelHeight - n.PixelHeight + 1;
        if (searchW <= 0 || searchH <= 0)
            return new AutomationTemplateMatchResult(false, 0, 0, 0);

        var hStride = h.PixelWidth * 4;
        var nStride = n.PixelWidth * 4;
        var hPixels = new byte[hStride * h.PixelHeight];
        var nPixels = new byte[nStride * n.PixelHeight];
        h.CopyPixels(hPixels, hStride, 0);
        n.CopyPixels(nPixels, nStride, 0);

        var bestScore = double.MinValue;
        var bestX = 0;
        var bestY = 0;
        var endAt = DateTime.UtcNow.AddMilliseconds(Math.Max(25, options.TimeoutMs));

        for (var y = 0; y < searchH; y++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (DateTime.UtcNow >= endAt)
                break;

            for (var x = 0; x < searchW; x++)
            {
                var score = ComputeScore(hPixels, hStride, nPixels, nStride, x, y, n.PixelWidth, n.PixelHeight);
                if (score <= bestScore)
                    continue;

                bestScore = score;
                bestX = x;
                bestY = y;
            }
        }

        var threshold = 1.0 - Math.Clamp(options.Tolerance01, 0, 0.9);
        if (bestScore < threshold)
            return new AutomationTemplateMatchResult(false, 0, 0, bestScore);

        return new AutomationTemplateMatchResult(true, bestX, bestY, bestScore);
    }

    private static BitmapSource EnsureBgra32(BitmapSource source)
    {
        if (source.Format == PixelFormats.Bgra32)
            return source;

        var converted = new FormatConvertedBitmap();
        converted.BeginInit();
        converted.Source = source;
        converted.DestinationFormat = PixelFormats.Bgra32;
        converted.EndInit();
        converted.Freeze();
        return converted;
    }

    private static double ComputeScore(
        byte[] haystack,
        int haystackStride,
        byte[] needle,
        int needleStride,
        int offsetX,
        int offsetY,
        int width,
        int height)
    {
        long acc = 0;
        var samples = width * height;
        for (var y = 0; y < height; y++)
        {
            var hRow = (offsetY + y) * haystackStride + offsetX * 4;
            var nRow = y * needleStride;
            for (var x = 0; x < width; x++)
            {
                var hi = hRow + x * 4;
                var ni = nRow + x * 4;
                var db = Math.Abs(haystack[hi] - needle[ni]);
                var dg = Math.Abs(haystack[hi + 1] - needle[ni + 1]);
                var dr = Math.Abs(haystack[hi + 2] - needle[ni + 2]);
                acc += db + dg + dr;
            }
        }

        var maxDiff = samples * 255.0 * 3.0;
        return 1.0 - (acc / maxDiff);
    }
}
