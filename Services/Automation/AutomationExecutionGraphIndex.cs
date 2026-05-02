#nullable enable

using GamepadMapperGUI.Interfaces.Services.Automation;
using GamepadMapperGUI.Models.Automation;

namespace GamepadMapperGUI.Services.Automation;

public sealed class AutomationExecutionGraphIndex : IAutomationExecutionGraphIndex
{
    private readonly Dictionary<Guid, AutomationNodeState> _nodeById;
    private readonly Dictionary<(Guid SourceNodeId, string SourcePortId), Guid> _executionTargets;
    private readonly Dictionary<(Guid TargetNodeId, string TargetPortId), (Guid SourceNodeId, string SourcePortId)> _dataSources;
    private readonly AutomationLoopScopeIndex _loopScopes;

    public AutomationExecutionGraphIndex(AutomationGraphDocument document, INodeTypeRegistry registry)
    {
        _nodeById = document.Nodes.ToDictionary(n => n.Id);
        _executionTargets = [];
        _dataSources = [];
        _loopScopes = new AutomationLoopScopeIndex(document);

        foreach (var edge in document.Edges)
        {
            if (!_nodeById.TryGetValue(edge.SourceNodeId, out var src) || !_nodeById.TryGetValue(edge.TargetNodeId, out var tgt))
                continue;

            var outPort = registry.ResolveOutputPort(src.NodeTypeId, edge.SourcePortId);
            var inPort = registry.ResolveInputPort(tgt.NodeTypeId, edge.TargetPortId);
            if (outPort is null || inPort is null)
                continue;

            if (outPort.FlowKind == AutomationPortFlowKind.Execution && inPort.FlowKind == AutomationPortFlowKind.Execution)
                _executionTargets[(edge.SourceNodeId, edge.SourcePortId)] = edge.TargetNodeId;
            else if (outPort.FlowKind == AutomationPortFlowKind.Data && inPort.FlowKind == AutomationPortFlowKind.Data)
                _dataSources[(edge.TargetNodeId, edge.TargetPortId)] = (edge.SourceNodeId, edge.SourcePortId);
        }
    }

    public AutomationNodeState? GetNode(Guid nodeId) => _nodeById.GetValueOrDefault(nodeId);

    public Guid? GetExecutionTarget(Guid sourceNodeId, string sourcePortId) =>
        _executionTargets.TryGetValue((sourceNodeId, sourcePortId), out var nodeId) ? nodeId : null;

    public Guid? GetDataSource(Guid targetNodeId, string targetPortId) =>
        _dataSources.TryGetValue((targetNodeId, targetPortId), out var source) ? source.SourceNodeId : null;

    public (Guid SourceNodeId, string SourcePortId)? GetDataSourceLink(Guid targetNodeId, string targetPortId) =>
        _dataSources.TryGetValue((targetNodeId, targetPortId), out var source) ? source : null;

    public IReadOnlyList<Guid> FindExecutionRoots(INodeTypeRegistry registry)
    {
        var incoming = new HashSet<Guid>();
        foreach (var ((_, _), targetNodeId) in _executionTargets)
            incoming.Add(targetNodeId);

        return _nodeById.Values
            .Where(n => !incoming.Contains(n.Id) && HasExecutionOutPort(n, registry))
            .Select(n => n.Id)
            .ToList();
    }

    public bool TryGetLoopStartNodeIdByScopeLabel(string scopeLabel, out Guid loopNodeId) =>
        _loopScopes.TryGetLoopNodeId(scopeLabel, out loopNodeId);

    private static bool HasExecutionOutPort(AutomationNodeState node, INodeTypeRegistry registry)
    {
        var def = registry.GetRequired(node.NodeTypeId);
        return def.OutputPorts.Any(p => p.FlowKind == AutomationPortFlowKind.Execution && p.IsOutput);
    }
}
