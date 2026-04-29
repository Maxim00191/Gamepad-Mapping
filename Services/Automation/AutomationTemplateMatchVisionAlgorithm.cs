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
        return ValueTask.FromResult(AutomationTemplateMatchVisionCore.Process(_matcher, frame, cancellationToken));
    }
}
