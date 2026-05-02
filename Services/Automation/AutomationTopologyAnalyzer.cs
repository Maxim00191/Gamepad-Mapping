#nullable enable

using System.Collections.Generic;
using GamepadMapperGUI.Interfaces.Services.Automation;
using GamepadMapperGUI.Models.Automation;

namespace GamepadMapperGUI.Services.Automation;

public sealed class AutomationTopologyAnalyzer(INodeTypeRegistry registry) : IAutomationTopologyAnalyzer
{
    private readonly INodeTypeRegistry _registry = registry;

    public ConnectionValidationResult ValidateConnection(
        AutomationGraphDocument document,
        Guid sourceNodeId,
        string sourcePortId,
        Guid targetNodeId,
        string targetPortId) =>
        ValidateConnection(document, sourceNodeId, sourcePortId, targetNodeId, targetPortId, null);

    public ConnectionValidationResult ValidateConnection(
        AutomationGraphDocument document,
        Guid sourceNodeId,
        string sourcePortId,
        Guid targetNodeId,
        string targetPortId,
        IReadOnlySet<Guid>? ignoredEdgeIds)
    {
        if (sourceNodeId == targetNodeId)
            return new ConnectionValidationResult(false, "AutomationConnection_InvalidDirection");

        var sourceNode = document.Nodes.FirstOrDefault(n => n.Id == sourceNodeId);
        var targetNode = document.Nodes.FirstOrDefault(n => n.Id == targetNodeId);
        if (sourceNode is null || targetNode is null)
            return new ConnectionValidationResult(false, "AutomationConnection_NodeMissing");

        var outPort = _registry.ResolveOutputPort(sourceNode.NodeTypeId, sourcePortId);
        var inPort = _registry.ResolveInputPort(targetNode.NodeTypeId, targetPortId);
        if (outPort is null)
            return new ConnectionValidationResult(false, "AutomationConnection_SourcePortNotFound");
        if (inPort is null)
            return new ConnectionValidationResult(false, "AutomationConnection_TargetPortNotFound");
        if (!outPort.IsOutput || inPort.IsOutput)
            return new ConnectionValidationResult(false, "AutomationConnection_InvalidDirection");

        if (outPort.FlowKind != inPort.FlowKind)
            return new ConnectionValidationResult(false, "AutomationConnection_TypeMismatch");

        if (!AutomationPortCompatibility.TypesMatch(outPort.PortType, inPort.PortType))
            return new ConnectionValidationResult(false, "AutomationConnection_TypeMismatch");

        if (document.Edges.Any(e =>
                !IsIgnored(e.Id, ignoredEdgeIds) &&
                e.SourceNodeId == sourceNodeId &&
                e.SourcePortId == sourcePortId &&
                e.TargetNodeId == targetNodeId &&
                e.TargetPortId == targetPortId))
        {
            return new ConnectionValidationResult(false, "AutomationConnection_GenericRejected");
        }

        var existingIncoming = document.Edges.FirstOrDefault(e =>
            !IsIgnored(e.Id, ignoredEdgeIds) &&
            e.TargetNodeId == targetNodeId &&
            string.Equals(e.TargetPortId, targetPortId, StringComparison.Ordinal));
        var existingOutgoing = document.Edges.FirstOrDefault(e =>
            !IsIgnored(e.Id, ignoredEdgeIds) &&
            e.SourceNodeId == sourceNodeId &&
            string.Equals(e.SourcePortId, sourcePortId, StringComparison.Ordinal));

        if (outPort.FlowKind == AutomationPortFlowKind.Execution &&
            inPort.FlowKind == AutomationPortFlowKind.Execution &&
            existingOutgoing != default)
        {
            return new ConnectionValidationResult(false, "AutomationConnection_ExecutionOutAlreadyConnected");
        }

        return new ConnectionValidationResult(
            true,
            null,
            existingIncoming?.Id);
    }

    private static bool IsIgnored(Guid edgeId, IReadOnlySet<Guid>? ignoredEdgeIds) =>
        ignoredEdgeIds is not null && ignoredEdgeIds.Contains(edgeId);

