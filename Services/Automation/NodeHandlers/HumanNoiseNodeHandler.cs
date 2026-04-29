#nullable enable

using GamepadMapperGUI.Interfaces.Services.Automation;
using GamepadMapperGUI.Models.Automation;

namespace GamepadMapperGUI.Services.Automation.NodeHandlers;

public sealed class HumanNoiseNodeHandler : IAutomationRuntimeNodeHandler
{
    public string NodeTypeId => "output.human_noise";

    public Guid? Execute(AutomationRuntimeContext context, AutomationNodeState node, IList<string> log, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var baseDeltaX = AutomationNodePropertyReader.ReadInt(node.Properties, AutomationNodePropertyKeys.MouseJitterBaseDeltaX, 0);
        var baseDeltaY = AutomationNodePropertyReader.ReadInt(node.Properties, AutomationNodePropertyKeys.MouseJitterBaseDeltaY, 0);
        var stickMagnitude = Math.Clamp(
            (float)AutomationNodePropertyReader.ReadDouble(node.Properties, AutomationNodePropertyKeys.MouseJitterStickMagnitude, 1d),
            0f,
            1f);

        var adjusted = context.HumanNoise?.AdjustMouseMove(baseDeltaX, baseDeltaY, stickMagnitude) ?? (baseDeltaX, baseDeltaY);
        context.Mouse.MoveBy(adjusted.Dx, adjusted.Dy);
        log.Add($"[human_noise] dx={adjusted.Dx} dy={adjusted.Dy} base_dx={baseDeltaX} base_dy={baseDeltaY} stick={stickMagnitude:F2}");
        return context.GetExecutionTarget(node.Id, "flow.out");
    }
}
