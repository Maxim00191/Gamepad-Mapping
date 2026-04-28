#nullable enable

using GamepadMapperGUI.Models.Automation;
using GamepadMapperGUI.Services.Automation;

namespace GamepadMapping.Tests.Services;

public sealed class AutomationNodeContextMenuServiceTests
{
    [Fact]
    public void BuildNodeActions_IncludesCopyActions()
    {
        var service = new AutomationNodeContextMenuService();

        var actions = service.BuildNodeActions(
            Guid.NewGuid(),
            "perception.find_image",
            null,
            null);

        Assert.Contains(actions, x => x.Kind == AutomationNodeContextMenuActionKind.CopyNodeId);
        Assert.Contains(actions, x => x.Kind == AutomationNodeContextMenuActionKind.CopyNodeTypeId);
    }

    [Fact]
    public void BuildNodeActions_CaptureSelectedAndTarget_AddsEnabledCacheSourceAction()
    {
        var service = new AutomationNodeContextMenuService();
        var selectedId = Guid.NewGuid();
        var targetId = Guid.NewGuid();

        var actions = service.BuildNodeActions(
            targetId,
            "perception.capture_screen",
            selectedId,
            "perception.capture_screen");

        var action = Assert.Single(actions, x => x.Kind == AutomationNodeContextMenuActionKind.UseAsCaptureCacheSource);
        Assert.True(action.IsEnabled);
    }

    [Fact]
    public void BuildNodeActions_CaptureSelectedButSameNode_DisablesCacheSourceAction()
    {
        var service = new AutomationNodeContextMenuService();
        var nodeId = Guid.NewGuid();

        var actions = service.BuildNodeActions(
            nodeId,
            "perception.capture_screen",
            nodeId,
            "perception.capture_screen");

        var action = Assert.Single(actions, x => x.Kind == AutomationNodeContextMenuActionKind.UseAsCaptureCacheSource);
        Assert.False(action.IsEnabled);
    }
}
