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
    private Dictionary<string, AutomationSubgraphDefinition> _subgraphsById = new(StringComparer.Ordinal);
    private Dictionary<string, List<Guid>> _eventListenerNodeIdsBySignal = new(StringComparer.OrdinalIgnoreCase);
    private Queue<Guid> _pendingExecutionStarts = new();
    private HashSet<Guid> _queuedExecutionStarts = [];

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
        _subgraphsById = document.Subgraphs
            .Where(s => !string.IsNullOrWhiteSpace(s.Id))
            .ToDictionary(s => s.Id.Trim(), s => s, StringComparer.Ordinal);
        _eventListenerNodeIdsBySignal = BuildEventListenerLookup(document);
        _pendingExecutionStarts = new Queue<Guid>();
        _queuedExecutionStarts = [];
        var eventBus = new AutomationEventBus();
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
            InputModeResolver = _inputModeResolver,
            EventBus = eventBus
        };
        context.EventBus.Subscribe(signal => OnEventSignal(document, signal, log));

        var analysis = _topology.Analyze(document);
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
        EnqueueInitialStarts(document, roots, log);

        try
        {
            var timer = Stopwatch.StartNew();
            var lastTick = timer.Elapsed;
            var consumedSteps = 0;
            while (_pendingExecutionStarts.Count > 0)
            {
                var startNodeId = _pendingExecutionStarts.Dequeue();
                _queuedExecutionStarts.Remove(startNodeId);
                consumedSteps += RunFrom(context, _index, startNodeId, log, timer, ref lastTick, _limits.MaxExecutionSteps - consumedSteps, ct);
                if (consumedSteps >= _limits.MaxExecutionSteps)
                    throw new InvalidOperationException("AutomationSmoke_StepLimit");
            }
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

    private int RunFrom(
        AutomationRuntimeContext context,
        AutomationExecutionGraphIndex index,
        Guid startNodeId,
        List<string> log,
        Stopwatch timer,
        ref TimeSpan lastTick,
        int availableSteps,
        CancellationToken ct)
    {
        if (availableSteps <= 0)
            return 0;

        var previousIndex = context.Index;
        context.Index = index;
        var current = startNodeId;
        const double fixedTimestepSeconds = 1d / 60d;
        for (var step = 0; step < availableSteps; step++)
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

            var node = index.GetNode(current) ?? throw new InvalidOperationException("node_missing");

            log.Add($"[step:{step}] enter {AutomationLogFormatter.NodeRef(node.NodeTypeId, node.Id)}");

            var next = ExecuteAndGetNext(context, index, node, log, ct);
            if (next is null)
            {
                log.Add($"[step:{step}] completed terminal_node={AutomationLogFormatter.NodeId(node.Id)}");
                context.Index = previousIndex;
                return step + 1;
            }

            var nextNode = index.GetNode(next.Value);
            var nextRef = nextNode is null
                ? AutomationLogFormatter.NodeId(next.Value)
                : AutomationLogFormatter.NodeRef(nextNode.NodeTypeId, nextNode.Id);
            log.Add($"[step:{step}] next={nextRef}");

            current = next.Value;
        }

        context.Index = previousIndex;
        return availableSteps;
    }

    private Guid? ExecuteAndGetNext(
        AutomationRuntimeContext context,
        AutomationExecutionGraphIndex index,
        AutomationNodeState node,
        List<string> log,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (string.Equals(node.NodeTypeId, "automation.macro", StringComparison.Ordinal))
            return ExecuteMacroNode(context, node, log, ct);
        if (string.Equals(node.NodeTypeId, "event.listener", StringComparison.Ordinal))
            return context.GetExecutionTarget(node.Id, "flow.out");

        if (_handlersByNodeType.TryGetValue(node.NodeTypeId, out var handler))
        {
            log.Add($"[handler] execute {AutomationLogFormatter.NodeRef(node.NodeTypeId, node.Id)}");
            return handler.Execute(context, node, log, ct);
        }

        log.Add($"[handler] missing type={node.NodeTypeId} for_node={AutomationLogFormatter.NodeId(node.Id)} fallback=true");
        var fallbackPort = GetFirstExecOutPort(node.NodeTypeId);
        return string.IsNullOrEmpty(fallbackPort)
            ? null
            : index.GetExecutionTarget(node.Id, fallbackPort);
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

    private void EnqueueInitialStarts(AutomationGraphDocument document, IReadOnlyList<Guid> roots, List<string> log)
    {
        foreach (var root in roots)
        {
            var node = _index.GetNode(root);
            if (node is null || string.Equals(node.NodeTypeId, "event.listener", StringComparison.Ordinal))
                continue;

            EnqueueStart(root);
        }

        OnEventSignal(document, "engine.start", log);
    }

    private Dictionary<string, List<Guid>> BuildEventListenerLookup(AutomationGraphDocument document)
    {
        var map = new Dictionary<string, List<Guid>>(StringComparer.OrdinalIgnoreCase);
        foreach (var node in document.Nodes)
        {
            if (!string.Equals(node.NodeTypeId, "event.listener", StringComparison.Ordinal))
                continue;

            var signal = AutomationNodePropertyReader.ReadString(node.Properties, AutomationNodePropertyKeys.EventSignal);
            if (string.IsNullOrWhiteSpace(signal))
                continue;

            var key = signal.Trim();
            if (!map.TryGetValue(key, out var list))
            {
                list = [];
                map[key] = list;
            }

            list.Add(node.Id);
        }

        return map;
    }

    private void OnEventSignal(AutomationGraphDocument document, string signal, List<string> log)
    {
        if (string.IsNullOrWhiteSpace(signal))
            return;

        if (_eventListenerNodeIdsBySignal.TryGetValue(signal.Trim(), out var listeners))
        {
            foreach (var listenerNodeId in listeners)
            {
                var next = _index.GetExecutionTarget(listenerNodeId, "flow.out");
                if (next is Guid nextNodeId)
                    EnqueueStart(nextNodeId);
            }
        }
    }

    private void EnqueueStart(Guid nodeId)
    {
        if (_queuedExecutionStarts.Add(nodeId))
            _pendingExecutionStarts.Enqueue(nodeId);
    }

    private Guid? ExecuteMacroNode(AutomationRuntimeContext context, AutomationNodeState node, List<string> log, CancellationToken ct)
    {
        var subgraphId = AutomationNodePropertyReader.ReadString(node.Properties, AutomationNodePropertyKeys.MacroSubgraphId);
        if (string.IsNullOrWhiteSpace(subgraphId))
            throw new InvalidOperationException("macro:subgraph_missing");

        if (!_subgraphsById.TryGetValue(subgraphId.Trim(), out var subgraph))
            throw new InvalidOperationException("macro:subgraph_not_found");

        var subgraphIndex = new AutomationExecutionGraphIndex(subgraph.Graph, _registry);
        var roots = subgraphIndex.FindExecutionRoots(_registry);
        if (roots.Count == 0)
            throw new InvalidOperationException("macro:subgraph_root_missing");

        var macroTimer = Stopwatch.StartNew();
        var macroLastTick = macroTimer.Elapsed;
        RunFrom(context, subgraphIndex, roots[0], log, macroTimer, ref macroLastTick, _limits.MaxExecutionSteps, ct);
        return context.GetExecutionTarget(node.Id, "flow.out");
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
            new HumanNoiseNodeHandler(),
            new EventEmitNodeHandler()
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
