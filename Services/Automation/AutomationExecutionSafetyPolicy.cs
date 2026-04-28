#nullable enable

using GamepadMapperGUI.Interfaces.Services.Automation;
using GamepadMapperGUI.Models.Automation;

namespace GamepadMapperGUI.Services.Automation;

public sealed class AutomationExecutionSafetyPolicy : IAutomationExecutionSafetyPolicy
{
    private const int BaseStepBudget = 400;
    private const int StepBudgetPerNode = 48;
    private const int StepBudgetPerEdge = 24;
    private const int MaxGlobalStepBudget = 250000;

    private const int BaseLoopLimit = 1000;
    private const int MaxLoopLimit = 5000;

    private const int BaseMaxDelayMs = 120000;
    private const int MaxDelayCeilingMs = 300000;

    public AutomationExecutionSafetyLimits GetLimits(AutomationGraphDocument document)
    {
        var nodeCount = Math.Max(0, document.Nodes.Count);
        var edgeCount = Math.Max(0, document.Edges.Count);
        var explicitLoopLimit = GetMaxRequestedLoopIterations(document);
        var loopLimit = Math.Clamp(
            Math.Max(BaseLoopLimit + (nodeCount * 16), explicitLoopLimit),
            BaseLoopLimit,
            MaxLoopLimit);
        var computedStepBudget =
            BaseStepBudget +
            (nodeCount * StepBudgetPerNode) +
            (edgeCount * StepBudgetPerEdge) +
            (explicitLoopLimit * Math.Max(1, nodeCount));

        return new AutomationExecutionSafetyLimits
        {
            MaxExecutionSteps = Math.Clamp(computedStepBudget, BaseStepBudget, MaxGlobalStepBudget),
            MaxLoopIterationsPerNode = loopLimit,
            MaxDelayMilliseconds = Math.Clamp(BaseMaxDelayMs + (edgeCount * 400), BaseMaxDelayMs, MaxDelayCeilingMs)
        };
    }

    private static int GetMaxRequestedLoopIterations(AutomationGraphDocument document)
    {
        var max = 0;
        foreach (var node in document.Nodes)
        {
            if (!string.Equals(node.NodeTypeId, "automation.loop", StringComparison.Ordinal))
                continue;

            var requested = AutomationNodePropertyReader.ReadInt(
                node.Properties,
                AutomationNodePropertyKeys.LoopMaxIterations,
                0);
            max = Math.Max(max, requested);
        }

        return Math.Clamp(max, 0, MaxLoopLimit);
    }
}
