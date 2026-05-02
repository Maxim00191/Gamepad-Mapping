#nullable enable

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using GamepadMapperGUI.Interfaces.Services.Automation;
using GamepadMapperGUI.Models.Automation;

namespace GamepadMapperGUI.Services.Automation;

public sealed class AutomationGraphLinkInsertionEvaluator(
    INodeTypeRegistry registry,
    IAutomationTopologyAnalyzer topology,
    IAutomationGraphSerializer serializer) : IAutomationGraphLinkInsertionEvaluator
{
    private readonly INodeTypeRegistry _registry = registry;
    private readonly IAutomationTopologyAnalyzer _topology = topology;
    private readonly IAutomationGraphSerializer _serializer = serializer;

    public bool TryBuildPlan(
        AutomationGraphDocument document,
        Guid edgeId,
        string newNodeTypeId,
        double newNodeX,
        double newNodeY,
        [NotNullWhen(true)] out AutomationGraphLinkInsertionPlan? plan)
    {
        plan = null;
        if (!_registry.TryGet(newNodeTypeId, out var newDef) || newDef is null)
            return false;

        var edge = document.Edges.FirstOrDefault(e => e.Id == edgeId);
        if (edge is null)
            return false;

        var sourceNode = document.Nodes.FirstOrDefault(n => n.Id == edge.SourceNodeId);
        var targetNode = document.Nodes.FirstOrDefault(n => n.Id == edge.TargetNodeId);
        if (sourceNode is null || targetNode is null)
            return false;

        var sourceOut = _registry.ResolveOutputPort(sourceNode.NodeTypeId, edge.SourcePortId);
        var targetIn = _registry.ResolveInputPort(targetNode.NodeTypeId, edge.TargetPortId);
        if (sourceOut is null || targetIn is null)
            return false;

        var newNodeId = Guid.NewGuid();
        var draftNewNode = new AutomationNodeState
        {
            Id = newNodeId,
            NodeTypeId = newNodeTypeId,
            X = newNodeX,
            Y = newNodeY,
            Properties = new System.Text.Json.Nodes.JsonObject()
        };

        foreach (var inPort in newDef.InputPorts)
        {
            foreach (var outPort in newDef.OutputPorts)
            {
                if (!PortsQuickCompatible(sourceOut, inPort) || !PortsQuickCompatible(outPort, targetIn))
                    continue;

                var temp = _serializer.Deserialize(_serializer.Serialize(document));
                temp.Edges.RemoveAll(e => e.Id == edgeId);
                temp.Nodes.Add(CloneNodeForSimulation(draftNewNode));

                var v1 = _topology.ValidateConnection(temp, edge.SourceNodeId, edge.SourcePortId, newNodeId, inPort.Id);
                if (!v1.IsAllowed)
                    continue;

                var v2 = _topology.ValidateConnection(temp, newNodeId, outPort.Id, edge.TargetNodeId, edge.TargetPortId);
                if (!v2.IsAllowed)
                    continue;

                plan = new AutomationGraphLinkInsertionPlan
                {
                    RemovedEdgeId = edgeId,
                    NewNode = CloneNodeForSimulation(draftNewNode),
                    EdgeFromSourceToNew = new AutomationEdgeState
                    {
                        Id = Guid.NewGuid(),
                        SourceNodeId = edge.SourceNodeId,
                        SourcePortId = edge.SourcePortId,
                        TargetNodeId = newNodeId,
                        TargetPortId = inPort.Id
                    },
                    EdgeFromNewToTarget = new AutomationEdgeState
                    {
                        Id = Guid.NewGuid(),
                        SourceNodeId = newNodeId,
                        SourcePortId = outPort.Id,
                        TargetNodeId = edge.TargetNodeId,
                        TargetPortId = edge.TargetPortId
                    }
                };
                return true;
            }
        }

        return false;
    }

    public bool TryBuildExistingNodeSplicePlan(
        AutomationGraphDocument document,
        Guid edgeId,
        Guid existingNodeId,
        [NotNullWhen(true)] out AutomationGraphExistingNodeSplicePlan? plan)
    {
        plan = null;
        var edge = document.Edges.FirstOrDefault(e => e.Id == edgeId);
        if (edge is null)
            return false;
        if (edge.SourceNodeId == existingNodeId || edge.TargetNodeId == existingNodeId)
            return false;

        var existingNode = document.Nodes.FirstOrDefault(n => n.Id == existingNodeId);
        if (existingNode is null)
            return false;

        if (!_registry.TryGet(existingNode.NodeTypeId, out var existingDef) || existingDef is null)
            return false;

        var sourceNode = document.Nodes.FirstOrDefault(n => n.Id == edge.SourceNodeId);
        var targetNode = document.Nodes.FirstOrDefault(n => n.Id == edge.TargetNodeId);
        if (sourceNode is null || targetNode is null)
            return false;

        var sourceOut = _registry.ResolveOutputPort(sourceNode.NodeTypeId, edge.SourcePortId);
        var targetIn = _registry.ResolveInputPort(targetNode.NodeTypeId, edge.TargetPortId);
        if (sourceOut is null || targetIn is null)
            return false;

        foreach (var inPort in existingDef.InputPorts)
        {
            foreach (var outPort in existingDef.OutputPorts)
            {
                if (!PortsQuickCompatible(sourceOut, inPort) || !PortsQuickCompatible(outPort, targetIn))
                    continue;

                var temp = _serializer.Deserialize(_serializer.Serialize(document));
                temp.Edges.RemoveAll(e => e.Id == edgeId);
                temp.Edges.RemoveAll(e =>
                    (e.TargetNodeId == existingNodeId && string.Equals(e.TargetPortId, inPort.Id, StringComparison.Ordinal)) ||
                    (e.SourceNodeId == existingNodeId && string.Equals(e.SourcePortId, outPort.Id, StringComparison.Ordinal)));

                var v1 = _topology.ValidateConnection(
                    temp,
                    edge.SourceNodeId,
                    edge.SourcePortId,
                    existingNodeId,
                    inPort.Id);
                if (!v1.IsAllowed)
                    continue;

                var v2 = _topology.ValidateConnection(
                    temp,
                    existingNodeId,
                    outPort.Id,
                    edge.TargetNodeId,
                    edge.TargetPortId);
                if (!v2.IsAllowed)
                    continue;

                var removed = new List<Guid> { edgeId };
                foreach (var e in document.Edges)
                {
                    if (e.Id == edgeId)
                        continue;
                    if (e.TargetNodeId == existingNodeId && string.Equals(e.TargetPortId, inPort.Id, StringComparison.Ordinal))
                        removed.Add(e.Id);
                    if (e.SourceNodeId == existingNodeId && string.Equals(e.SourcePortId, outPort.Id, StringComparison.Ordinal))
                        removed.Add(e.Id);
                }

                plan = new AutomationGraphExistingNodeSplicePlan
                {
                    RemovedEdgeIds = removed.Distinct().ToArray(),
                    EdgeFromSourceToExisting = new AutomationEdgeState
                    {
                        Id = Guid.NewGuid(),
                        SourceNodeId = edge.SourceNodeId,
                        SourcePortId = edge.SourcePortId,
                        TargetNodeId = existingNodeId,
                        TargetPortId = inPort.Id
                    },
                    EdgeFromExistingToTarget = new AutomationEdgeState
                    {
                        Id = Guid.NewGuid(),
                        SourceNodeId = existingNodeId,
                        SourcePortId = outPort.Id,
                        TargetNodeId = edge.TargetNodeId,
                        TargetPortId = edge.TargetPortId
                    }
                };
                return true;
            }
        }

        return false;
    }

    private static bool PortsQuickCompatible(AutomationPortDescriptor sourceOut, AutomationPortDescriptor targetIn) =>
        sourceOut.FlowKind == targetIn.FlowKind &&
        AutomationPortCompatibility.TypesMatch(sourceOut.PortType, targetIn.PortType) &&
        sourceOut.IsOutput &&
        !targetIn.IsOutput;

    private AutomationNodeState CloneNodeForSimulation(AutomationNodeState node)
    {
        var wrap = new AutomationGraphDocument { Nodes = [node] };
        return _serializer.Deserialize(_serializer.Serialize(wrap)).Nodes[0];
    }
}
