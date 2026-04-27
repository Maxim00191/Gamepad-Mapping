namespace GamepadMapperGUI.Models.Automation;

public sealed class AutomationExecutionSafetyLimits
{
    public int MaxExecutionSteps { get; init; } = 400;

    public int MaxLoopIterationsPerNode { get; init; } = 1000;

    public int MaxDelayMilliseconds { get; init; } = 120000;
}
