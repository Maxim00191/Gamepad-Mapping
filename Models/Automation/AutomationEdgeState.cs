namespace GamepadMapperGUI.Models.Automation;

public sealed class AutomationEdgeState
{
    public Guid Id { get; set; }

    public Guid SourceNodeId { get; set; }

    public string SourcePortId { get; set; } = "";

    public Guid TargetNodeId { get; set; }

    public string TargetPortId { get; set; } = "";
}
