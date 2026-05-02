#nullable enable

using GamepadMapperGUI.Models.Automation;

namespace GamepadMapperGUI.Interfaces.Services.Automation;

public interface IAutomationVisionPipeline
{
    ValueTask<AutomationVisionResult> ProcessAsync(
        AutomationVisionAlgorithmKind kind,
        AutomationVisionFrame frame,
        CancellationToken cancellationToken);
}
