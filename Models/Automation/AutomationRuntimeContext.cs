#nullable enable

using System.Text.Json.Nodes;
using System.Windows.Media.Imaging;
using GamepadMapperGUI.Interfaces.Services.Automation;
using GamepadMapperGUI.Interfaces.Services.Input;
using GamepadMapperGUI.Services.Automation;

namespace GamepadMapperGUI.Models.Automation;

public sealed class AutomationRuntimeContext
{
    private sealed record ScreenBundle(BitmapSource Bitmap, int OriginScreenX, int OriginScreenY);

    private readonly Dictionary<Guid, ScreenBundle> _bundles = [];
    private readonly Dictionary<Guid, AutomationImageProbeResult> _probeResults = [];
    private readonly Dictionary<Guid, int> _loopCounters = [];
    private readonly Dictionary<string, AutomationDataValue> _variables = new(StringComparer.Ordinal);
    private readonly Dictionary<(Guid NodeId, string PortId), AutomationDataValue> _dataEvaluationCache = [];
    private readonly Dictionary<Guid, object> _stateByNodeId = [];
    private readonly Random _random = new();

    public required IAutomationScreenCaptureService Capture { get; init; }

    public required IAutomationImageProbe Probe { get; init; }

    public required IKeyboardEmulator Keyboard { get; init; }

    public required IMouseEmulator Mouse { get; init; }

    public IVirtualScreenMouse? VirtualMouse { get; init; }

    public required IAutomationExecutionGraphIndex Index { get; set; }

    public required AutomationExecutionSafetyLimits Limits { get; init; }

    public required IAutomationInputStateManager InputState { get; init; }

    public IHumanInputNoiseController? HumanNoise { get; init; }

    public required IAutomationNodeInputModeResolver InputModeResolver { get; init; }

    public required IAutomationEventBus EventBus { get; init; }

    public double DeltaTimeSeconds { get; set; } = 1d / 60d;

    public bool RequestBreakLoop { get; private set; }

    public bool RequestContinueLoop { get; private set; }

    public (IKeyboardEmulator Keyboard, IMouseEmulator Mouse) ResolveInputEmulationPair(string? requestedModeId) =>
        InputModeResolver.Resolve(requestedModeId);

    public void StoreCapture(Guid nodeId, BitmapSource bitmap, int originScreenX, int originScreenY) =>
        _bundles[nodeId] = new ScreenBundle(bitmap, originScreenX, originScreenY);

    public bool TryGetCapture(Guid nodeId, out BitmapSource bitmap, out int originScreenX, out int originScreenY)
    {
        bitmap = default!;
        originScreenX = 0;
        originScreenY = 0;
        if (!_bundles.TryGetValue(nodeId, out var bundle))
            return false;

        bitmap = bundle.Bitmap;
        originScreenX = bundle.OriginScreenX;
        originScreenY = bundle.OriginScreenY;
        return true;
    }

    public void StoreProbeResult(Guid nodeId, AutomationImageProbeResult result) => _probeResults[nodeId] = result;

    public bool TryGetProbeResultByNode(Guid nodeId, out AutomationImageProbeResult result) =>
        _probeResults.TryGetValue(nodeId, out result);

    public void BeginExecutionStep() => _dataEvaluationCache.Clear();

    public bool TryResolveProbeResult(Guid targetNodeId, string targetPortId, out AutomationImageProbeResult result)
    {
        var source = Index.GetDataSourceLink(targetNodeId, targetPortId);
        if (source is not { } sourceLink)
        {
            result = default;
            return false;
        }

        return _probeResults.TryGetValue(sourceLink.SourceNodeId, out result);
    }

    public Guid? GetExecutionTarget(Guid sourceNodeId, string sourcePortId) =>
        Index.GetExecutionTarget(sourceNodeId, sourcePortId);

    public int GetLoopCounter(Guid nodeId) => _loopCounters.GetValueOrDefault(nodeId);

    public void SetLoopCounter(Guid nodeId, int value) => _loopCounters[nodeId] = value;

    public void SetVariable(string name, AutomationDataValue value)
    {
        if (string.IsNullOrWhiteSpace(name))
            return;

        _variables[name.Trim()] = value;
    }

