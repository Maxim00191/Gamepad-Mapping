#nullable enable

using GamepadMapperGUI.Interfaces.Services.Automation;
using GamepadMapperGUI.Models.Automation;
using GamepadMapperGUI.Services.Automation;

namespace GamepadMapperGUI.Services.Automation.NodeHandlers;

public sealed class HumanNoiseNodeHandler(
    IAutomationHumanNoiseTargetResolver? targetResolver = null) : IAutomationRuntimeNodeHandler
{
    private readonly IAutomationHumanNoiseTargetResolver _targetResolver =
        targetResolver ?? new AutomationHumanNoiseTargetResolver();

    public string NodeTypeId => AutomationNodeTypeIds.HumanNoise;

    public Guid? Execute(AutomationRuntimeContext context, AutomationNodeState node, IList<string> log, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var baseDeltaX = AutomationNodePropertyReader.ReadInt(node.Properties, AutomationNodePropertyKeys.MouseJitterBaseDeltaX, 0);
        var baseDeltaY = AutomationNodePropertyReader.ReadInt(node.Properties, AutomationNodePropertyKeys.MouseJitterBaseDeltaY, 0);
        var stickMagnitude = Math.Clamp(
            (float)AutomationNodePropertyReader.ReadDouble(node.Properties, AutomationNodePropertyKeys.MouseJitterStickMagnitude, 1d),
            0f,
            1f);

        var target = _targetResolver.Resolve(context.Index, node);
        if (target.Kind == AutomationHumanNoiseTargetKind.Keyboard ||
            target.Kind == AutomationHumanNoiseTargetKind.Unknown && target.NodeId is not null)
        {
            log.Add($"[human_noise] target={FormatTargetKind(target)} action=no_pointer_move target_node={target.NodeId}");
            return context.GetExecutionTarget(node.Id, AutomationPortIds.FlowOut);
        }

        var adjusted = context.HumanNoise?.AdjustMouseMove(baseDeltaX, baseDeltaY, stickMagnitude) ?? (baseDeltaX, baseDeltaY);
        context.Mouse.MoveBy(adjusted.Dx, adjusted.Dy);
        log.Add(
            $"[human_noise] target={FormatTargetKind(target)} dx={adjusted.Dx} dy={adjusted.Dy} base_dx={baseDeltaX} base_dy={baseDeltaY} stick={stickMagnitude:F2}");
        return context.GetExecutionTarget(node.Id, AutomationPortIds.FlowOut);
    }

    private static string FormatTargetKind(AutomationHumanNoiseTarget target) =>
        target.Kind == AutomationHumanNoiseTargetKind.Unknown && target.NodeId is null
            ? "standalone_mouse"
            : target.Kind.ToString().ToLowerInvariant();
}
