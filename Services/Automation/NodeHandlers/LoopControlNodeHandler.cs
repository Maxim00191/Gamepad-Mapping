#nullable enable

using GamepadMapperGUI.Interfaces.Services.Automation;
using GamepadMapperGUI.Models.Automation;

namespace GamepadMapperGUI.Services.Automation.NodeHandlers;

public sealed class LoopControlNodeHandler : IAutomationRuntimeNodeHandler
{
    public string NodeTypeId => "logic.loop_control";

    public Guid? Execute(AutomationRuntimeContext context, AutomationNodeState node, List<string> log, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var mode = AutomationNodePropertyReader.ReadString(node.Properties, AutomationNodePropertyKeys.LoopControlMode);
        if (string.Equals(mode, "break", StringComparison.OrdinalIgnoreCase))
        {
            context.TriggerLoopBreak();
            log.Add("loop:break");
        }
        else
        {
            context.TriggerLoopContinue();
            log.Add("loop:continue");
        }

        return context.GetExecutionTarget(node.Id, "flow.out");
    }
}
