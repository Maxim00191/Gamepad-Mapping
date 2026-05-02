#nullable enable

using GamepadMapperGUI.Interfaces.Services.Automation;
using GamepadMapperGUI.Models.Automation;

namespace GamepadMapperGUI.Services.Automation;

public sealed class AutomationGraphClipboardService(IAutomationGraphSerializer serializer) : IAutomationGraphClipboardService
{
    private readonly IAutomationGraphSerializer _serializer = serializer;

    public bool TryBuildSelectionPayload(AutomationGraphDocument document, IReadOnlyCollection<Guid> selectedNodeIds, out string payloadText)
    {
        payloadText = "";
        if (selectedNodeIds.Count == 0)
            return false;

        var idSet = new HashSet<Guid>(selectedNodeIds);
        var slice = new AutomationGraphDocument();
        foreach (var id in idSet)
        {
            var node = document.Nodes.FirstOrDefault(n => n.Id == id);
            if (node is null)
                continue;

            slice.Nodes.Add(CloneNode(node));
        }

        if (slice.Nodes.Count == 0)
            return false;

        foreach (var edge in document.Edges)
        {
            if (!idSet.Contains(edge.SourceNodeId) || !idSet.Contains(edge.TargetNodeId))
                continue;

            slice.Edges.Add(CloneEdge(edge));
        }

        payloadText = _serializer.Serialize(slice);
        return true;
    }

    public bool TryParsePayloadForPaste(string payloadText, double anchorLogicalX, double anchorLogicalY, out AutomationGraphDocument fragment)
    {
        fragment = new AutomationGraphDocument();
        if (string.IsNullOrWhiteSpace(payloadText))
            return false;

        AutomationGraphDocument incoming;
        try
        {
            incoming = _serializer.Deserialize(payloadText);
        }
        catch
        {
            return false;
        }

        if (incoming.Nodes.Count == 0)
            return false;

        var idMap = new Dictionary<Guid, Guid>();
        foreach (var node in incoming.Nodes)
            idMap[node.Id] = Guid.NewGuid();

        if (idMap.Count == 0)
            return false;

        var minX = incoming.Nodes.Min(n => n.X);
        var minY = incoming.Nodes.Min(n => n.Y);
        var maxX = incoming.Nodes.Max(n => n.X);
        var maxY = incoming.Nodes.Max(n => n.Y);
        var cx = (minX + maxX) / 2d;
        var cy = (minY + maxY) / 2d;
        var dx = anchorLogicalX - cx;
        var dy = anchorLogicalY - cy;

        foreach (var node in incoming.Nodes)
        {
            if (!idMap.TryGetValue(node.Id, out var newId))
                continue;

            var copy = CloneNode(node);
            copy.Id = newId;
            copy.X += dx;
            copy.Y += dy;
            fragment.Nodes.Add(copy);
        }

        foreach (var edge in incoming.Edges)
        {
            if (!idMap.TryGetValue(edge.SourceNodeId, out var newSource) ||
                !idMap.TryGetValue(edge.TargetNodeId, out var newTarget))
            {
                continue;
            }

            fragment.Edges.Add(new AutomationEdgeState
            {
                Id = Guid.NewGuid(),
                SourceNodeId = newSource,
                SourcePortId = edge.SourcePortId,
                TargetNodeId = newTarget,
                TargetPortId = edge.TargetPortId
            });
        }

        return fragment.Nodes.Count > 0;
    }

    private AutomationNodeState CloneNode(AutomationNodeState node)
    {
        var wrap = new AutomationGraphDocument { Nodes = [node] };
        return _serializer.Deserialize(_serializer.Serialize(wrap)).Nodes[0];
    }

    private static AutomationEdgeState CloneEdge(AutomationEdgeState edge) =>
        new()
        {
            Id = edge.Id,
            SourceNodeId = edge.SourceNodeId,
            SourcePortId = edge.SourcePortId,
            TargetNodeId = edge.TargetNodeId,
            TargetPortId = edge.TargetPortId
        };
}
