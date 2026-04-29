#nullable enable

using GamepadMapperGUI.Interfaces.Services.Automation;
using GamepadMapperGUI.Models.Automation;

namespace GamepadMapperGUI.Services.Automation.NodeHandlers;

public sealed class BranchCompareNodeHandler : IAutomationRuntimeNodeHandler
{
    public string NodeTypeId => AutomationNodeTypeIds.BranchCompare;

    public Guid? Execute(AutomationRuntimeContext context, AutomationNodeState node, IList<string> log, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var operatorId = AutomationNodePropertyReader.ReadString(node.Properties, AutomationNodePropertyKeys.CompareOperator);
        var decision = context.ResolveComparisonCondition(node, operatorId);
        var portId = decision ? AutomationPortIds.BranchTrue : AutomationPortIds.BranchFalse;
        if (context.VerboseExecutionLog)
            log.Add($"[branch_compare] node={AutomationLogFormatter.NodeId(node.Id)} decision={decision} => {portId}");
        return context.GetExecutionTarget(node.Id, portId);
    }
}
