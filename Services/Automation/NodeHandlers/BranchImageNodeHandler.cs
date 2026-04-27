#nullable enable

using GamepadMapperGUI.Interfaces.Services.Automation;
using GamepadMapperGUI.Models.Automation;

namespace GamepadMapperGUI.Services.Automation.NodeHandlers;

public sealed class BranchImageNodeHandler : IAutomationRuntimeNodeHandler
{
    public string NodeTypeId => "logic.branch_image";

    public Guid? Execute(AutomationRuntimeContext context, AutomationNodeState node, List<string> log, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!context.TryResolveProbeResult(node.Id, "probe.image", out var probeResult))
        {
            log.Add("branch:no_probe");
            return context.GetExecutionTarget(node.Id, "branch.miss");
        }

        var portId = probeResult.Matched ? "branch.match" : "branch.miss";
        return context.GetExecutionTarget(node.Id, portId);
    }
}
