#nullable enable

namespace GamepadMapperGUI.Models.Automation;

public sealed class AutomationGraphExistingNodeSplicePlan
{
    public required IReadOnlyList<Guid> RemovedEdgeIds { get; init; }

    public required AutomationEdgeState EdgeFromSourceToExisting { get; init; }

    public required AutomationEdgeState EdgeFromExistingToTarget { get; init; }
}
