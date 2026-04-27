#nullable enable

using GamepadMapperGUI.Interfaces.Services.Automation;
using GamepadMapperGUI.Models.Automation;

namespace GamepadMapperGUI.Services.Automation;

public sealed class AutomationContourVisionAlgorithm(AutomationColorThresholdVisionAlgorithm threshold) : IAutomationVisionAlgorithm
{
    private readonly AutomationColorThresholdVisionAlgorithm _threshold = threshold;

    public AutomationVisionAlgorithmKind Kind => AutomationVisionAlgorithmKind.Contour;

    public async ValueTask<AutomationVisionResult> ProcessAsync(AutomationVisionFrame frame, CancellationToken cancellationToken)
    {
        var masked = await _threshold.ProcessAsync(frame, cancellationToken);
        if (!masked.Matched)
            return masked;

        // Current implementation returns dominant mask envelope as contour proxy.
        return masked with { Confidence = Math.Max(masked.Confidence, 0.9d) };
    }
}
