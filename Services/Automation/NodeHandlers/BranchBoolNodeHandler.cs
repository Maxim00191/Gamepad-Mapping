#nullable enable

using GamepadMapperGUI.Interfaces.Services.Automation;
using GamepadMapperGUI.Models.Automation;

namespace GamepadMapperGUI.Services.Automation.NodeHandlers;

public sealed class BranchBoolNodeHandler : IAutomationRuntimeNodeHandler
{
    public string NodeTypeId => "logic.branch_bool";

    public Guid? Execute(AutomationRuntimeContext context, AutomationNodeState node, List<string> log, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var decision = context.TryResolveBooleanInput(node.Id, "condition", out var condition) && condition;
        return context.GetExecutionTarget(node.Id, decision ? "branch.true" : "branch.false");
    }
}
