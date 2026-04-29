#nullable enable

using GamepadMapperGUI.Interfaces.Services.Automation;
using GamepadMapperGUI.Models.Automation;

namespace GamepadMapperGUI.Services.Automation;

public sealed class OpenCvTemplateMatchVisionAlgorithm(IAutomationTemplateMatcher matcher) : IAutomationVisionAlgorithm
{
    private readonly IAutomationTemplateMatcher _matcher = matcher;

    public AutomationVisionAlgorithmKind Kind => AutomationVisionAlgorithmKind.OpenCvTemplateMatch;

    public ValueTask<AutomationVisionResult> ProcessAsync(AutomationVisionFrame frame, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (frame.Needle is null || frame.Needle.PixelWidth <= 0 || frame.Needle.PixelHeight <= 0)
            return ValueTask.FromResult(new AutomationVisionResult(false, 0, 0));

        var match = _matcher.Match(frame.Image, frame.Needle, frame.ProbeOptions, cancellationToken);
        if (!match.Matched)
            return ValueTask.FromResult(new AutomationVisionResult(false, 0, 0));

        var conf = Math.Clamp(match.Confidence, 0d, 1d);
        return ValueTask.FromResult(new AutomationVisionResult(true, match.MatchX, match.MatchY, 1, conf));
    }
}
