#nullable enable

using GamepadMapperGUI.Interfaces.Services.Automation;
using GamepadMapperGUI.Models.Automation;

namespace GamepadMapperGUI.Services.Automation;

internal static class AutomationTemplateMatchVisionCore
{
    public static AutomationVisionResult Process(
        IAutomationTemplateMatcher matcher,
        AutomationVisionFrame frame,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (frame.Needle is null || frame.Needle.PixelWidth <= 0 || frame.Needle.PixelHeight <= 0)
            return new AutomationVisionResult(false, 0, 0);

        var match = matcher.Match(frame.Image, frame.Needle, frame.ProbeOptions, cancellationToken);
        if (!match.Matched)
            return new AutomationVisionResult(false, 0, 0, 0, 0, 0, 0, 0, 0, match.Confidence);

        var conf = Math.Clamp(match.Confidence, 0d, 1d);
        return new AutomationVisionResult(true, match.MatchX, match.MatchY, 1, conf);
    }
}
