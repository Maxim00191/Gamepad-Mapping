#nullable enable

using GamepadMapperGUI.Interfaces.Services.Automation;
using GamepadMapperGUI.Models.Automation;

namespace GamepadMapperGUI.Services.Automation.NodeHandlers;

public sealed class EventEmitNodeHandler : IAutomationRuntimeNodeHandler
{
    public string NodeTypeId => "event.emit";

    public Guid? Execute(AutomationRuntimeContext context, AutomationNodeState node, List<string> log, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var signal = AutomationNodePropertyReader.ReadString(node.Properties, AutomationNodePropertyKeys.EventSignal);
        if (!string.IsNullOrWhiteSpace(signal))
        {
            context.EventBus.Publish(signal);
            log.Add($"[event] emitted signal={signal.Trim()}");
        }

        return context.GetExecutionTarget(node.Id, "flow.out");
    }
}
