#nullable enable

using GamepadMapperGUI.Models.Automation;
using GamepadMapperGUI.Services.Automation;

namespace GamepadMapping.Tests.Services;

public sealed class AutomationGraphLinkInsertionEvaluatorTests
{
    [Fact]
    public void TryBuildPlan_ForExecutionChain_InsertsCompatibleDelay()
    {
        var registry = new NodeTypeRegistry();
        var topology = new AutomationTopologyAnalyzer(registry);
        var serializer = new AutomationGraphJsonSerializer();
        var sut = new AutomationGraphLinkInsertionEvaluator(registry, topology, serializer);

        var n1 = Guid.NewGuid();
        var n2 = Guid.NewGuid();
        var edgeId = Guid.NewGuid();
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
                    X = 300,
                    Y = 0,
                    Properties = new System.Text.Json.Nodes.JsonObject()
                }
            ],
            Edges =
            [
                new AutomationEdgeState
                {
                    Id = edgeId,
                    SourceNodeId = n1,
                    SourcePortId = AutomationPortIds.FlowOut,
                    TargetNodeId = n2,
                    TargetPortId = AutomationPortIds.FlowIn
                }
            ]
        };

        Assert.True(sut.TryBuildPlan(doc, edgeId, "automation.delay", 40, 50, out var plan));
        Assert.NotNull(plan);
        Assert.Equal(edgeId, plan.RemovedEdgeId);
        Assert.Equal(n1, plan.EdgeFromSourceToNew.SourceNodeId);
        Assert.Equal(plan.NewNode.Id, plan.EdgeFromSourceToNew.TargetNodeId);
        Assert.Equal(plan.NewNode.Id, plan.EdgeFromNewToTarget.SourceNodeId);
        Assert.Equal(n2, plan.EdgeFromNewToTarget.TargetNodeId);
    }

    [Fact]
    public void TryBuildExistingNodeSplicePlan_InsertsNodeOntoFlowEdge()
    {
        var registry = new NodeTypeRegistry();
        var topology = new AutomationTopologyAnalyzer(registry);
        var serializer = new AutomationGraphJsonSerializer();
        var sut = new AutomationGraphLinkInsertionEvaluator(registry, topology, serializer);

        var n1 = Guid.NewGuid();
        var n2 = Guid.NewGuid();
        var n3 = Guid.NewGuid();
        var edgeId = Guid.NewGuid();
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
                    X = 300,
                    Y = 0,
                    Properties = new System.Text.Json.Nodes.JsonObject()
                },
                new AutomationNodeState
                {
                    Id = n3,
                    NodeTypeId = "automation.delay",
                    X = 150,
                    Y = 200,
                    Properties = new System.Text.Json.Nodes.JsonObject()
                }
            ],
            Edges =
            [
                new AutomationEdgeState
                {
                    Id = edgeId,
                    SourceNodeId = n1,
                    SourcePortId = AutomationPortIds.FlowOut,
                    TargetNodeId = n2,
                    TargetPortId = AutomationPortIds.FlowIn
                }
            ]
        };

        Assert.True(sut.TryBuildExistingNodeSplicePlan(doc, edgeId, n3, out var plan));
        Assert.NotNull(plan);
        Assert.Contains(edgeId, plan.RemovedEdgeIds);
        Assert.Equal(n1, plan.EdgeFromSourceToExisting.SourceNodeId);
        Assert.Equal(n3, plan.EdgeFromSourceToExisting.TargetNodeId);
        Assert.Equal(n3, plan.EdgeFromExistingToTarget.SourceNodeId);
        Assert.Equal(n2, plan.EdgeFromExistingToTarget.TargetNodeId);
    }
}
