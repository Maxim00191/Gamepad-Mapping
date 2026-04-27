#nullable enable

using GamepadMapperGUI.Interfaces.Services.Automation;
using GamepadMapperGUI.Models.Automation;

namespace GamepadMapperGUI.Services.Automation.NodeHandlers;

public sealed class SwitchNodeHandler : IAutomationRuntimeNodeHandler
{
    public string NodeTypeId => "logic.switch";

    public Guid? Execute(AutomationRuntimeContext context, AutomationNodeState node, List<string> log, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var selectedCase = AutomationNodePropertyReader.ReadString(node.Properties, AutomationNodePropertyKeys.SwitchCaseValue);
        var inputValue = context.ResolveStringInput(node.Id, "value");
        if (!string.IsNullOrWhiteSpace(selectedCase) &&
            string.Equals(selectedCase, inputValue, StringComparison.OrdinalIgnoreCase))
        {
            return context.GetExecutionTarget(node.Id, "case.match");
        }

        return context.GetExecutionTarget(node.Id, "case.default");
    }
}
