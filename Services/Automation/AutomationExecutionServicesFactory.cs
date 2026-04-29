#nullable enable

using GamepadMapperGUI.Interfaces.Services.Automation;
using GamepadMapperGUI.Interfaces.Services.Infrastructure;
using GamepadMapperGUI.Interfaces.Services.Input;
using GamepadMapperGUI.Models.Automation;

namespace GamepadMapperGUI.Services.Automation;

public sealed class AutomationExecutionServicesFactory : IAutomationExecutionServicesFactory
{
    public AutomationExecutionServices Create(
        IKeyboardEmulator keyboard,
        IMouseEmulator mouse,
        IHumanInputNoiseController? humanNoise = null,
        IAutomationNodeInputModeResolver? inputModeResolver = null,
        IProcessTargetService? processTargetService = null)
    {
        ArgumentNullException.ThrowIfNull(keyboard);
        ArgumentNullException.ThrowIfNull(mouse);

        var registry = new NodeTypeRegistry();
        var processWindowResolver = new AutomationProcessWindowResolver();
        var capture = new AutomationScreenCaptureGdiService(processWindowResolver);
        var duplication = new AutomationScreenCaptureDesktopDuplicationService(capture);
        var captureResolver = new AutomationScreenCaptureServiceResolver(
            new Dictionary<string, IAutomationScreenCaptureService>(StringComparer.OrdinalIgnoreCase)
            {
                [AutomationCaptureApi.Gdi] = capture,
                [AutomationCaptureApi.DesktopDuplication] = duplication
            },
            AutomationCaptureApi.DesktopDuplication);
        var topology = new AutomationTopologyAnalyzer(registry);
        var contracts = new AutomationNodeContractValidator();
        var safety = new AutomationExecutionSafetyPolicy();
        var visionPipeline = AutomationVisionAlgorithmComposition.CreateDefaultPipeline();
        var probe = new AutomationImageProbe(visionPipeline);
        var inputState = new AutomationInputStateManager(keyboard);
        var outputGuard = processTargetService is null
            ? null
            : new AutomationRuntimeOutputGuard(processTargetService);
        var processWindowInputDispatcher = new AutomationProcessWindowInputDispatcher(processWindowResolver);
        var runner = new AutomationGraphSmokeRunner(
            captureResolver,
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
            inputModeResolver,
            outputGuard,
            processWindowInputDispatcher,
            processTargetService: processTargetService);

        return new AutomationExecutionServices
        {
            NodeRegistry = registry,
            ScreenCapture = capture,
            ScreenCaptureResolver = captureResolver,
            TopologyAnalyzer = topology,
            ContractValidator = contracts,
            SafetyPolicy = safety,
            ImageProbe = probe,
            InputStateManager = inputState,
            SmokeRunner = runner
        };
    }
}
