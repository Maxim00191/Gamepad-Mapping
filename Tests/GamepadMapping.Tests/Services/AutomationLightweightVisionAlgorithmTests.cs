#nullable enable

using System.Windows.Media;
using System.Windows.Media.Imaging;
using GamepadMapperGUI.Models.Automation;
using GamepadMapperGUI.Services.Automation;

namespace GamepadMapping.Tests.Services;

public sealed class AutomationLightweightVisionAlgorithmTests
{
    [Fact]
    public async Task ColorThreshold_UsesConfiguredHsvRangeAndMinimumArea()
    {
        var sut = new AutomationColorThresholdVisionAlgorithm();
        var image = CreateBitmap(8, 8, (255, 255, 255));
        PaintRect(image.Pixels, image.Width, 3, 4, 2, 2, (0, 0, 255));
        var frame = new AutomationVisionFrame(
            image.ToBitmapSource(),
            null,
            0,
            0,
            new AutomationImageProbeOptions(
                0.1,
                500,
                ColorDetectionOptions: new AutomationColorDetectionOptions(175, 5, 120, 255, 120, 255, 4)));

        var result = await sut.ProcessAsync(frame, CancellationToken.None);

        Assert.True(result.Matched);
        Assert.Equal(3, result.MatchX);
        Assert.Equal(4, result.MatchY);
        Assert.Equal(4, result.MatchCount);
    }

    [Fact]
    public async Task ColorThreshold_RejectsRegionsBelowMinimumArea()
    {
        var sut = new AutomationColorThresholdVisionAlgorithm();
        var image = CreateBitmap(8, 8, (255, 255, 255));
        PaintRect(image.Pixels, image.Width, 3, 4, 2, 2, (0, 0, 255));
        var frame = new AutomationVisionFrame(
            image.ToBitmapSource(),
            null,
            0,
            0,
            new AutomationImageProbeOptions(
                0.1,
                500,
                ColorDetectionOptions: new AutomationColorDetectionOptions(175, 5, 120, 255, 120, 255, 5)));

        var result = await sut.ProcessAsync(frame, CancellationToken.None);

        Assert.False(result.Matched);
    }

    [Fact]
    public async Task TextRegion_FindsLikelyTextBlockWithoutTemplateOrModel()
    {
        var sut = new AutomationTextRegionVisionAlgorithm();
        var image = CreateBitmap(80, 32, (255, 255, 255));
        PaintRect(image.Pixels, image.Width, 20, 10, 3, 12, (0, 0, 0));
        PaintRect(image.Pixels, image.Width, 28, 10, 3, 12, (0, 0, 0));
        PaintRect(image.Pixels, image.Width, 36, 10, 3, 12, (0, 0, 0));
        var frame = new AutomationVisionFrame(
            image.ToBitmapSource(),
            null,
            0,
            0,
            new AutomationImageProbeOptions(
                0.1,
                500,
                TextDetectionOptions: new AutomationTextDetectionOptions(8, 9, 3)));

        var result = await sut.ProcessAsync(frame, CancellationToken.None);

        Assert.True(result.Matched);
        Assert.InRange(result.MatchX, 18, 42);
        Assert.InRange(result.MatchY, 8, 24);
        Assert.True(result.MatchCount >= 1);
    }

    private static TestBitmap CreateBitmap(int width, int height, (byte B, byte G, byte R) color)
    {
        var pixels = new byte[width * height * 4];
        for (var i = 0; i < pixels.Length; i += 4)
        {
            pixels[i] = color.B;
            pixels[i + 1] = color.G;
            pixels[i + 2] = color.R;
            pixels[i + 3] = 255;
        }

        return new TestBitmap(width, height, pixels);
    }

    private static void PaintRect(
        byte[] pixels,
        int width,
        int left,
        int top,
        int rectWidth,
        int rectHeight,
        (byte B, byte G, byte R) color)
    {
        for (var y = top; y < top + rectHeight; y++)
        {
            for (var x = left; x < left + rectWidth; x++)
            {
                var i = ((y * width) + x) * 4;
                pixels[i] = color.B;
                pixels[i + 1] = color.G;
                pixels[i + 2] = color.R;
                pixels[i + 3] = 255;
            }
        }
    }

    private sealed record TestBitmap(int Width, int Height, byte[] Pixels)
    {
        public BitmapSource ToBitmapSource()
        {
            var bitmap = BitmapSource.Create(Width, Height, 96, 96, PixelFormats.Bgra32, null, Pixels, Width * 4);
            bitmap.Freeze();
            return bitmap;
        }
    }
}
