namespace GamepadMapperGUI.Models.Automation;

public sealed class AutomationSubgraphDefinition
{
    public string Id { get; set; } = "";

    public string DisplayName { get; set; } = "";

    public AutomationGraphDocument Graph { get; set; } = new();
}
