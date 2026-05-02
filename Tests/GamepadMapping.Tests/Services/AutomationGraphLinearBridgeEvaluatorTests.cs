#nullable enable

using GamepadMapperGUI.Models.Automation;
using GamepadMapperGUI.Services.Automation;

namespace GamepadMapping.Tests.Services;

public sealed class AutomationGraphLinearBridgeEvaluatorTests
{
    [Fact]
    public void TryBuildBridgeAcrossNode_WhenSingleLinearExecutionChain_BuildsDirectEdge()
    {
        var registry = new NodeTypeRegistry();
        var topology = new AutomationTopologyAnalyzer(registry);
        var sut = new AutomationGraphLinearBridgeEvaluator(topology);

        var n1 = Guid.NewGuid();
        var n2 = Guid.NewGuid();
        var n3 = Guid.NewGuid();
        var e1 = Guid.NewGuid();
        var e2 = Guid.NewGuid();
        var doc = new AutomationGraphDocument
        {
            Nodes =
            [
                new AutomationNodeState
                {
                    Id = n1,
                    NodeTypeId = "automation.delay",
                    X = 0,
                    Y = 0,
                    Properties = new System.Text.Json.Nodes.JsonObject()
                },
                new AutomationNodeState
                {
                    Id = n2,
                    NodeTypeId = "automation.delay",
                    X = 100,
                    Y = 0,
                    Properties = new System.Text.Json.Nodes.JsonObject()
                },
                new AutomationNodeState
                {
                    Id = n3,
                    NodeTypeId = "automation.delay",
                    X = 200,
                    Y = 0,
                    Properties = new System.Text.Json.Nodes.JsonObject()
                }
            ],
            Edges =
            [
                new AutomationEdgeState
                {
                    Id = e1,
                    SourceNodeId = n1,
                    SourcePortId = AutomationPortIds.FlowOut,
                    TargetNodeId = n2,
                    TargetPortId = AutomationPortIds.FlowIn
                },
                new AutomationEdgeState
                {
                    Id = e2,
                    SourceNodeId = n2,
                    SourcePortId = AutomationPortIds.FlowOut,
                    TargetNodeId = n3,
                    TargetPortId = AutomationPortIds.FlowIn
                }
            ]
        };

        Assert.True(sut.TryBuildBridgeAcrossNode(doc, n2, out var plan));
        Assert.NotNull(plan);
        Assert.Equal(n1, plan.SourceNodeId);
        Assert.Equal(n3, plan.TargetNodeId);
        Assert.Equal(AutomationPortIds.FlowOut, plan.SourcePortId);
        Assert.Equal(AutomationPortIds.FlowIn, plan.TargetPortId);
        Assert.Contains(e1, plan.RemovedEdgeIds);
        Assert.Contains(e2, plan.RemovedEdgeIds);
    }

    [Fact]
    public void TryBuildBridgeAcrossNode_WhenMiddleNodeHasTwoOutgoing_ReturnsFalse()
    {
        var registry = new NodeTypeRegistry();
        var topology = new AutomationTopologyAnalyzer(registry);
        var sut = new AutomationGraphLinearBridgeEvaluator(topology);

        var n1 = Guid.NewGuid();
        var n2 = Guid.NewGuid();
        var n3 = Guid.NewGuid();
        var n4 = Guid.NewGuid();
        var doc = new AutomationGraphDocument
        {
            Nodes =
            [
                new AutomationNodeState
                {
                    Id = n1,
                    NodeTypeId = "automation.delay",
                    X = 0,
                    Y = 0,
                    Properties = new System.Text.Json.Nodes.JsonObject()
                },
                new AutomationNodeState
                {
                    Id = n2,
                    NodeTypeId = "logic.branch_bool",
                    X = 100,
                    Y = 0,
                    Properties = new System.Text.Json.Nodes.JsonObject()
                },
                new AutomationNodeState
                {
                    Id = n3,
                    NodeTypeId = "automation.delay",
                    X = 200,
                    Y = 0,
                    Properties = new System.Text.Json.Nodes.JsonObject()
                },
                new AutomationNodeState
                {
                    Id = n4,
                    NodeTypeId = "automation.delay",
                    X = 200,
                    Y = 50,
                    Properties = new System.Text.Json.Nodes.JsonObject()
                }
            ],
            Edges =
            [
                new AutomationEdgeState
                {
                    Id = Guid.NewGuid(),
                    SourceNodeId = n1,
                    SourcePortId = AutomationPortIds.FlowOut,
                    TargetNodeId = n2,
                    TargetPortId = AutomationPortIds.FlowIn
                },
                new AutomationEdgeState
                {
                    Id = Guid.NewGuid(),
                    SourceNodeId = n2,
                    SourcePortId = AutomationPortIds.BranchTrue,
                    TargetNodeId = n3,
                    TargetPortId = AutomationPortIds.FlowIn
                },
                new AutomationEdgeState
                {
                    Id = Guid.NewGuid(),
                    SourceNodeId = n2,
                    SourcePortId = AutomationPortIds.BranchFalse,
                    TargetNodeId = n4,
                    TargetPortId = AutomationPortIds.FlowIn
                }
            ]
        };

        Assert.False(sut.TryBuildBridgeAcrossNode(doc, n2, out var plan));
        Assert.Null(plan);
    }
}
