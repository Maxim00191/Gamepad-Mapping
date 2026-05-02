#nullable enable

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using GamepadMapperGUI.Interfaces.Services.Automation;
using GamepadMapperGUI.Models.Automation;

namespace GamepadMapperGUI.Services.Automation;

public sealed class AutomationGraphLinearBridgeEvaluator(IAutomationTopologyAnalyzer topology)
    : IAutomationGraphLinearBridgeEvaluator
{
    private readonly IAutomationTopologyAnalyzer _topology = topology;

    public bool TryBuildBridgeAcrossNode(
        AutomationGraphDocument document,
        Guid nodeId,
        [NotNullWhen(true)] out AutomationGraphLinearBridgePlan? plan)
    {
        plan = null;
        var incoming = document.Edges.Where(e => e.TargetNodeId == nodeId).ToList();
        var outgoing = document.Edges.Where(e => e.SourceNodeId == nodeId).ToList();
        if (incoming.Count != 1 || outgoing.Count != 1)
            return false;

        var ein = incoming[0];
        var eout = outgoing[0];
        if (ein.SourceNodeId == nodeId || eout.TargetNodeId == nodeId)
            return false;

        var ignored = new HashSet<Guid> { ein.Id, eout.Id };
        var v = _topology.ValidateConnection(
            document,
            ein.SourceNodeId,
            ein.SourcePortId,
            eout.TargetNodeId,
            eout.TargetPortId,
            ignored);
        if (!v.IsAllowed)
            return false;

        plan = new AutomationGraphLinearBridgePlan
        {
            SourceNodeId = ein.SourceNodeId,
            SourcePortId = ein.SourcePortId,
            TargetNodeId = eout.TargetNodeId,
            TargetPortId = eout.TargetPortId,
            RemovedEdgeIds = [ein.Id, eout.Id]
        };
        return true;
    }
}
