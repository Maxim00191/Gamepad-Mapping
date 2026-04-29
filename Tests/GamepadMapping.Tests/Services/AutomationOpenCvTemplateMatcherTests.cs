#nullable enable

using System.Windows.Media;
using System.Windows.Media.Imaging;
using GamepadMapperGUI.Models.Automation;
using GamepadMapperGUI.Services.Automation;

namespace GamepadMapping.Tests.Services;

public sealed class AutomationOpenCvTemplateMatcherTests
{
    [Fact]
    public void Match_FindsEmbeddedPatchAcrossLargeHaystack()
    {
        const int hw = 640;
        const int hh = 480;
        const int nw = 24;
        const int nh = 18;
        const int ox = 412;
        const int oy = 301;

        var (haystack, needle) = CreateHaystackAndNeedleFromSameBuffer(hw, hh, ox, oy, nw, nh, 210, 55, 90);

        var sut = new AutomationOpenCvTemplateMatcher();
        var options = new AutomationImageProbeOptions(0.25, 500);

        var result = sut.Match(haystack, needle, options);

        Assert.True(result.Matched);
        Assert.Equal(ox, result.MatchX);
        Assert.Equal(oy, result.MatchY);
        Assert.True(result.Confidence >= 0.99);
    }

    [Fact]
    public void GetOrCreateCachedBgrMat_ReusesMatForSameBitmapInstance()
    {
        var bitmap = ToFrozenBitmap(8, 8, new byte[8 * 8 * 4], 8 * 4);

        var first = AutomationBitmapSourceToOpenCvMat.GetOrCreateCachedBgrMat(bitmap);
        var second = AutomationBitmapSourceToOpenCvMat.GetOrCreateCachedBgrMat(bitmap);

        Assert.True(ReferenceEquals(first, second));
    }

    private static (BitmapSource Haystack, BitmapSource Needle) CreateHaystackAndNeedleFromSameBuffer(
        int w,
        int h,
        int patchX,
        int patchY,
        int patchW,
        int patchH,
        byte pr,
        byte pg,
        byte pb)
    {
        var stride = w * 4;
        var pixels = new byte[stride * h];
        var seed = 17;
        for (var i = 0; i < pixels.Length; i += 4)
        {
            seed = seed * 1103515245 + 12345;
            pixels[i] = (byte)(seed >> 16);
            pixels[i + 1] = (byte)(seed >> 8);
            pixels[i + 2] = (byte)seed;
            pixels[i + 3] = 255;
        }

        for (var y = 0; y < patchH; y++)
        {
            var row = (patchY + y) * stride + patchX * 4;
            for (var x = 0; x < patchW; x++)
            {
                var i = row + x * 4;
                var t = (byte)((x * 7 + y * 11) % 200);
                pixels[i] = (byte)Math.Clamp(pb + t / 3, 0, 255);
                pixels[i + 1] = (byte)Math.Clamp(pg + t / 5, 0, 255);
                pixels[i + 2] = (byte)Math.Clamp(pr + t / 2, 0, 255);
                pixels[i + 3] = 255;
            }
        }

        var needlePixels = new byte[patchW * patchH * 4];
        var nStride = patchW * 4;
        for (var y = 0; y < patchH; y++)
        {
            Buffer.BlockCopy(pixels, (patchY + y) * stride + patchX * 4, needlePixels, y * nStride, nStride);
        }

        var haystack = ToFrozenBitmap(w, h, pixels, stride);
        var needle = ToFrozenBitmap(patchW, patchH, needlePixels, nStride);
        return (haystack, needle);
    }

    private static BitmapSource ToFrozenBitmap(int w, int h, byte[] pixels, int stride)
    {
        var bmp = BitmapSource.Create(w, h, 96, 96, PixelFormats.Bgra32, null, pixels, stride);
        bmp.Freeze();
        return bmp;
    }
}
