#nullable enable

using System.IO;
using System.Windows.Media.Imaging;
using GamepadMapperGUI.Interfaces.Services.Automation;
using GamepadMapperGUI.Models.Automation;

namespace GamepadMapperGUI.Services.Automation;

public sealed class AutomationImageProbe(IAutomationTemplateMatcher matcher) : IAutomationImageProbe
{
    private readonly IAutomationTemplateMatcher _matcher = matcher;

    public AutomationImageProbeResult Probe(
        BitmapSource haystack,
        int haystackLeftScreenPx,
        int haystackTopScreenPx,
        BitmapSource? needle,
        AutomationImageProbeOptions options)
    {
        if (needle is null || needle.PixelWidth <= 0 || needle.PixelHeight <= 0)
            return ProbeCenterFallback(haystack, haystackLeftScreenPx, haystackTopScreenPx);

        var match = _matcher.Match(haystack, needle, options);
        if (!match.Matched)
            return new AutomationImageProbeResult(false, 0, 0);

        var x = haystackLeftScreenPx + match.MatchX + needle.PixelWidth / 2;
        var y = haystackTopScreenPx + match.MatchY + needle.PixelHeight / 2;
        return new AutomationImageProbeResult(true, x, y);
    }

    public static BitmapSource? TryLoadBitmapFromPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return null;

        try
        {
            using var fs = File.OpenRead(path);
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = fs;
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch
        {
            return null;
        }
    }

    private static AutomationImageProbeResult ProbeCenterFallback(BitmapSource haystack, int leftPx, int topPx)
    {
        var w = Math.Max(0, haystack.PixelWidth);
        var h = Math.Max(0, haystack.PixelHeight);
        if (w == 0 || h == 0)
            return new AutomationImageProbeResult(false, 0, 0);

        var cx = leftPx + w / 2;
        var cy = topPx + h / 2;
        return new AutomationImageProbeResult(true, cx, cy);
    }
}
