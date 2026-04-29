#nullable enable

using System.Text.Json.Nodes;
using GamepadMapperGUI.Models.Automation;
using GamepadMapperGUI.Services.Automation;

namespace GamepadMapping.Tests.Models;

public sealed class AutomationLoopScopeIndexTests
{
    [Fact]
    public void HasDuplicateLoopScopeLabels_True_WhenSameLabelTwice()
    {
        var a = new AutomationNodeState { Id = Guid.NewGuid(), NodeTypeId = "automation.loop", Properties = new JsonObject() };
        AutomationNodePropertyReader.WriteString(a.Properties, AutomationNodePropertyKeys.LoopScopeLabel, "x");
        var b = new AutomationNodeState { Id = Guid.NewGuid(), NodeTypeId = "automation.loop", Properties = new JsonObject() };
        AutomationNodePropertyReader.WriteString(b.Properties, AutomationNodePropertyKeys.LoopScopeLabel, "X");

        var doc = new AutomationGraphDocument { Nodes = [a, b] };

        Assert.True(AutomationLoopScopeIndex.HasDuplicateLoopScopeLabels(doc));
    }

    [Fact]
    public void TryGetLoopNodeId_ResolvesCaseInsensitive()
    {
        var loop = new AutomationNodeState { Id = Guid.NewGuid(), NodeTypeId = "automation.loop", Properties = new JsonObject() };
        AutomationNodePropertyReader.WriteString(loop.Properties, AutomationNodePropertyKeys.LoopScopeLabel, "Main");
        var doc = new AutomationGraphDocument { Nodes = [loop] };

        var index = new AutomationLoopScopeIndex(doc);

        Assert.True(index.TryGetLoopNodeId("main", out var id));
        Assert.Equal(loop.Id, id);
    }
}
