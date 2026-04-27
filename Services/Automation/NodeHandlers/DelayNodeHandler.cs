#nullable enable

using GamepadMapperGUI.Interfaces.Services.Automation;
using GamepadMapperGUI.Models.Automation;

namespace GamepadMapperGUI.Services.Automation.NodeHandlers;

public sealed class DelayNodeHandler : IAutomationRuntimeNodeHandler
{
    public string NodeTypeId => "automation.delay";

    public Guid? Execute(AutomationRuntimeContext context, AutomationNodeState node, List<string> log, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var fixedDelay = AutomationNodePropertyReader.ReadInt(node.Properties, AutomationNodePropertyKeys.DelayMilliseconds, 300);
        var minDelay = AutomationNodePropertyReader.ReadInt(node.Properties, AutomationNodePropertyKeys.DelayMinMilliseconds, fixedDelay);
        var maxDelay = AutomationNodePropertyReader.ReadInt(node.Properties, AutomationNodePropertyKeys.DelayMaxMilliseconds, fixedDelay);
        var low = Math.Clamp(Math.Min(minDelay, maxDelay), 0, context.Limits.MaxDelayMilliseconds);
        var high = Math.Clamp(Math.Max(minDelay, maxDelay), 0, context.Limits.MaxDelayMilliseconds);
        var delayMs = context.NextRandomInt(low, high);
        log.Add($"delay:ms:{delayMs}");
        if (delayMs > 0)
            Task.Delay(delayMs, cancellationToken).GetAwaiter().GetResult();

        return context.GetExecutionTarget(node.Id, "flow.out");
    }
}
