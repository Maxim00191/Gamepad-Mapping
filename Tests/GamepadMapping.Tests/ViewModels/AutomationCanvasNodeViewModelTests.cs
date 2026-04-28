#nullable enable

using Gamepad_Mapping.ViewModels;
using GamepadMapperGUI.Models.Automation;

namespace GamepadMapping.Tests.ViewModels;

public sealed class AutomationCanvasNodeViewModelTests
{
    [Fact]
    public void InlineEditors_RaisesHasInlineEditorsChangedWhenMutated()
    {
        var node = new AutomationCanvasNodeViewModel(
            new AutomationNodeState
            {
                Id = Guid.NewGuid(),
                NodeTypeId = "perception.find_image"
            },
            "Find image",
            "",
            [],
            []);
        var raised = false;
        node.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(AutomationCanvasNodeViewModel.HasInlineEditors))
                raised = true;
        };

        node.InlineEditors.Add(new AutomationInlineNodeFieldViewModel
        {
            NodeId = node.Id,
            NodeTypeId = node.NodeTypeId,
            PropertyKey = AutomationNodePropertyKeys.FindImageAlgorithm,
            Label = "Algorithm",
            Kind = AutomationNodeInlineEditorKind.Action
        });

        Assert.True(raised);
        Assert.True(node.HasInlineEditors);
    }
}
