#nullable enable

namespace GamepadMapperGUI.Models.Automation;

public sealed class AutomationGraphLinearBridgePlan
{
    public required Guid SourceNodeId { get; init; }

    public required string SourcePortId { get; init; }

    public required Guid TargetNodeId { get; init; }

    public required string TargetPortId { get; init; }

    public required Guid[] RemovedEdgeIds { get; init; }
}
