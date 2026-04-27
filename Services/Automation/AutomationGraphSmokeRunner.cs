using GamepadMapperGUI.Interfaces.Services.Automation;
using GamepadMapperGUI.Interfaces.Services.Input;
using GamepadMapperGUI.Models.Automation;
using GamepadMapperGUI.Services.Automation.NodeHandlers;
using System.Diagnostics;

namespace GamepadMapperGUI.Services.Automation;

public sealed class AutomationGraphSmokeRunner : IAutomationGraphSmokeRunner
{
    private readonly IAutomationScreenCaptureService _capture;
    private readonly IAutomationImageProbe _probe;
    private readonly IKeyboardEmulator _keyboard;
    private readonly IMouseEmulator _mouse;
    private readonly IVirtualScreenMouse? _virtualMouse;
    private readonly INodeTypeRegistry _registry;
    private readonly IAutomationTopologyAnalyzer _topology;
    private readonly IAutomationNodeContractValidator _contracts;
    private readonly IAutomationExecutionSafetyPolicy _safetyPolicy;
    private readonly IAutomationInputStateManager _inputState;
    private readonly IHumanInputNoiseController? _humanNoise;
    private readonly IAutomationNodeInputModeResolver _inputModeResolver;
    private readonly IReadOnlyDictionary<string, IAutomationRuntimeNodeHandler> _handlersByNodeType;

    private AutomationExecutionGraphIndex _index = null!;
    private AutomationExecutionSafetyLimits _limits = new();

    public AutomationGraphSmokeRunner(
        IAutomationScreenCaptureService capture,
        IAutomationImageProbe probe,
        IKeyboardEmulator keyboard,
        IMouseEmulator mouse,
        IVirtualScreenMouse? virtualMouse,
        INodeTypeRegistry registry,
        IAutomationTopologyAnalyzer topology,
        IAutomationNodeContractValidator contracts,
        IAutomationExecutionSafetyPolicy safetyPolicy,
        IAutomationInputStateManager? inputState = null,
        IHumanInputNoiseController? humanNoise = null,
        IAutomationNodeInputModeResolver? inputModeResolver = null)
    {
        _capture = capture;
        _probe = probe;
        _keyboard = keyboard;
        _mouse = mouse;
        _virtualMouse = virtualMouse;
        _registry = registry;
        _topology = topology;
        _contracts = contracts;
        _safetyPolicy = safetyPolicy;
        _inputState = inputState ?? new AutomationInputStateManager(keyboard);
        _humanNoise = humanNoise;
        _inputModeResolver = inputModeResolver ?? new AutomationNodeInputModeResolver(keyboard, mouse);
        _handlersByNodeType = BuildHandlers();
    }

    public Task<AutomationSmokeRunResult> RunOnceAsync(AutomationGraphDocument document,
        CancellationToken cancellationToken = default) =>
        Task.Run(() => RunSync(document, cancellationToken), cancellationToken);

    private AutomationSmokeRunResult RunSync(AutomationGraphDocument document, CancellationToken ct)
    {
        var log = new List<string>();
        _index = new AutomationExecutionGraphIndex(document, _registry);
        _limits = _safetyPolicy.GetLimits(document);
        var context = new AutomationRuntimeContext
        {
            Capture = _capture,
            Probe = _probe,
            Keyboard = _keyboard,
            Mouse = _mouse,
            VirtualMouse = _virtualMouse,
            Index = _index,
            Limits = _limits,
            InputState = _inputState,
            HumanNoise = _humanNoise,
            InputModeResolver = _inputModeResolver
        };

        var analysis = _topology.Analyze(document);
        if (analysis.HasExecutionCycle)
            return Fail("AutomationSmoke_Cycle", null, log);
        if (analysis.HasDataCycle)
            return Fail("AutomationTopology_DataCycleDetected", null, log);

        var emptyRoiNode = AutomationRoiPreflight.FindFirstEmptyRoiDocument(document);
        if (emptyRoiNode is not null)
            return Fail("AutomationSmoke_EmptyRoi", emptyRoiNode, log);

        if (_contracts.TryValidate(document, _index, out var contractError))
            return Fail("AutomationSmoke_RunFailed", contractError, log);

        var roots = _index.FindExecutionRoots(_registry);
        if (roots.Count == 0)
            return Fail("AutomationSmoke_NoRoot", null, log);

        var root = roots[0];
        var rootNode = _index.GetNode(root);
        var rootRef = rootNode is null
            ? AutomationLogFormatter.NodeId(root)
            : AutomationLogFormatter.NodeRef(rootNode.NodeTypeId, rootNode.Id);
        log.Add($"[run] root={rootRef}");

        try
        {
            RunFrom(context, root, log, ct);
        }
        catch (OperationCanceledException)
        {
            return new AutomationSmokeRunResult
            {
                Ok = false,
                MessageResourceKey = "AutomationSmoke_Cancelled",
                LogLines = log
            };
        }

        catch (InvalidOperationException ex)
        {
            log.Add(ex.Message);
            return new AutomationSmokeRunResult
            {
                Ok = false,
                MessageResourceKey = "AutomationSmoke_RunFailed",
                Detail = ex.Message,
                LogLines = log
            };
        }

        return new AutomationSmokeRunResult
        {
            Ok = true,
            MessageResourceKey = "AutomationSmoke_Completed",
            LogLines = log
        };
    }