    public AutomationDataValue GetVariable(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return AutomationDataValue.Empty;

        return _variables.GetValueOrDefault(name.Trim(), AutomationDataValue.Empty);
    }

    public void TriggerLoopBreak() => RequestBreakLoop = true;

    public void TriggerLoopContinue() => RequestContinueLoop = true;

    public void ResetLoopControlFlags()
    {
        RequestBreakLoop = false;
        RequestContinueLoop = false;
    }

    public int NextRandomInt(int minInclusive, int maxInclusive)
    {
        if (maxInclusive <= minInclusive)
            return minInclusive;

        return _random.Next(minInclusive, maxInclusive + 1);
    }

    public bool TryResolveNumberInput(Guid targetNodeId, string targetPortId, out double value)
    {
        var resolved = ResolveInputValue(targetNodeId, targetPortId);
        return resolved.TryGetNumber(out value);
    }

    public bool TryResolveBooleanInput(Guid targetNodeId, string targetPortId, out bool value)
    {
        var resolved = ResolveInputValue(targetNodeId, targetPortId);
        return resolved.TryGetBoolean(out value);
    }

    public string ResolveStringInput(Guid targetNodeId, string targetPortId)
    {
        var resolved = ResolveInputValue(targetNodeId, targetPortId);
        return resolved.GetStringOrEmpty();
    }

    private AutomationDataValue ResolveInputValue(Guid targetNodeId, string targetPortId)
    {
        var sourceLink = Index.GetDataSourceLink(targetNodeId, targetPortId);
        if (sourceLink is null)
            return AutomationDataValue.Empty;

        return ResolveDataOutput(sourceLink.Value.SourceNodeId, sourceLink.Value.SourcePortId);
    }

    private AutomationDataValue ResolveDataOutput(Guid sourceNodeId, string sourcePortId)
    {
        var cacheKey = (sourceNodeId, sourcePortId);
        if (_dataEvaluationCache.TryGetValue(cacheKey, out var cached))
            return cached;

        var node = Index.GetNode(sourceNodeId);
        if (node is null)
            return AutomationDataValue.Empty;

        var value = EvaluateNodeDataPort(node, sourcePortId);
        _dataEvaluationCache[cacheKey] = value;
        return value;
    }

    public TState GetOrCreateNodeState<TState>(Guid nodeId, Func<TState> factory)
        where TState : class
    {
        if (_stateByNodeId.TryGetValue(nodeId, out var existing) && existing is TState typed)
            return typed;

        var created = factory();
        _stateByNodeId[nodeId] = created;
        return created;
    }

    private AutomationDataValue EvaluateNodeDataPort(AutomationNodeState node, string sourcePortId) =>
        node.NodeTypeId switch
        {
            "perception.find_image" => EvaluateFindImageOutput(node, sourcePortId),
            "math.add" => EvaluateBinaryMath(node, sourcePortId, (l, r) => l + r),
            "math.subtract" => EvaluateBinaryMath(node, sourcePortId, (l, r) => l - r),
            "math.multiply" => EvaluateBinaryMath(node, sourcePortId, (l, r) => l * r),
            "math.divide" => EvaluateBinaryMath(node, sourcePortId, (l, r) => Math.Abs(r) < double.Epsilon ? 0 : l / r),
            "logic.gt" => EvaluateComparison(node, sourcePortId, (l, r) => l > r),
            "logic.lt" => EvaluateComparison(node, sourcePortId, (l, r) => l < r),
            "logic.eq" => EvaluateComparison(node, sourcePortId, (l, r) => Math.Abs(l - r) < double.Epsilon),
            "logic.and" => EvaluateBoolBinary(node, sourcePortId, (l, r) => l && r),
            "logic.or" => EvaluateBoolBinary(node, sourcePortId, (l, r) => l || r),
            "logic.not" => EvaluateBoolNot(node, sourcePortId),
            "math.random" => EvaluateRandom(node, sourcePortId),
            "variables.get" => EvaluateVariableGet(node, sourcePortId),
            "control.pid_controller" => EvaluatePid(node, sourcePortId),
            "output.key_state" => EvaluateKeyState(node, sourcePortId),
            _ => AutomationDataValue.Empty
        };

