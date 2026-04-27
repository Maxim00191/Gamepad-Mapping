namespace GamepadMapperGUI.Models.Automation;

public sealed class AutomationStateDefinition
{
    public string Id { get; set; } = "";

    public string DisplayName { get; set; } = "";

    public string SubgraphId { get; set; } = "";

    public List<AutomationStateTransitionDefinition> Transitions { get; set; } = [];
}
