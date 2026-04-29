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
        using var hMat = AutomationBitmapSourceToOpenCvMat.ToBgrMat(haystack);
        using var nMat = AutomationBitmapSourceToOpenCvMat.ToBgrMat(needle);
        var searchW = hMat.Width - nMat.Width + 1;
        var searchH = hMat.Height - nMat.Height + 1;
        if (searchW <= 0 || searchH <= 0)
            return new AutomationTemplateMatchResult(false, 0, 0, 0);

        using var matchResult = new Mat();
        Cv2.MatchTemplate(hMat, nMat, matchResult, TemplateMatchModes.CCoeffNormed);
        Cv2.MinMaxLoc(matchResult, out _, out var maxVal, out _, out var maxLoc);

        var minScore = 1.0 - Math.Clamp(options.Tolerance01, 0, 0.9);
        if (maxVal < minScore)
            return new AutomationTemplateMatchResult(false, 0, 0, maxVal);

        return new AutomationTemplateMatchResult(true, maxLoc.X, maxLoc.Y, maxVal);
    }
}
