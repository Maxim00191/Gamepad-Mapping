#nullable enable

using GamepadMapperGUI.Interfaces.Services.Automation;
using GamepadMapperGUI.Models.Automation;

namespace GamepadMapperGUI.Services.Automation;

public sealed class AutomationHumanNoiseTargetResolver : IAutomationHumanNoiseTargetResolver
{
    private const int MaxTraversalDepth = 16;

    private static readonly HashSet<string> TransparentNodeTypeIds =
    [
        AutomationNodeTypeIds.HumanNoise,
        AutomationNodeTypeIds.Delay,
        AutomationNodeTypeIds.DebugLog,
        AutomationNodeTypeIds.VariablesSet
    ];

    public AutomationHumanNoiseTarget Resolve(IAutomationExecutionGraphIndex index, AutomationNodeState node)
    {
        var currentId = index.GetExecutionTarget(node.Id, AutomationPortIds.FlowOut);
        var visited = new HashSet<Guid>();
        for (var depth = 0; currentId is Guid nodeId && depth < MaxTraversalDepth; depth++)
        {
            if (!visited.Add(nodeId))
                return new AutomationHumanNoiseTarget(AutomationHumanNoiseTargetKind.Unknown, nodeId);

            var current = index.GetNode(nodeId);
            if (current is null)
                return new AutomationHumanNoiseTarget(AutomationHumanNoiseTargetKind.Unknown, nodeId);

            if (string.Equals(current.NodeTypeId, AutomationNodeTypeIds.KeyboardKey, StringComparison.Ordinal))
                return new AutomationHumanNoiseTarget(AutomationHumanNoiseTargetKind.Keyboard, current.Id);

            if (string.Equals(current.NodeTypeId, AutomationNodeTypeIds.MouseClick, StringComparison.Ordinal))
                return new AutomationHumanNoiseTarget(AutomationHumanNoiseTargetKind.Mouse, current.Id);

            if (!TransparentNodeTypeIds.Contains(current.NodeTypeId))
                return new AutomationHumanNoiseTarget(AutomationHumanNoiseTargetKind.Unknown, current.Id);

            currentId = index.GetExecutionTarget(current.Id, AutomationPortIds.FlowOut);
        }

        return AutomationHumanNoiseTarget.Unknown;
    }
}
