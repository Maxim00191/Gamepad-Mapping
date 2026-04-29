#nullable enable

using GamepadMapperGUI.Interfaces.Services.Automation;
using GamepadMapperGUI.Interfaces.Services.Input;
using GamepadMapperGUI.Models.Automation;

namespace GamepadMapperGUI.Services.Automation.NodeHandlers;

public sealed class MouseClickNodeHandler : IAutomationRuntimeNodeHandler
{
    private sealed class MouseButtonHoldState
    {
        public string Button { get; set; } = "left";
        public bool IsHeld { get; set; }
    }

    public string NodeTypeId => "output.mouse_click";

    public Guid? Execute(AutomationRuntimeContext context, AutomationNodeState node, IList<string> log, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!AutomationOutputDispatchGuard.CanDispatch(context, "mouse_click", log))
            return context.GetExecutionTarget(node.Id, "flow.out");

        var requestedModeId = AutomationNodePropertyReader.ReadString(node.Properties, AutomationNodePropertyKeys.InputEmulationApiId);
        var (_, mouse) = context.ResolveInputEmulationPair(requestedModeId);
        var useMatchPosition = AutomationNodePropertyReader.ReadBool(node.Properties, AutomationNodePropertyKeys.MouseUseMatchPosition);
        var coordinateMode = AutomationNodePropertyReader.ReadString(node.Properties, AutomationNodePropertyKeys.MouseCoordinateMode);
        var humanizeRadius = Math.Clamp(
            AutomationNodePropertyReader.ReadInt(node.Properties, AutomationNodePropertyKeys.MouseHumanizeRadiusPx, 0),
            0,
            25);

        var targetX = 0;
        var targetY = 0;
        var hasTargetPosition = false;
        if (useMatchPosition && context.TryResolveProbeResult(node.Id, AutomationPortIds.ProbeImage, out var probeResult) && probeResult.Matched)
        {
            targetX = probeResult.MatchScreenXPx;
            targetY = probeResult.MatchScreenYPx;
            hasTargetPosition = true;
        }

        if (!hasTargetPosition && string.Equals(coordinateMode, "absolute", StringComparison.OrdinalIgnoreCase))
        {
            targetX = AutomationNodePropertyReader.ReadInt(node.Properties, AutomationNodePropertyKeys.MouseAbsoluteX, 0);
            targetY = AutomationNodePropertyReader.ReadInt(node.Properties, AutomationNodePropertyKeys.MouseAbsoluteY, 0);
            hasTargetPosition = true;
        }

        if (!hasTargetPosition && string.Equals(coordinateMode, "dynamic", StringComparison.OrdinalIgnoreCase))
        {
            if (context.TryResolveNumberInput(node.Id, "coord.x", out var dynamicX) &&
                context.TryResolveNumberInput(node.Id, "coord.y", out var dynamicY))
            {
                targetX = (int)Math.Round(dynamicX);
                targetY = (int)Math.Round(dynamicY);
                hasTargetPosition = true;
            }
        }

        if (hasTargetPosition && humanizeRadius > 0)
        {
            targetX += context.NextRandomInt(-humanizeRadius, humanizeRadius);
            targetY += context.NextRandomInt(-humanizeRadius, humanizeRadius);
        }

        if (hasTargetPosition && context.VirtualMouse is not null)
        {
            context.VirtualMouse.MoveCursorToVirtualScreenPixels(targetX, targetY);
        }
        else if (string.Equals(coordinateMode, "relative", StringComparison.OrdinalIgnoreCase))
        {
            var dx = AutomationNodePropertyReader.ReadInt(node.Properties, AutomationNodePropertyKeys.MouseRelativeDeltaX, 0);
            var dy = AutomationNodePropertyReader.ReadInt(node.Properties, AutomationNodePropertyKeys.MouseRelativeDeltaY, 0);
            mouse.MoveBy(dx, dy);
        }
        else if (hasTargetPosition)
        {
            log.Add("mouse:no_virtual_driver");
        }

