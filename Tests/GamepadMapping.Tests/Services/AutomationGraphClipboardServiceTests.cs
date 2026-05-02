#nullable enable

using System.Text.Json.Nodes;
using GamepadMapperGUI.Models.Automation;
using GamepadMapperGUI.Services.Automation;

namespace GamepadMapping.Tests.Services;

public sealed class AutomationGraphClipboardServiceTests
{
    [Fact]
    public void TryParsePayloadForPaste_RepositionsFragmentCentroidToAnchor()
    {
        var serializer = new AutomationGraphJsonSerializer();
        var sut = new AutomationGraphClipboardService(serializer);
        var id = Guid.NewGuid();
        var doc = new AutomationGraphDocument();
        doc.Nodes.Add(new AutomationNodeState
        {
            Id = id,
            NodeTypeId = "automation.delay",
            X = 0,
            Y = 0,
            Properties = new JsonObject()
        });

        Assert.True(sut.TryBuildSelectionPayload(doc, [id], out var payload));
        Assert.True(sut.TryParsePayloadForPaste(payload, 150, 75, out var fragment));
        Assert.Single(fragment.Nodes);
        Assert.NotEqual(id, fragment.Nodes[0].Id);
        Assert.Equal(150, fragment.Nodes[0].X, 2);
        Assert.Equal(75, fragment.Nodes[0].Y, 2);
    }
}
