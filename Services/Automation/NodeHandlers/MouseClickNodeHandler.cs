#nullable enable

using GamepadMapperGUI.Interfaces.Services.Automation;
using GamepadMapperGUI.Models.Automation;

namespace GamepadMapperGUI.Services.Automation.NodeHandlers;

public sealed class MouseClickNodeHandler : IAutomationRuntimeNodeHandler
{
    public string NodeTypeId => "output.mouse_click";

    public Guid? Execute(AutomationRuntimeContext context, AutomationNodeState node, List<string> log, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var useMatchPosition = AutomationNodePropertyReader.ReadBool(node.Properties, AutomationNodePropertyKeys.MouseUseMatchPosition);
        var coordinateMode = AutomationNodePropertyReader.ReadString(node.Properties, AutomationNodePropertyKeys.MouseCoordinateMode);
        var humanizeRadius = Math.Clamp(
            AutomationNodePropertyReader.ReadInt(node.Properties, AutomationNodePropertyKeys.MouseHumanizeRadiusPx, 0),
            0,
            25);

        var targetX = 0;
        var targetY = 0;
        var hasTargetPosition = false;
        if (useMatchPosition && context.TryResolveProbeResult(node.Id, "probe.image", out var probeResult) && probeResult.Matched)
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
            context.Mouse.MoveBy(dx, dy);
        }
        else if (hasTargetPosition)
        {
            log.Add("mouse:no_virtual_driver");
        }

        ExecuteButtonAction(context, node, cancellationToken);
        return context.GetExecutionTarget(node.Id, "flow.out");
    }

    private static void ExecuteButtonAction(AutomationRuntimeContext context, AutomationNodeState node, CancellationToken cancellationToken)
    {
        var mode = AutomationNodePropertyReader.ReadString(node.Properties, AutomationNodePropertyKeys.MouseActionMode);
        var button = AutomationNodePropertyReader.ReadString(node.Properties, AutomationNodePropertyKeys.MouseButton);
        if (string.IsNullOrWhiteSpace(button))
            button = "left";

        if (string.Equals(mode, "press", StringComparison.OrdinalIgnoreCase))
        {
            MouseDown(context, button);
            return;
        }

        if (string.Equals(mode, "release", StringComparison.OrdinalIgnoreCase))
        {
            MouseUp(context, button);
            return;
        }

        if (string.Equals(mode, "hold", StringComparison.OrdinalIgnoreCase))
        {
            var holdMs = Math.Clamp(
                AutomationNodePropertyReader.ReadInt(node.Properties, AutomationNodePropertyKeys.KeyboardHoldMilliseconds, 120),
                1,
                context.Limits.MaxDelayMilliseconds);
            MouseDown(context, button);
            Task.Delay(holdMs, cancellationToken).GetAwaiter().GetResult();
            MouseUp(context, button);
            return;
        }

        MouseClick(context, button);
    }

    private static void MouseClick(AutomationRuntimeContext context, string button)
    {
        if (string.Equals(button, "right", StringComparison.OrdinalIgnoreCase))
            context.Mouse.RightClick();
        else if (string.Equals(button, "middle", StringComparison.OrdinalIgnoreCase))
            context.Mouse.MiddleClick();
        else
            context.Mouse.LeftClick();
    }

    private static void MouseDown(AutomationRuntimeContext context, string button)
    {
        if (string.Equals(button, "right", StringComparison.OrdinalIgnoreCase))
            context.Mouse.RightDown();
        else if (string.Equals(button, "middle", StringComparison.OrdinalIgnoreCase))
            context.Mouse.MiddleDown();
        else
            context.Mouse.LeftDown();
    }

    private static void MouseUp(AutomationRuntimeContext context, string button)
    {
        if (string.Equals(button, "right", StringComparison.OrdinalIgnoreCase))
            context.Mouse.RightUp();
        else if (string.Equals(button, "middle", StringComparison.OrdinalIgnoreCase))
            context.Mouse.MiddleUp();
        else
            context.Mouse.LeftUp();
    }
}
