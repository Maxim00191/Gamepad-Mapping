#nullable enable

using System.Collections.Generic;
using GamepadMapperGUI.Interfaces.Services.Automation;
using GamepadMapperGUI.Models.Automation;

namespace GamepadMapperGUI.Services.Automation;

public sealed class AutomationGraphOutgoingReachabilityService : IAutomationGraphOutgoingReachabilityService
{
    public void CollectReachableTargetNodeIds(
        AutomationGraphDocument document,
        Guid originNodeId,
        HashSet<Guid> destination)
    {
        destination.Clear();
        if (document.Nodes.Count == 0 || document.Edges.Count == 0)
            return;

        Dictionary<Guid, List<Guid>>? adjacency = null;
        foreach (var edge in document.Edges)
        {
            adjacency ??= new Dictionary<Guid, List<Guid>>(document.Edges.Count);
            if (!adjacency.TryGetValue(edge.SourceNodeId, out var targets))
            {
                targets = [];
                adjacency[edge.SourceNodeId] = targets;
            }

            targets.Add(edge.TargetNodeId);
        }

        if (adjacency is null || !adjacency.TryGetValue(originNodeId, out var firstTargets))
            return;

        var visited = new HashSet<Guid> { originNodeId };
        var queue = new Queue<Guid>();

        foreach (var t in firstTargets)
        {
            if (!visited.Add(t))
                continue;
            destination.Add(t);
            queue.Enqueue(t);
        }

        while (queue.Count > 0)
        {
            var id = queue.Dequeue();
            if (!adjacency.TryGetValue(id, out var next))
                continue;

            foreach (var t in next)
            {
                if (!visited.Add(t))
                    continue;
                destination.Add(t);
                queue.Enqueue(t);
            }
        }
    }
}
