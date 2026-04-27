#nullable enable

namespace GamepadMapperGUI.Models.Automation;

public sealed class AutomationConnectionDragState
{
    public required Guid StartNodeId { get; init; }

    public required string StartPortId { get; init; }

    public required bool StartPortIsOutput { get; init; }

    public Guid? HoverNodeId { get; set; }

    public string? HoverPortId { get; set; }

    public bool? HoverPortIsOutput { get; set; }

    public bool HoverValidationAllowed { get; set; }

    public string? HoverValidationReasonResourceKey { get; set; }
}
