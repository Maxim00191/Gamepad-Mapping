#nullable enable

using System.IO;
using System.Windows.Media.Imaging;
using GamepadMapperGUI.Interfaces.Services.Automation;
using GamepadMapperGUI.Models.Automation;

namespace GamepadMapperGUI.Services.Automation;

public sealed class AutomationImageProbe(IAutomationVisionPipeline pipeline) : IAutomationImageProbe
{
    private readonly IAutomationVisionPipeline _pipeline = pipeline;

    public async ValueTask<AutomationImageProbeResult> ProbeAsync(
        BitmapSource haystack,
        int haystackLeftScreenPx,
        int haystackTopScreenPx,
        BitmapSource? needle,
        AutomationImageProbeOptions options,
        AutomationVisionAlgorithmKind algorithmKind,
        CancellationToken cancellationToken)
    {
        var requiresNeedle = AutomationVisionAlgorithmRequirements.RequiresNeedleImage(algorithmKind);
        if (requiresNeedle && (needle is null || needle.PixelWidth <= 0 || needle.PixelHeight <= 0))
            return new AutomationImageProbeResult(false, 0, 0);

        var frame = new AutomationVisionFrame(haystack, needle, haystackLeftScreenPx, haystackTopScreenPx, options);
        var result = await _pipeline.ProcessAsync(algorithmKind, frame, cancellationToken);
        if (!result.Matched)
            return new AutomationImageProbeResult(false, 0, 0, 0, 0, GetBestTemplateCorrelation(algorithmKind, result), 0, 0);

        return ToProbeResult(algorithmKind, result, haystackLeftScreenPx, haystackTopScreenPx, needle);
    }

    private static double GetBestTemplateCorrelation(AutomationVisionAlgorithmKind kind, AutomationVisionResult vision)
    {
        if (kind is AutomationVisionAlgorithmKind.TemplateMatch or AutomationVisionAlgorithmKind.OpenCvTemplateMatch)
            return vision.BestTemplateCorrelation;
        return 0;
    }

    private static AutomationImageProbeResult ToProbeResult(
        AutomationVisionAlgorithmKind algorithmKind,
        AutomationVisionResult vision,
        int haystackLeftScreenPx,
        int haystackTopScreenPx,
        BitmapSource? needle)
    {
        return algorithmKind switch
        {
            AutomationVisionAlgorithmKind.TemplateMatch or AutomationVisionAlgorithmKind.OpenCvTemplateMatch =>
                new AutomationImageProbeResult(
                    true,
                    haystackLeftScreenPx + vision.MatchX + (needle?.PixelWidth ?? 0) / 2,
                    haystackTopScreenPx + vision.MatchY + (needle?.PixelHeight ?? 0) / 2,
                    vision.MatchCount,
                    vision.Confidence,
                    vision.Confidence,
                    needle?.PixelWidth ?? 0,
                    needle?.PixelHeight ?? 0),
            AutomationVisionAlgorithmKind.ColorThreshold or
                AutomationVisionAlgorithmKind.Contour or
                AutomationVisionAlgorithmKind.YoloOnnx or
                AutomationVisionAlgorithmKind.TextRegion or
                AutomationVisionAlgorithmKind.OcrPhraseMatch =>
                new AutomationImageProbeResult(
                    true,
                    haystackLeftScreenPx + vision.MatchX,
                    haystackTopScreenPx + vision.MatchY,
                    vision.MatchCount,
                    vision.Confidence,
                    0,
                    Math.Max(0, vision.BoundingWidth),
                    Math.Max(0, vision.BoundingHeight)),
            _ => throw new ArgumentOutOfRangeException(nameof(algorithmKind), algorithmKind, null)
        };
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

}