    private AutomationDataValue EvaluatePid(AutomationNodeState node, string sourcePortId)
    {
        if (!string.Equals(sourcePortId, "control.signal", StringComparison.Ordinal))
            return AutomationDataValue.Empty;

        if (!TryResolveNumberInput(node.Id, "current.value", out var current))
            current = AutomationNodePropertyReader.ReadDouble(node.Properties, AutomationNodePropertyKeys.PidCurrentValue, 0d);
        if (!TryResolveNumberInput(node.Id, "target.value", out var target))
            target = AutomationNodePropertyReader.ReadDouble(node.Properties, AutomationNodePropertyKeys.PidTargetValue, 0d);

        var kp = AutomationNodePropertyReader.ReadDouble(node.Properties, AutomationNodePropertyKeys.PidKp, 1d);
        var ki = AutomationNodePropertyReader.ReadDouble(node.Properties, AutomationNodePropertyKeys.PidKi, 0d);
        var kd = AutomationNodePropertyReader.ReadDouble(node.Properties, AutomationNodePropertyKeys.PidKd, 0d);

        var state = GetOrCreateNodeState(node.Id, () => new GamepadMapperGUI.Services.Automation.NodeHandlers.PidControllerState());
        var dt = Math.Max(DeltaTimeSeconds, 0.0001d);
        var error = target - current;
        state.IntegralAccumulator += error * dt;
        var derivative = state.Initialized ? (error - state.PreviousError) / dt : 0d;
        state.PreviousError = error;
        state.Initialized = true;

        var output = (kp * error) + (ki * state.IntegralAccumulator) + (kd * derivative);
        return new AutomationDataValue(AutomationPortType.Number, output);
    }

    private AutomationDataValue EvaluateKeyState(AutomationNodeState node, string sourcePortId)
    {
        if (!string.Equals(sourcePortId, "result.pressed", StringComparison.Ordinal))
            return AutomationDataValue.Empty;

        var keyText = AutomationNodePropertyReader.ReadString(node.Properties, AutomationNodePropertyKeys.KeyboardKey);
        var pressed = AutomationKeyboardKeyParser.TryParse(keyText, out var key) && InputState.IsHeld(key);
        return new AutomationDataValue(AutomationPortType.Boolean, pressed);
    }

    private AutomationDataValue EvaluateFindImageOutput(AutomationNodeState node, string sourcePortId)
    {
        if (!_probeResults.TryGetValue(node.Id, out var probe))
            return sourcePortId switch
            {
                "result.found" => new AutomationDataValue(AutomationPortType.Boolean, false),
                "result.x" => new AutomationDataValue(AutomationPortType.Number, 0d),
                "result.y" => new AutomationDataValue(AutomationPortType.Number, 0d),
                "result.count" => new AutomationDataValue(AutomationPortType.Integer, 0),
                _ => AutomationDataValue.Empty
            };

        return sourcePortId switch
        {
            "result.found" => new AutomationDataValue(AutomationPortType.Boolean, probe.Matched),
            "result.x" => new AutomationDataValue(AutomationPortType.Number, (double)probe.MatchScreenXPx),
            "result.y" => new AutomationDataValue(AutomationPortType.Number, (double)probe.MatchScreenYPx),
            "result.count" => new AutomationDataValue(AutomationPortType.Integer, probe.MatchCount),
            _ => AutomationDataValue.Empty
        };
    }

    private AutomationDataValue EvaluateBinaryMath(AutomationNodeState node, string sourcePortId, Func<double, double, double> operation)
    {
        if (!string.Equals(sourcePortId, "value", StringComparison.Ordinal))
            return AutomationDataValue.Empty;

        if (!TryResolveNumberInput(node.Id, "left", out var left))
            left = AutomationNodePropertyReader.ReadDouble(node.Properties, AutomationNodePropertyKeys.MathLeft, 0);
        if (!TryResolveNumberInput(node.Id, "right", out var right))
            right = AutomationNodePropertyReader.ReadDouble(node.Properties, AutomationNodePropertyKeys.MathRight, 0);

        return new AutomationDataValue(AutomationPortType.Number, operation(left, right));
    }