    public AutomationTopologyAnalysis Analyze(AutomationGraphDocument document)
    {
        var executionAdjacency = BuildAdjacency(document, AutomationPortFlowKind.Execution);
        var dataAdjacency = BuildAdjacency(document, AutomationPortFlowKind.Data);
        var cycleEdges = FindFirstCycleEdges(executionAdjacency);
        var dataCycleEdges = FindFirstCycleEdges(dataAdjacency);
        var hasExecutionCycle = cycleEdges.Count > 0;
        var hasDataCycle = dataCycleEdges.Count > 0;
        var detail = hasDataCycle ? "AutomationTopology_DataCycleDetected" : null;

        return new AutomationTopologyAnalysis
        {
            HasExecutionCycle = hasExecutionCycle,
            HasDataCycle = hasDataCycle,
            CycleEdgeIds = cycleEdges,
            DataCycleEdgeIds = dataCycleEdges,
            DetailMessageResourceKey = detail
        };
    }

    private Dictionary<Guid, List<(Guid EdgeId, Guid TargetNodeId)>> BuildAdjacency(
        AutomationGraphDocument document,
        AutomationPortFlowKind flowKind)
    {
        var nodeById = document.Nodes.ToDictionary(n => n.Id);
        var adjacency = new Dictionary<Guid, List<(Guid EdgeId, Guid TargetNodeId)>>();
        foreach (var edge in document.Edges)
        {
            if (!nodeById.TryGetValue(edge.SourceNodeId, out var sourceNode) ||
                !nodeById.TryGetValue(edge.TargetNodeId, out var targetNode))
            {
                continue;
            }

            var outPort = _registry.ResolveOutputPort(sourceNode.NodeTypeId, edge.SourcePortId);
            var inPort = _registry.ResolveInputPort(targetNode.NodeTypeId, edge.TargetPortId);
            if (outPort is null || inPort is null)
                continue;
            if (outPort.FlowKind != flowKind || inPort.FlowKind != flowKind)
                continue;
            if (!AutomationPortCompatibility.TypesMatch(outPort.PortType, inPort.PortType))
                continue;

            if (!adjacency.TryGetValue(edge.SourceNodeId, out var outgoing))
            {
                outgoing = [];
                adjacency[edge.SourceNodeId] = outgoing;
            }

            outgoing.Add((edge.Id, edge.TargetNodeId));
        }

        return adjacency;
    }

    private static List<Guid> FindFirstCycleEdges(IReadOnlyDictionary<Guid, List<(Guid EdgeId, Guid TargetNodeId)>> adjacency)
    {
        var color = new Dictionary<Guid, byte>();
        var recursionStack = new HashSet<Guid>();
        var cycleEdges = new List<Guid>();
        foreach (var nodeId in adjacency.Keys)
        {
            if (color.GetValueOrDefault(nodeId) != 0)
                continue;
            if (DfsDetectCycle(nodeId, adjacency, color, recursionStack, cycleEdges))
                break;
        }

        return cycleEdges;
    }

    private static bool DfsDetectCycle(
        Guid nodeId,
        IReadOnlyDictionary<Guid, List<(Guid EdgeId, Guid TargetNodeId)>> adjacency,
        Dictionary<Guid, byte> color,
        HashSet<Guid> recursionStack,
        List<Guid> cycleEdgesOut)
    {
        color[nodeId] = 1;
        recursionStack.Add(nodeId);

        if (adjacency.TryGetValue(nodeId, out var outs))
        {
            foreach (var (edgeId, target) in outs)
            {
                if (color.GetValueOrDefault(target) == 0)
                {
                    if (DfsDetectCycle(target, adjacency, color, recursionStack, cycleEdgesOut))
                    {
                        cycleEdgesOut.Add(edgeId);
                        return true;
                    }
                }
                else if (recursionStack.Contains(target))
                {
                    cycleEdgesOut.Add(edgeId);
                    return true;
                }
            }
        }

        recursionStack.Remove(nodeId);
        color[nodeId] = 2;
        return false;
    }
}
