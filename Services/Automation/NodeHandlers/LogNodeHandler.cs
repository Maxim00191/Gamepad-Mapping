#nullable enable

using GamepadMapperGUI.Interfaces.Services.Automation;
using GamepadMapperGUI.Models.Automation;

namespace GamepadMapperGUI.Services.Automation.NodeHandlers;

public sealed class LogNodeHandler : IAutomationRuntimeNodeHandler
{
    public string NodeTypeId => "debug.log";

    public Guid? Execute(AutomationRuntimeContext context, AutomationNodeState node, List<string> log, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var template = AutomationNodePropertyReader.ReadString(node.Properties, AutomationNodePropertyKeys.LogMessage);
        var value = context.ResolveStringInput(node.Id, "message");
        if (string.IsNullOrWhiteSpace(value))
            value = template;
        log.Add($"log:{value}");
        return context.GetExecutionTarget(node.Id, "flow.out");
    }
}
