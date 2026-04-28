using GamepadMapperGUI.Models.Automation;
using GamepadMapperGUI.Services.Automation;

namespace GamepadMapping.Tests.Services;

public sealed class AutomationTopologyAnalyzerTests
{
    private readonly NodeTypeRegistry _registry = new();

    [Fact]
    public void ValidateConnection_RejectsSecondExecutionEdgeFromSameOutputPort()
    {
        var loop = CreateNode("automation.loop");
        var delayA = CreateNode("automation.delay");
        var delayB = CreateNode("automation.delay");
        var doc = new AutomationGraphDocument
        {
            Nodes = [loop, delayA, delayB],
            Edges =
            [
                new AutomationEdgeState
                {
                    Id = Guid.NewGuid(),
                    SourceNodeId = loop.Id,
                    SourcePortId = "flow.out",
                    TargetNodeId = delayA.Id,
                    TargetPortId = "flow.in"
                }
            ]
        };

        var sut = new AutomationTopologyAnalyzer(_registry);
        var result = sut.ValidateConnection(doc, loop.Id, "flow.out", delayB.Id, "flow.in");

        Assert.False(result.IsAllowed);
        Assert.Equal("AutomationConnection_ExecutionOutAlreadyConnected", result.ReasonResourceKey);
    }

    [Fact]
    public void ValidateConnection_ReturnsExistingIncomingEdgeForSingleInputPorts()
    {
        var captureA = CreateNode("perception.capture_screen");
        var captureB = CreateNode("perception.capture_screen");
        var findImage = CreateNode("perception.find_image");
        var existing = Guid.NewGuid();
        var doc = new AutomationGraphDocument
        {
            Nodes = [captureA, captureB, findImage],
            Edges =
            [
                new AutomationEdgeState
                {
                    Id = existing,
                    SourceNodeId = captureA.Id,
                    SourcePortId = "screen.image",
                    TargetNodeId = findImage.Id,
                    TargetPortId = "haystack.image"
                }
            ]
        };

        var sut = new AutomationTopologyAnalyzer(_registry);
        var result = sut.ValidateConnection(doc, captureB.Id, "screen.image", findImage.Id, "haystack.image");

        Assert.True(result.IsAllowed);
        Assert.Equal(existing, result.ExistingIncomingEdgeId);
    }

    [Fact]
    public void ValidateConnection_AllowsFindProbeToBranchProbeInput()
    {
        var find = CreateNode("perception.find_image");
        var branch = CreateNode("logic.branch_image");
        var doc = new AutomationGraphDocument
        {
            Nodes = [find, branch]
        };

        var sut = new AutomationTopologyAnalyzer(_registry);
        var result = sut.ValidateConnection(doc, find.Id, "probe.image", branch.Id, "probe.image");

        Assert.True(result.IsAllowed);
        Assert.Null(result.ReasonResourceKey);
    }

    [Fact]
    public void ValidateConnection_AllowsExecutionCycleBackEdge()
    {
        var loop = CreateNode("automation.loop");
        var delay = CreateNode("automation.delay");
        var doc = new AutomationGraphDocument
        {
            Nodes = [loop, delay],
            Edges =
            [
                new AutomationEdgeState
                {
                    Id = Guid.NewGuid(),
                    SourceNodeId = loop.Id,
                    SourcePortId = "loop.body",
                    TargetNodeId = delay.Id,
                    TargetPortId = "flow.in"
                }
            ]
        };

        var sut = new AutomationTopologyAnalyzer(_registry);
        var result = sut.ValidateConnection(doc, delay.Id, "flow.out", loop.Id, "flow.in");

        Assert.True(result.IsAllowed);
        Assert.Null(result.ReasonResourceKey);
    }

    [Fact]
    public void Analyze_FlagsDataCyclesAsInvalidTopology()
    {
        var findA = CreateNode("perception.find_image");
        var findB = CreateNode("perception.find_image");
        var doc = new AutomationGraphDocument
        {
            Nodes = [findA, findB],
            Edges =
            [
                new AutomationEdgeState
                {
                    Id = Guid.NewGuid(),
                    SourceNodeId = findA.Id,
                    SourcePortId = "probe.image",
                    TargetNodeId = findB.Id,
                    TargetPortId = "haystack.image"
                },
                new AutomationEdgeState
                {
                    Id = Guid.NewGuid(),
                    SourceNodeId = findB.Id,
                    SourcePortId = "probe.image",
                    TargetNodeId = findA.Id,
                    TargetPortId = "haystack.image"
                }
            ]
        };

        var sut = new AutomationTopologyAnalyzer(_registry);
        var result = sut.Analyze(doc);

        Assert.True(result.HasDataCycle);
        Assert.Equal("AutomationTopology_DataCycleDetected", result.DetailMessageResourceKey);
        Assert.NotEmpty(result.DataCycleEdgeIds);
    }

    private static AutomationNodeState CreateNode(string nodeTypeId) =>
        new()
        {
            Id = Guid.NewGuid(),
            NodeTypeId = nodeTypeId
        };
}
