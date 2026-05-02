namespace GamepadMapperGUI.Models.Automation;

public sealed class AutomationStateMachineDefinition
{
    public string Id { get; set; } = "";

    public string DisplayName { get; set; } = "";

    public string InitialStateId { get; set; } = "";

    public List<AutomationStateDefinition> States { get; set; } = [];
}