    private void RunFrom(AutomationRuntimeContext context, Guid startNodeId, List<string> log, CancellationToken ct)
    {
        var current = startNodeId;
        var timer = Stopwatch.StartNew();
        var lastTick = timer.Elapsed;
        const double fixedTimestepSeconds = 1d / 60d;
        for (var step = 0; step < _limits.MaxExecutionSteps; step++)
        {
            ct.ThrowIfCancellationRequested();
            var now = timer.Elapsed;
            var elapsedSinceLast = now - lastTick;
            if (elapsedSinceLast.TotalSeconds < fixedTimestepSeconds)
            {
                var remaining = TimeSpan.FromSeconds(fixedTimestepSeconds - elapsedSinceLast.TotalSeconds);
                if (remaining > TimeSpan.Zero)
                    Task.Delay(remaining, ct).GetAwaiter().GetResult();
                now = timer.Elapsed;
            }
            context.DeltaTimeSeconds = Math.Max((now - lastTick).TotalSeconds, fixedTimestepSeconds);
            lastTick = now;

            var node = _index.GetNode(current) ?? throw new InvalidOperationException("node_missing");

            log.Add($"[step:{step}] enter {AutomationLogFormatter.NodeRef(node.NodeTypeId, node.Id)}");

            var next = ExecuteAndGetNext(context, node, log, ct);
            if (next is null)
            {
                log.Add($"[step:{step}] completed terminal_node={AutomationLogFormatter.NodeId(node.Id)}");
                return;
            }

            var nextNode = _index.GetNode(next.Value);
            var nextRef = nextNode is null
                ? AutomationLogFormatter.NodeId(next.Value)
                : AutomationLogFormatter.NodeRef(nextNode.NodeTypeId, nextNode.Id);
            log.Add($"[step:{step}] next={nextRef}");

            current = next.Value;
        }

        throw new InvalidOperationException("AutomationSmoke_StepLimit");
    }

    private Guid? ExecuteAndGetNext(AutomationRuntimeContext context, AutomationNodeState node, List<string> log, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (_handlersByNodeType.TryGetValue(node.NodeTypeId, out var handler))
        {
            log.Add($"[handler] execute {AutomationLogFormatter.NodeRef(node.NodeTypeId, node.Id)}");
            return handler.Execute(context, node, log, ct);
        }

        log.Add($"[handler] missing type={node.NodeTypeId} for_node={AutomationLogFormatter.NodeId(node.Id)} fallback=true");
        var fallbackPort = GetFirstExecOutPort(node.NodeTypeId);
        return string.IsNullOrEmpty(fallbackPort)
            ? null
            : context.GetExecutionTarget(node.Id, fallbackPort);
    }

    private string? GetFirstExecOutPort(string nodeTypeId)
    {
        var def = _registry.GetRequired(nodeTypeId);
        foreach (var p in def.OutputPorts)
        {
            if (p.FlowKind == AutomationPortFlowKind.Execution && p.IsOutput)
                return p.Id;
        }

        return null;
    }

    private static IReadOnlyDictionary<string, IAutomationRuntimeNodeHandler> BuildHandlers()
    {
        IAutomationRuntimeNodeHandler[] handlers =
        [
            new CaptureScreenNodeHandler(),
            new FindImageNodeHandler(),
            new BranchImageNodeHandler(),
            new BranchBoolNodeHandler(),
            new SwitchNodeHandler(),
            new KeyboardKeyNodeHandler(),
            new MouseClickNodeHandler(),
            new LoopNodeHandler(),
            new LoopControlNodeHandler(),
            new DelayNodeHandler(),
            new SetVariableNodeHandler(),
            new LogNodeHandler(),
            new KeyStateNodeHandler(),
            new HumanNoiseNodeHandler()
        ];
        return handlers.ToDictionary(h => h.NodeTypeId, StringComparer.Ordinal);
    }

    private static AutomationSmokeRunResult Fail(string key, string? detail, List<string> log) =>
        new()
        {
            Ok = false,
            MessageResourceKey = key,
            Detail = detail,
            LogLines = log
        };

}
