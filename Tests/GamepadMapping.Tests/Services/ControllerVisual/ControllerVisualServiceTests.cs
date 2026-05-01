using Gamepad_Mapping.Services.ControllerVisual;
using GamepadMapperGUI.Models;

namespace GamepadMapping.Tests.Services.ControllerVisual;

public class ControllerVisualServiceTests
{
    [Fact]
    public void MapIdToBinding_Touchpad_ReturnsButtonBinding()
    {
        var service = new ControllerVisualService();

        var binding = service.MapIdToBinding("btn_touchpad");

        Assert.NotNull(binding);
        Assert.Equal(GamepadBindingType.Button, binding.Type);
        Assert.Equal("Touchpad", binding.Value);
    }

    [Fact]
    public void EnumerateMappedLogicalControlIds_IncludesTouchpad()
    {
        var service = new ControllerVisualService();

        var logicalIds = service.EnumerateMappedLogicalControlIds().ToArray();

        Assert.Contains("btn_touchpad", logicalIds);
    }

    [Fact]
    public void IsTouchpadSurfaceLogicalControl_RecognizesCanonicalId()
    {
        var service = new ControllerVisualService();

        Assert.True(service.IsTouchpadSurfaceLogicalControl("btn_touchpad"));
        Assert.False(service.IsTouchpadSurfaceLogicalControl("btn_A"));
    }

    [Fact]
    public void MapBindingToId_TouchpadGesture_ReturnsTouchpadLogicalId()
    {
        var service = new ControllerVisualService();

        Assert.Equal("btn_touchpad", service.MapBindingToId("SWIPE_UP", GamepadBindingType.Touchpad));
        Assert.Equal("btn_touchpad", service.MapBindingToId("MOUSEX", GamepadBindingType.Touchpad));
    }

    [Fact]
    public void IsMappingOnLogicalControl_TouchpadType_MatchesTouchpadDiagramRegion()
    {
        var service = new ControllerVisualService();
        var mapping = new MappingEntry
        {
            From = new GamepadBinding { Type = GamepadBindingType.Touchpad, Value = "SWIPE_UP" }
        };

        Assert.True(service.IsMappingOnLogicalControl(mapping, "btn_touchpad"));
    }

    [Fact]
    public void IsMappingOnLogicalControl_LegacyButtonTouchpad_MatchesTouchpadDiagramRegion()
    {
        var service = new ControllerVisualService();
        var mapping = new MappingEntry
        {
            From = new GamepadBinding { Type = GamepadBindingType.Button, Value = "Touchpad" }
        };

        Assert.True(service.IsMappingOnLogicalControl(mapping, "btn_touchpad"));
    }
}
