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
}
