#nullable enable

using GamepadMapperGUI.Interfaces.Services.Automation;
using GamepadMapperGUI.Models.Automation;

namespace GamepadMapperGUI.Services.Automation;

public sealed class AutomationExecutionSafetyPolicy : IAutomationExecutionSafetyPolicy
{
    private const int BaseStepBudget = 400;
    private const int StepBudgetPerNode = 48;
    private const int StepBudgetPerEdge = 24;
    private const int MaxGlobalStepBudget = 10000;

    private const int BaseLoopLimit = 1000;
    private const int MaxLoopLimit = 5000;

    private const int BaseMaxDelayMs = 120000;
    private const int MaxDelayCeilingMs = 300000;

    public AutomationExecutionSafetyLimits GetLimits(AutomationGraphDocument document)
    {
        var nodeCount = Math.Max(0, document.Nodes.Count);
        var edgeCount = Math.Max(0, document.Edges.Count);
        var computedStepBudget = BaseStepBudget + (nodeCount * StepBudgetPerNode) + (edgeCount * StepBudgetPerEdge);

        return new AutomationExecutionSafetyLimits
        {
            MaxExecutionSteps = Math.Clamp(computedStepBudget, BaseStepBudget, MaxGlobalStepBudget),
            MaxLoopIterationsPerNode = Math.Clamp(BaseLoopLimit + (nodeCount * 16), BaseLoopLimit, MaxLoopLimit),
            MaxDelayMilliseconds = Math.Clamp(BaseMaxDelayMs + (edgeCount * 400), BaseMaxDelayMs, MaxDelayCeilingMs)
        };
    }
}
