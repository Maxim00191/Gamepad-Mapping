#nullable enable

using GamepadMapperGUI.Interfaces.Services.Automation;
using GamepadMapperGUI.Models.Automation;

namespace GamepadMapperGUI.Services.Automation.NodeHandlers;

public sealed class LoopJumpNodeHandler : IAutomationRuntimeNodeHandler
{
    public string NodeTypeId => AutomationNodeTypeIds.LoopJump;

    public Guid? Execute(AutomationRuntimeContext context, AutomationNodeState node, IList<string> log, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var label = AutomationNodePropertyReader.ReadString(node.Properties, AutomationNodePropertyKeys.LoopJumpTargetScopeLabel);
        if (string.IsNullOrWhiteSpace(label))
            throw new InvalidOperationException("loop_jump:target_missing");

        if (!context.Index.TryGetLoopStartNodeIdByScopeLabel(label, out var loopNodeId))
            throw new InvalidOperationException("loop_jump:target_not_found");

        context.PrepareExecutionJumpToLoopHead(loopNodeId);
        if (context.VerboseExecutionLog)
            log.Add($"loop_jump:{label.Trim()}->{AutomationLogFormatter.NodeId(loopNodeId)}");

        return loopNodeId;
    }
}