        ExecuteButtonAction(context, node, mouse, hasTargetPosition, targetX, targetY, log, cancellationToken);
        return context.GetExecutionTarget(node.Id, "flow.out");
    }

    private static void ExecuteButtonAction(
        AutomationRuntimeContext context,
        AutomationNodeState node,
        IMouseEmulator mouse,
        bool hasTargetPosition,
        int targetX,
        int targetY,
        IList<string> log,
        CancellationToken cancellationToken)
    {
        var mode = NormalizeMode(
            AutomationNodePropertyReader.ReadString(node.Properties, AutomationNodePropertyKeys.MouseActionMode),
            AutomationOutputActionModes.Click);
        var button = AutomationNodePropertyReader.ReadString(node.Properties, AutomationNodePropertyKeys.MouseButton);
        if (string.IsNullOrWhiteSpace(button))
            button = "left";
        button = button.Trim().ToLowerInvariant();

        if (string.Equals(mode, AutomationOutputActionModes.Tap, StringComparison.Ordinal))
            mode = AutomationOutputActionModes.Click;

        if (string.Equals(mode, AutomationOutputActionModes.HoldWhileTrue, StringComparison.Ordinal))
        {
            var condition = ResolveHoldCondition(context, node);
            ApplyMouseHoldTransition(context, node, mouse, button, condition);
            return;
        }

        if (string.Equals(mode, AutomationOutputActionModes.Press, StringComparison.Ordinal))
        {
            MouseDown(mouse, button);
            return;
        }

        if (string.Equals(mode, AutomationOutputActionModes.Release, StringComparison.Ordinal))
        {
            MouseUp(mouse, button);
            return;
        }

        if (string.Equals(mode, AutomationOutputActionModes.Hold, StringComparison.Ordinal))
        {
            var holdMs = Math.Clamp(
                AutomationNodePropertyReader.ReadInt(node.Properties, AutomationNodePropertyKeys.KeyboardHoldMilliseconds, 120),
                1,
                context.Limits.MaxDelayMilliseconds);
            MouseDown(mouse, button);
            Task.Delay(holdMs, cancellationToken).GetAwaiter().GetResult();
            MouseUp(mouse, button);
            return;
        }

        MouseClick(mouse, button);
    }

    private static void MouseClick(IMouseEmulator mouse, string button)
    {
        if (string.Equals(button, "right", StringComparison.OrdinalIgnoreCase))
            mouse.RightClick();
        else if (string.Equals(button, "middle", StringComparison.OrdinalIgnoreCase))
            mouse.MiddleClick();
        else
            mouse.LeftClick();
    }

    private static void MouseDown(IMouseEmulator mouse, string button)
    {
        if (string.Equals(button, "right", StringComparison.OrdinalIgnoreCase))
            mouse.RightDown();
        else if (string.Equals(button, "middle", StringComparison.OrdinalIgnoreCase))
            mouse.MiddleDown();
        else
            mouse.LeftDown();
    }

    private static void MouseUp(IMouseEmulator mouse, string button)
    {
        if (string.Equals(button, "right", StringComparison.OrdinalIgnoreCase))
            mouse.RightUp();
        else if (string.Equals(button, "middle", StringComparison.OrdinalIgnoreCase))
            mouse.MiddleUp();
        else
            mouse.LeftUp();
    }

    private static void ApplyMouseHoldTransition(
        AutomationRuntimeContext context,
        AutomationNodeState node,
        IMouseEmulator mouse,
        string button,
        bool condition)
    {
        var state = context.GetOrCreateNodeState(node.Id, static () => new MouseButtonHoldState());
        if (!condition)
        {
            if (state.IsHeld)
            {
                MouseUp(mouse, state.Button);
                state.IsHeld = false;
            }
            return;
        }

        if (state.IsHeld && string.Equals(state.Button, button, StringComparison.Ordinal))
            return;

        if (state.IsHeld)
            MouseUp(mouse, state.Button);

        MouseDown(mouse, button);
        state.Button = button;
        state.IsHeld = true;
    }

    private static bool ResolveHoldCondition(AutomationRuntimeContext context, AutomationNodeState node)
    {
        if (context.TryResolveBooleanInput(node.Id, AutomationPortIds.Condition, out var signal))
            return signal;

        return AutomationNodePropertyReader.ReadBool(node.Properties, AutomationNodePropertyKeys.OutputHoldCondition);
    }

    private static string NormalizeMode(string? mode, string fallback)
    {
        if (string.IsNullOrWhiteSpace(mode))
            return fallback;

        var normalized = mode.Trim().ToLowerInvariant();
        return string.IsNullOrWhiteSpace(normalized) ? fallback : normalized;
    }
}
