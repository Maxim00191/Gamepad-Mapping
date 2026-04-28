#nullable enable

using GamepadMapperGUI.Interfaces.Services.Automation;
using GamepadMapperGUI.Interfaces.Services.Input;
using GamepadMapperGUI.Models.Automation;

namespace GamepadMapperGUI.Services.Automation;

public sealed class AutomationExecutionServicesFactory : IAutomationExecutionServicesFactory
{
    public AutomationExecutionServices Create(
        IKeyboardEmulator keyboard,
        IMouseEmulator mouse,
        IHumanInputNoiseController? humanNoise = null,
        IAutomationNodeInputModeResolver? inputModeResolver = null)
    {
        ArgumentNullException.ThrowIfNull(keyboard);
        ArgumentNullException.ThrowIfNull(mouse);

        var registry = new NodeTypeRegistry();
        var capture = new AutomationScreenCaptureGdiService();
        var topology = new AutomationTopologyAnalyzer(registry);
        var contracts = new AutomationNodeContractValidator();
        var safety = new AutomationExecutionSafetyPolicy();
        var templateMatcher = new AutomationTemplateMatcherBruteForce();
        var visionPipeline = AutomationVisionAlgorithmComposition.CreateDefaultPipeline(templateMatcher);
        var probe = new AutomationImageProbe(visionPipeline);
        var inputState = new AutomationInputStateManager(keyboard);
        var runner = new AutomationGraphSmokeRunner(
            capture,
            probe,
            keyboard,
            mouse,
            mouse as IVirtualScreenMouse,
            registry,
            topology,
            contracts,
            safety,
            inputState,
            humanNoise,
            inputModeResolver);

        return new AutomationExecutionServices
        {
            NodeRegistry = registry,
            ScreenCapture = capture,
            TopologyAnalyzer = topology,
            ContractValidator = contracts,
            SafetyPolicy = safety,
            ImageProbe = probe,
            InputStateManager = inputState,
            SmokeRunner = runner
        };
    }
}
