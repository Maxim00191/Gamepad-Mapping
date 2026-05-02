#nullable enable

using System.Linq;
using GamepadMapperGUI.Interfaces.Services.Automation;
using GamepadMapperGUI.Models.Automation;

namespace GamepadMapperGUI.Services.Automation;

public static class AutomationExecutionFlowDefaults
{
    public static bool TryGetDefaultExecutionFlowConnection(
        INodeTypeRegistry registry,
        string sourceNodeTypeId,
        string targetNodeTypeId,
        out string sourcePortId,
        out string targetPortId)
    {
        sourcePortId = "";
        targetPortId = "";

        if (!registry.TryGet(sourceNodeTypeId, out var srcDef) || srcDef is null)
            return false;
        if (!registry.TryGet(targetNodeTypeId, out var tgtDef) || tgtDef is null)
            return false;

        var outExec = srcDef.OutputPorts.FirstOrDefault(p =>
            p.FlowKind == AutomationPortFlowKind.Execution && p.IsOutput);
        var inExec = tgtDef.InputPorts.FirstOrDefault(p =>
            p.FlowKind == AutomationPortFlowKind.Execution && !p.IsOutput);

        if (outExec is null || inExec is null)
            return false;

        sourcePortId = outExec.Id;
        targetPortId = inExec.Id;
        return true;
    }
}
