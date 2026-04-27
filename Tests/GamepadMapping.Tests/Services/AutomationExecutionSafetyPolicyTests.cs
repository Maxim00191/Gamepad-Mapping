using GamepadMapperGUI.Models.Automation;
using GamepadMapperGUI.Services.Automation;

namespace GamepadMapping.Tests.Services;

public sealed class AutomationExecutionSafetyPolicyTests
{
    [Fact]
    public void GetLimits_ScalesWithGraphComplexity_WithinBounds()
    {
        var doc = new AutomationGraphDocument
        {
            Nodes = Enumerable.Range(0, 40)
                .Select(_ => new AutomationNodeState { Id = Guid.NewGuid(), NodeTypeId = "automation.delay" })
                .ToList(),
            Edges = Enumerable.Range(0, 80)
                .Select(_ => new AutomationEdgeState
                {
                    Id = Guid.NewGuid(),
                    SourceNodeId = Guid.NewGuid(),
                    SourcePortId = "flow.out",
                    TargetNodeId = Guid.NewGuid(),
                    TargetPortId = "flow.in"
                })
                .ToList()
        };

        var sut = new AutomationExecutionSafetyPolicy();
        var limits = sut.GetLimits(doc);

        Assert.True(limits.MaxExecutionSteps > 400);
        Assert.InRange(limits.MaxExecutionSteps, 400, 10000);
        Assert.InRange(limits.MaxLoopIterationsPerNode, 1000, 5000);
        Assert.InRange(limits.MaxDelayMilliseconds, 120000, 300000);
    }

    [Fact]
    public void GetLimits_EmptyGraph_UsesBaseline()
    {
        var sut = new AutomationExecutionSafetyPolicy();
        var limits = sut.GetLimits(new AutomationGraphDocument());

        Assert.Equal(400, limits.MaxExecutionSteps);
        Assert.Equal(1000, limits.MaxLoopIterationsPerNode);
        Assert.Equal(120000, limits.MaxDelayMilliseconds);
    }
}
