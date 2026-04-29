#nullable enable

using GamepadMapperGUI.Interfaces.Services.Automation;
using GamepadMapperGUI.Models.Automation;

namespace GamepadMapperGUI.Services.Automation.NodeHandlers;

public sealed class LoopNodeHandler : IAutomationRuntimeNodeHandler
{
    public string NodeTypeId => "automation.loop";

    public Guid? Execute(AutomationRuntimeContext context, AutomationNodeState node, IList<string> log, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (context.RequestBreakLoop)
        {
            context.ResetLoopControlFlags();
            context.SetLoopCounter(node.Id, 0);
            return context.GetExecutionTarget(node.Id, "flow.out");
        }

        var maxIterations = Math.Clamp(
            AutomationNodePropertyReader.ReadInt(node.Properties, AutomationNodePropertyKeys.LoopMaxIterations, 1),
            1,
            context.Limits.MaxLoopIterationsPerNode);
        var loopCount = context.GetLoopCounter(node.Id);
        if (loopCount >= maxIterations)
        {
            log.Add($"loop:done:{loopCount}");
            context.SetLoopCounter(node.Id, 0);
            return context.GetExecutionTarget(node.Id, "flow.out");
        }

        context.SetLoopCounter(node.Id, loopCount + 1);
        if (context.RequestContinueLoop)
            context.ResetLoopControlFlags();

        log.Add($"loop:iter:{loopCount + 1}/{maxIterations}");
        return context.GetExecutionTarget(node.Id, "loop.body");
    }
}
