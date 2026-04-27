namespace GamepadMapperGUI.Models.Automation;

public sealed class AutomationGraphDocument
{
    public int SchemaVersion { get; set; } = 1;

    public List<AutomationNodeState> Nodes { get; set; } = [];

    public List<AutomationEdgeState> Edges { get; set; } = [];

    public List<AutomationSubgraphDefinition> Subgraphs { get; set; } = [];

    public List<AutomationStateMachineDefinition> StateMachines { get; set; } = [];
}
