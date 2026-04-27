namespace GamepadMapperGUI.Models.Automation;

public sealed class AutomationTopologyAnalysis
{
    public bool HasExecutionCycle { get; init; }

    public bool HasDataCycle { get; init; }

    public IReadOnlyList<Guid> CycleEdgeIds { get; init; } = [];

    public IReadOnlyList<Guid> DataCycleEdgeIds { get; init; } = [];

    public string? DetailMessageResourceKey { get; init; }
}