    private AutomationDataValue EvaluateComparison(AutomationNodeState node, string sourcePortId, Func<double, double, bool> operation)
    {
        if (!string.Equals(sourcePortId, "value", StringComparison.Ordinal))
            return AutomationDataValue.Empty;

        if (!TryResolveNumberInput(node.Id, "left", out var left))
            left = AutomationNodePropertyReader.ReadDouble(node.Properties, AutomationNodePropertyKeys.CompareLeft, 0);
        if (!TryResolveNumberInput(node.Id, "right", out var right))
            right = AutomationNodePropertyReader.ReadDouble(node.Properties, AutomationNodePropertyKeys.CompareRight, 0);

        return new AutomationDataValue(AutomationPortType.Boolean, operation(left, right));
    }

    private AutomationDataValue EvaluateBoolBinary(AutomationNodeState node, string sourcePortId, Func<bool, bool, bool> operation)
    {
        if (!string.Equals(sourcePortId, "value", StringComparison.Ordinal))
            return AutomationDataValue.Empty;

        if (!TryResolveBooleanInput(node.Id, "left", out var left))
            left = AutomationNodePropertyReader.ReadBool(node.Properties, AutomationNodePropertyKeys.BoolLeft);
        if (!TryResolveBooleanInput(node.Id, "right", out var right))
            right = AutomationNodePropertyReader.ReadBool(node.Properties, AutomationNodePropertyKeys.BoolRight);

        return new AutomationDataValue(AutomationPortType.Boolean, operation(left, right));
    }

    private AutomationDataValue EvaluateBoolNot(AutomationNodeState node, string sourcePortId)
    {
        if (!string.Equals(sourcePortId, "value", StringComparison.Ordinal))
            return AutomationDataValue.Empty;

        if (!TryResolveBooleanInput(node.Id, "input", out var input))
            input = AutomationNodePropertyReader.ReadBool(node.Properties, AutomationNodePropertyKeys.BoolNotInput);

        return new AutomationDataValue(AutomationPortType.Boolean, !input);
    }

    private AutomationDataValue EvaluateRandom(AutomationNodeState node, string sourcePortId)
    {
        if (!string.Equals(sourcePortId, "value", StringComparison.Ordinal))
            return AutomationDataValue.Empty;

        var minValue = AutomationNodePropertyReader.ReadInt(node.Properties, AutomationNodePropertyKeys.RandomMin, 0);
        var maxValue = AutomationNodePropertyReader.ReadInt(node.Properties, AutomationNodePropertyKeys.RandomMax, 100);
        var value = NextRandomInt(Math.Min(minValue, maxValue), Math.Max(minValue, maxValue));
        return new AutomationDataValue(AutomationPortType.Integer, value);
    }

    private AutomationDataValue EvaluateVariableGet(AutomationNodeState node, string sourcePortId)
    {
        if (!string.Equals(sourcePortId, "value", StringComparison.Ordinal))
            return AutomationDataValue.Empty;

        var name = AutomationNodePropertyReader.ReadString(node.Properties, AutomationNodePropertyKeys.VariableName);
        var value = GetVariable(name);
        if (value.Value is not null)
            return value;

        if (node.Properties?.TryGetPropertyValue(AutomationNodePropertyKeys.VariableDefaultValue, out var fallbackNode) == true &&
            fallbackNode is not null)
        {
            return ParseLiteralValue(fallbackNode);
        }

        return AutomationDataValue.Empty;
    }

    private static AutomationDataValue ParseLiteralValue(JsonNode node)
    {
        if (node is JsonValue valueNode && valueNode.TryGetValue<bool>(out var boolValue))
            return new AutomationDataValue(AutomationPortType.Boolean, boolValue);
        if (node is JsonValue intNode && intNode.TryGetValue<int>(out var intValue))
            return new AutomationDataValue(AutomationPortType.Integer, intValue);
        if (node is JsonValue doubleNode && doubleNode.TryGetValue<double>(out var doubleValue))
            return new AutomationDataValue(AutomationPortType.Number, doubleValue);

        return new AutomationDataValue(AutomationPortType.String, node.ToString());
    }
}
