#nullable enable

using GamepadMapperGUI.Interfaces.Services.Automation;
using GamepadMapperGUI.Models.Automation;

namespace GamepadMapperGUI.Services.Automation;

public sealed class AutomationTemplateMatchVisionAlgorithm(IAutomationTemplateMatcher matcher) : IAutomationVisionAlgorithm
{
    private readonly IAutomationTemplateMatcher _matcher = matcher;

    public AutomationVisionAlgorithmKind Kind => AutomationVisionAlgorithmKind.TemplateMatch;

    public ValueTask<AutomationVisionResult> ProcessAsync(AutomationVisionFrame frame, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (frame.Needle is null || frame.Needle.PixelWidth <= 0 || frame.Needle.PixelHeight <= 0)
            return ValueTask.FromResult(new AutomationVisionResult(false, 0, 0));

        var match = _matcher.Match(frame.Image, frame.Needle, frame.ProbeOptions);
        return ValueTask.FromResult(new AutomationVisionResult(
            match.Matched,
            match.MatchX,
            match.MatchY,
            match.Matched ? 1 : 0,
            match.Matched ? Math.Clamp(1d - frame.ProbeOptions.Tolerance01, 0d, 1d) : 0d));
    }
}
