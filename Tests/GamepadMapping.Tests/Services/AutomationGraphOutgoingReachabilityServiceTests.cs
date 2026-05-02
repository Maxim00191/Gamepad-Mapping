#nullable enable

using GamepadMapperGUI.Models.Automation;
using GamepadMapperGUI.Services.Automation;

namespace GamepadMapping.Tests.Services;

public sealed class AutomationGraphOutgoingReachabilityServiceTests
{
    private readonly AutomationGraphOutgoingReachabilityService _sut = new();
    private readonly HashSet<Guid> _set = [];

    [Fact]
    public void Collect_LinearChain_IncludesAllDownstream()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var c = Guid.NewGuid();
        var doc = new AutomationGraphDocument
        {
            Nodes = [Node(a), Node(b), Node(c)],
            Edges =
            [
                Edge(a, b),
                Edge(b, c)
            ]
        };

        _sut.CollectReachableTargetNodeIds(doc, a, _set);

        Assert.Equal(2, _set.Count);
        Assert.Contains(b, _set);
        Assert.Contains(c, _set);
    }

    [Fact]
    public void Collect_Branches_FollowsAllPaths()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var c = Guid.NewGuid();
        var d = Guid.NewGuid();
        var doc = new AutomationGraphDocument
        {
            Nodes = [Node(a), Node(b), Node(c), Node(d)],
            Edges = [Edge(a, b), Edge(a, c), Edge(b, d)]
        };

        _sut.CollectReachableTargetNodeIds(doc, a, _set);

        Assert.Equal(3, _set.Count);
        Assert.Contains(b, _set);
        Assert.Contains(c, _set);
        Assert.Contains(d, _set);
    }

    [Fact]
    public void Collect_TwoNodeCycle_DoesNotLoopOrIncludeOrigin()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var doc = new AutomationGraphDocument
        {
            Nodes = [Node(a), Node(b)],
            Edges = [Edge(a, b), Edge(b, a)]
        };

        _sut.CollectReachableTargetNodeIds(doc, a, _set);
        Assert.Single(_set);
        Assert.Contains(b, _set);
    }

    [Fact]
    public void Collect_NoOutgoing_YieldsEmpty()
    {
        var a = Guid.NewGuid();
        var doc = new AutomationGraphDocument
        {
            Nodes = [Node(a)],
            Edges = []
        };

        _sut.CollectReachableTargetNodeIds(doc, a, _set);
        Assert.Empty(_set);
    }

    [Fact]
    public void Collect_ReusedHashSet_IsCleared()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        _set.Add(Guid.NewGuid());
        var doc = new AutomationGraphDocument
        {
            Nodes = [Node(a), Node(b)],
            Edges = [Edge(a, b)]
        };

        _sut.CollectReachableTargetNodeIds(doc, a, _set);
        Assert.Single(_set);
        Assert.Contains(b, _set);
    }

    private static AutomationNodeState Node(Guid id) =>
        new()
        {
            Id = id,
            NodeTypeId = "automation.delay",
            X = 0,
            Y = 0,
            Properties = new System.Text.Json.Nodes.JsonObject()
        };

    private static AutomationEdgeState Edge(Guid from, Guid to) =>
        new()
        {
            Id = Guid.NewGuid(),
            SourceNodeId = from,
            SourcePortId = "out",
            TargetNodeId = to,
            TargetPortId = "in"
        };
}
