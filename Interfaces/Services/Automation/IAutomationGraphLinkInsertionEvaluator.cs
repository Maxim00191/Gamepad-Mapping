#nullable enable

using GamepadMapperGUI.Models.Automation;

namespace GamepadMapperGUI.Interfaces.Services.Automation;

public interface IAutomationGraphLinkInsertionEvaluator
{
    bool TryBuildPlan(
        AutomationGraphDocument document,
        Guid edgeId,
        string newNodeTypeId,
        double newNodeX,
        double newNodeY,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out AutomationGraphLinkInsertionPlan? plan);

    bool TryBuildExistingNodeSplicePlan(
        AutomationGraphDocument document,
        Guid edgeId,
        Guid existingNodeId,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out AutomationGraphExistingNodeSplicePlan? plan);
}
