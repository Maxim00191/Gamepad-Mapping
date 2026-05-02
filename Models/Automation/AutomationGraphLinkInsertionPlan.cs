#nullable enable

namespace GamepadMapperGUI.Models.Automation;

public sealed class AutomationGraphLinkInsertionPlan
{
    public required Guid RemovedEdgeId { get; init; }

    public required AutomationNodeState NewNode { get; init; }

    public required AutomationEdgeState EdgeFromSourceToNew { get; init; }

    public required AutomationEdgeState EdgeFromNewToTarget { get; init; }
}
