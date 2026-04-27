namespace GamepadMapperGUI.Models.Automation;

public sealed class AutomationPortDescriptor
{
    public required string Id { get; init; }

    public AutomationPortType PortType { get; init; }

    public AutomationPortFlowKind FlowKind { get; init; }

    public bool IsOutput { get; init; }
}
