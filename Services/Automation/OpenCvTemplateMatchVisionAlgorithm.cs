#nullable enable

using GamepadMapperGUI.Interfaces.Services.Automation;
using GamepadMapperGUI.Models.Automation;
using OpenCvSharp;

namespace GamepadMapperGUI.Services.Automation;

public sealed class OpenCvTemplateMatchVisionAlgorithm : IAutomationVisionAlgorithm
{
    public AutomationVisionAlgorithmKind Kind => AutomationVisionAlgorithmKind.OpenCvTemplateMatch;

    public ValueTask<AutomationVisionResult> ProcessAsync(AutomationVisionFrame frame, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (frame.Needle is null || frame.Needle.PixelWidth <= 0 || frame.Needle.PixelHeight <= 0)
            return ValueTask.FromResult(new AutomationVisionResult(false, 0, 0));

        using var haystack = AutomationBitmapSourceToOpenCvMat.ToBgrMat(frame.Image);
        using var needle = AutomationBitmapSourceToOpenCvMat.ToBgrMat(frame.Needle);
        var searchW = haystack.Width - needle.Width + 1;
        var searchH = haystack.Height - needle.Height + 1;
        if (searchW <= 0 || searchH <= 0)
            return ValueTask.FromResult(new AutomationVisionResult(false, 0, 0));

        var deadline = DateTime.UtcNow.AddMilliseconds(Math.Max(25, frame.ProbeOptions.TimeoutMs));
        cancellationToken.ThrowIfCancellationRequested();
        if (DateTime.UtcNow >= deadline)
            return ValueTask.FromResult(new AutomationVisionResult(false, 0, 0));

        using var matchResult = new Mat();
        Cv2.MatchTemplate(haystack, needle, matchResult, TemplateMatchModes.CCoeffNormed);
        Cv2.MinMaxLoc(matchResult, out _, out var maxVal, out _, out OpenCvSharp.Point maxLoc);

        var minScore = 1.0 - Math.Clamp(frame.ProbeOptions.Tolerance01, 0, 0.9);
        if (maxVal < minScore)
            return ValueTask.FromResult(new AutomationVisionResult(false, 0, 0));

        var conf = Math.Clamp(maxVal, 0d, 1d);
        return ValueTask.FromResult(new AutomationVisionResult(true, maxLoc.X, maxLoc.Y, 1, conf));
    }
}
