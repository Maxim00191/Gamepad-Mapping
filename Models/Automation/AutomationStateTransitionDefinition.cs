namespace GamepadMapperGUI.Models.Automation;

public sealed class AutomationStateTransitionDefinition
{
    public string EventSignal { get; set; } = "";

    public string TargetStateId { get; set; } = "";
}
