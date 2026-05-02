#nullable enable

using System.Windows.Media.Imaging;
using GamepadMapperGUI.Interfaces.Services.Automation;
using GamepadMapperGUI.Models.Automation;
using OpenCvSharp;

namespace GamepadMapperGUI.Services.Automation;

public sealed class AutomationOpenCvTemplateMatcher : IAutomationTemplateMatcher
{
    public AutomationTemplateMatchResult Match(
        BitmapSource haystack,
        BitmapSource needle,
        AutomationImageProbeOptions options,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var hMat = AutomationBitmapSourceToOpenCvMat.GetOrCreateCachedBgrMat(haystack);
        var nMat = AutomationBitmapSourceToOpenCvMat.GetOrCreateCachedBgrMat(needle);
        var searchW = hMat.Width - nMat.Width + 1;
        var searchH = hMat.Height - nMat.Height + 1;
        if (searchW <= 0 || searchH <= 0)
            return new AutomationTemplateMatchResult(false, 0, 0, 0);

        var minScore = options.ResolveTemplateMatchMinNormalizedCorrelation();

        if (ShouldTryCoarseFine(hMat, nMat))
        {
            var refined = TryCoarseFineMatch(hMat, nMat, minScore, cancellationToken);
            if (refined is not null)
                return refined.Value;
        }

        return MatchTemplateFull(hMat, nMat, minScore, cancellationToken);
    }

    private static bool ShouldTryCoarseFine(Mat haystack, Mat needle)
    {
        if (haystack.Width * haystack.Height < AutomationVisionTemplateMatchTuning.MinHaystackPixelsForCoarseFine)
            return false;
        if (needle.Width < AutomationVisionTemplateMatchTuning.MinNeedleDimensionPx ||
            needle.Height < AutomationVisionTemplateMatchTuning.MinNeedleDimensionPx)
            return false;
        var maxDim = Math.Max(haystack.Width, haystack.Height);
        var scale = AutomationVisionTemplateMatchTuning.CoarseMaxSidePx / (double)maxDim;
        return scale < AutomationVisionTemplateMatchTuning.MinScaleRatioBeforeFullFrameMatch;
    }

    private static AutomationTemplateMatchResult? TryCoarseFineMatch(
        Mat hMat,
        Mat nMat,
        double minScore,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var maxDim = Math.Max(hMat.Width, hMat.Height);
        var scale = AutomationVisionTemplateMatchTuning.CoarseMaxSidePx / (double)maxDim;
        if (scale >= AutomationVisionTemplateMatchTuning.MinScaleRatioBeforeFullFrameMatch)
            return null;

        var coarseHw = Math.Max(1, (int)Math.Round(hMat.Width * scale));
        var coarseHh = Math.Max(1, (int)Math.Round(hMat.Height * scale));
        var coarseNw = Math.Max(1, (int)Math.Round(nMat.Width * scale));
        var coarseNh = Math.Max(1, (int)Math.Round(nMat.Height * scale));
        if (coarseHw < coarseNw || coarseHh < coarseNh)
            return null;

        using var coarseHay = new Mat();
        using var coarseNeedle = new Mat();
        Cv2.Resize(hMat, coarseHay, new Size(coarseHw, coarseHh), 0, 0, InterpolationFlags.Area);
        Cv2.Resize(nMat, coarseNeedle, new Size(coarseNw, coarseNh), 0, 0, InterpolationFlags.Area);

        var cSearchW = coarseHay.Width - coarseNeedle.Width + 1;
        var cSearchH = coarseHay.Height - coarseNeedle.Height + 1;
        if (cSearchW <= 0 || cSearchH <= 0)
            return null;

        using var coarseResult = new Mat();
        Cv2.MatchTemplate(coarseHay, coarseNeedle, coarseResult, TemplateMatchModes.CCoeffNormed);
        Cv2.MinMaxLoc(coarseResult, out _, out var coarseMax, out _, out var coarseLoc);
        if (coarseMax < Math.Max(
                AutomationVisionTemplateMatchTuning.CoarseCorrelationAbsoluteFloor,
                minScore * AutomationVisionTemplateMatchTuning.CoarseCorrelationFloorRatio))
            return null;

        var fx = coarseLoc.X * (hMat.Width / (double)coarseHay.Width);
        var fy = coarseLoc.Y * (hMat.Height / (double)coarseHay.Height);
        var margin = Math.Max(12, Math.Max(nMat.Width, nMat.Height));
        var roiX = (int)Math.Floor(fx - margin * 0.25);
        var roiY = (int)Math.Floor(fy - margin * 0.25);
        roiX = Math.Clamp(roiX, 0, Math.Max(0, hMat.Width - nMat.Width));
        roiY = Math.Clamp(roiY, 0, Math.Max(0, hMat.Height - nMat.Height));

        var roiW = Math.Min(hMat.Width - roiX, nMat.Width + margin * 2);
        var roiH = Math.Min(hMat.Height - roiY, nMat.Height + margin * 2);
        if (roiW < nMat.Width || roiH < nMat.Height)
            return null;

        using var roi = new Mat(hMat, new Rect(roiX, roiY, roiW, roiH));
        using var refinedResult = new Mat();
        Cv2.MatchTemplate(roi, nMat, refinedResult, TemplateMatchModes.CCoeffNormed);
        Cv2.MinMaxLoc(refinedResult, out _, out var refinedMax, out _, out var refinedLoc);

        if (refinedMax < minScore)
            return null;

        return new AutomationTemplateMatchResult(true, roiX + refinedLoc.X, roiY + refinedLoc.Y, refinedMax);
    }

    private static AutomationTemplateMatchResult MatchTemplateFull(
        Mat hMat,
        Mat nMat,
        double minScore,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var matchResult = new Mat();
        Cv2.MatchTemplate(hMat, nMat, matchResult, TemplateMatchModes.CCoeffNormed);
        Cv2.MinMaxLoc(matchResult, out _, out var maxVal, out _, out var maxLoc);

        if (maxVal < minScore)
            return new AutomationTemplateMatchResult(false, 0, 0, maxVal);

        return new AutomationTemplateMatchResult(true, maxLoc.X, maxLoc.Y, maxVal);
    }
}
