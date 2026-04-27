#nullable enable

using GamepadMapperGUI.Interfaces.Services.Automation;

namespace GamepadMapperGUI.Models.Automation;

public sealed class AutomationExecutionServices
{
    public required INodeTypeRegistry NodeRegistry { get; init; }

    public required IAutomationScreenCaptureService ScreenCapture { get; init; }

    public required IAutomationTopologyAnalyzer TopologyAnalyzer { get; init; }

    public required IAutomationNodeContractValidator ContractValidator { get; init; }

    public required IAutomationExecutionSafetyPolicy SafetyPolicy { get; init; }

    public required IAutomationImageProbe ImageProbe { get; init; }

    public required IAutomationInputStateManager InputStateManager { get; init; }

    public required IAutomationGraphSmokeRunner SmokeRunner { get; init; }
}
